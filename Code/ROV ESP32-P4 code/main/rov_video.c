/*
 * SPDX-License-Identifier: Apache-2.0
 * Capture/encoder setup adapted from esp_cam_sensor_imx imx219_video:
 * Copyright 2026 esp_cam_sensor_imx contributors.
 * ROV transport: RFC 6184 single NAL and FU-A, RTP payload type 96.
 */
#include <stdio.h>
#include <stdatomic.h>
#include <stdlib.h>
#include <string.h>
#include <inttypes.h>
#include <fcntl.h>
#include <unistd.h>
#include <sys/ioctl.h>
#include <sys/mman.h>
#include "esp_log.h"
#include "esp_timer.h"
#include "esp_random.h"
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "linux/videodev2.h"
#include "esp_video_init.h"
#include "esp_video_device.h"
#include "esp_h264_enc_single_hw.h"
#include "esp_h264_alloc.h"
#include "lwip/sockets.h"
#include "lwip/inet.h"
#include "rov.h"
#include "sdkconfig.h"

#define NBUF 3
#define OUT_BYTES (512 * 1024)
#define RTP_PAYLOAD 1200
static const char *TAG = "rov_video";
static atomic_uint status_frames, status_sent, status_errors, status_drops;
static atomic_bool stop_requested, video_running;
static uint16_t seq;
static uint32_t ssrc;

static void put32(uint8_t *p, uint32_t n)
{
    p[0] = n >> 24; p[1] = n >> 16; p[2] = n >> 8; p[3] = n;
}

static bool packet(int sock, const struct sockaddr_in *dst, const uint8_t *data,
                   size_t len, uint32_t ts, bool marker, int fu, uint8_t nal)
{
    uint8_t p[12 + RTP_PAYLOAD];
    p[0] = 0x80; p[1] = 96 | (marker ? 0x80 : 0);
    p[2] = seq >> 8; p[3] = seq++;
    put32(p + 4, ts); put32(p + 8, ssrc);
    size_t header = 12;
    if (fu >= 0) {
        p[12] = (nal & 0xe0) | 28;
        p[13] = (nal & 0x1f) | fu;
        header += 2;
    }
    if (header + len > sizeof(p)) return false;
    memcpy(p + header, data, len);
    return sendto(sock, p, header + len, 0, (const struct sockaddr *)dst,
                  sizeof(*dst)) == (ssize_t)(header + len);
}

static size_t start_code(const uint8_t *p, size_t n, size_t at, size_t *skip)
{
    for (size_t i = at; i + 2 < n; ++i) {
        if (p[i] || p[i + 1]) continue;
        if (p[i + 2] == 1) { *skip = 3; return i; }
        if (i + 3 < n && p[i + 2] == 0 && p[i + 3] == 1) {
            *skip = 4; return i;
        }
    }
    *skip = 0;
    return n;
}

static bool send_frame(int sock, const struct sockaddr_in *dst,
                       const uint8_t *p, size_t n, uint32_t ts)
{
    size_t skip;
    size_t at = start_code(p, n, 0, &skip);
    if (!skip) return false;
    while (at < n) {
        size_t begin = at + skip, next_skip;
        size_t next = start_code(p, n, begin, &next_skip);
        size_t len = next - begin;
        if (!len) return false;
        bool last = next == n;
        if (len <= RTP_PAYLOAD) {
            if (!packet(sock, dst, p + begin, len, ts, last, -1, 0)) return false;
        } else {
            uint8_t nal = p[begin];
            size_t offset = 1;
            while (offset < len) {
                size_t chunk = len - offset;
                if (chunk > RTP_PAYLOAD - 2) chunk = RTP_PAYLOAD - 2;
                bool end = offset + chunk == len;
                int flags = (offset == 1 ? 0x80 : 0) | (end ? 0x40 : 0);
                if (!packet(sock, dst, p + begin + offset, chunk, ts,
                            last && end, flags, nal)) return false;
                offset += chunk;
            }
        }
        at = next; skip = next_skip;
    }
    return true;
}

static void video_task(void *arg)
{
    int fd = -1, sock = -1;
    bool initialized = false, streaming = false, enc_open = false;
    esp_h264_enc_handle_t enc = NULL;
    uint8_t *out = NULL, *buffers[NBUF] = {0};
    uint32_t lengths[NBUF] = {0}, out_size = 0;
    const int type = V4L2_BUF_TYPE_VIDEO_CAPTURE;
    const esp_video_init_csi_config_t csi = {
        .sccb_config = {
            .init_sccb = true,
            .i2c_config = {.port = 0, .scl_pin = 8, .sda_pin = 7},
            .freq = 100000,
        },
        .reset_pin = -1, .pwdn_pin = -1,
    };
    const esp_video_init_config_t config = {.csi = &csi};
    // Allow USB enumeration and the Ethernet bridge to start first.
    vTaskDelay(pdMS_TO_TICKS(3000));
    esp_err_t err = esp_video_init(&config);
    if (err != ESP_OK) {
        ESP_LOGE(TAG, "Camera init failed: %s; telemetry continues", esp_err_to_name(err));
        goto cleanup;
    }
    initialized = true;
    fd = open(ESP_VIDEO_MIPI_CSI_DEVICE_NAME, O_RDONLY);
    if (fd < 0) goto cleanup;
    struct v4l2_format fmt = {.type = type};
    if (ioctl(fd, VIDIOC_G_FMT, &fmt)) goto cleanup;
    fmt.fmt.pix.pixelformat = V4L2_PIX_FMT_YUV420;
    if (ioctl(fd, VIDIOC_S_FMT, &fmt) || ioctl(fd, VIDIOC_G_FMT, &fmt)) goto cleanup;
    uint32_t w = fmt.fmt.pix.width, h = fmt.fmt.pix.height;
    if (fmt.fmt.pix.pixelformat != V4L2_PIX_FMT_YUV420 || w % 16 || h % 16) {
        ESP_LOGE(TAG, "Unsupported capture layout %"PRIu32"x%"PRIu32, w, h);
        goto cleanup;
    }
    size_t frame_size = (size_t)w * h * 3 / 2;
    struct v4l2_requestbuffers req = {
        .count = NBUF, .type = type, .memory = V4L2_MEMORY_MMAP,
    };
    if (ioctl(fd, VIDIOC_REQBUFS, &req) || req.count != NBUF) goto cleanup;
    for (unsigned i = 0; i < NBUF; ++i) {
        struct v4l2_buffer b = {.index = i, .type = type, .memory = V4L2_MEMORY_MMAP};
        if (ioctl(fd, VIDIOC_QUERYBUF, &b) || b.length < frame_size) goto cleanup;
        buffers[i] = mmap(NULL, b.length, PROT_READ | PROT_WRITE, MAP_SHARED, fd, b.m.offset);
        if (buffers[i] == MAP_FAILED) { buffers[i] = NULL; goto cleanup; }
        lengths[i] = b.length;
        if (ioctl(fd, VIDIOC_QBUF, &b)) goto cleanup;
    }
    out = esp_h264_aligned_calloc(128, 1, OUT_BYTES, &out_size, ESP_H264_MEM_SPIRAM);
    if (!out) goto cleanup;
    esp_h264_enc_cfg_hw_t ec = {
        .pic_type = ESP_H264_RAW_FMT_O_UYY_E_VYY,
        .gop = 30, .fps = 30, .res = {.width = w, .height = h},
        .rc = {.bitrate = CONFIG_ROV_VIDEO_BITRATE, .qp_min = 20, .qp_max = 45},
    };
    if (esp_h264_enc_hw_new(&ec, &enc) != ESP_H264_ERR_OK) goto cleanup;
    if (esp_h264_enc_open(enc) != ESP_H264_ERR_OK) goto cleanup;
    enc_open = true;
    sock = socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP);
    if (sock < 0 || !rov_bind_ethernet_socket(sock)) goto cleanup;
    struct timeval timeout = {.tv_usec = 20000};
    if (setsockopt(sock, SOL_SOCKET, SO_SNDTIMEO, &timeout, sizeof(timeout))) goto cleanup;
    struct sockaddr_in dst = {.sin_family = AF_INET, .sin_port = htons(CONFIG_ROV_VIDEO_PORT)};
    if (inet_pton(AF_INET, CONFIG_ROV_TOPSIDE_IP, &dst.sin_addr) != 1) goto cleanup;
    seq = (uint16_t)esp_random(); ssrc = esp_random();
    if (ioctl(fd, VIDIOC_STREAMON, &type)) goto cleanup;
    streaming = true;
    ESP_LOGI(TAG, "H264 RTP %"PRIu32"x%"PRIu32" -> %s:%d, bitrate=%d",
             w, h, CONFIG_ROV_TOPSIDE_IP, CONFIG_ROV_VIDEO_PORT, CONFIG_ROV_VIDEO_BITRATE);
    uint32_t frames = 0, sent = 0, errors = 0, net_drops = 0;
    int64_t log_at = esp_timer_get_time(), started = log_at;
    for (;;) {
        if (atomic_load(&stop_requested)) break;
        struct v4l2_buffer b = {.type = type, .memory = V4L2_MEMORY_MMAP};
        if (ioctl(fd, VIDIOC_DQBUF, &b)) { ESP_LOGE(TAG, "Capture dequeue failed"); break; }
        if (b.index >= NBUF) { ESP_LOGE(TAG, "Invalid capture index"); break; }
        int64_t now = esp_timer_get_time();
        if (b.bytesused >= frame_size && b.bytesused <= lengths[b.index]) {
            esp_h264_enc_in_frame_t input = {
                .raw_data = {.buffer = buffers[b.index], .len = frame_size},
                .pts = (uint32_t)((now - started) / 1000),
            };
            esp_h264_enc_out_frame_t output = {
                .raw_data = {.buffer = out, .len = out_size},
            };
            esp_h264_err_t result = esp_h264_enc_process(enc, &input, &output);
            if (ioctl(fd, VIDIOC_QBUF, &b)) break;
            frames++;
            if (result == ESP_H264_ERR_OK && output.length && output.length <= out_size) {
                uint32_t timestamp = (uint32_t)((now * 9) / 100);
                if (rov_ethernet_ready() && send_frame(sock, &dst, out, output.length, timestamp)) sent++;
                else net_drops++;
            } else {
                errors++;
                if (errors < 4) ESP_LOGW(TAG, "Encode failed: %d", result);
            }
        } else {
            errors++;
            if (ioctl(fd, VIDIOC_QBUF, &b)) break;
        }
        atomic_store(&status_frames, frames);
        atomic_store(&status_sent, sent);
        atomic_store(&status_errors, errors);
        atomic_store(&status_drops, net_drops);
        if (now - log_at >= 5000000) {
            ESP_LOGI(TAG, "frames=%"PRIu32" sent=%"PRIu32" errors=%"PRIu32" net_drops=%"PRIu32,
                     frames, sent, errors, net_drops);
            log_at = now;
        }
        vTaskDelay(1);
    }
cleanup:
    if (streaming) ioctl(fd, VIDIOC_STREAMOFF, &type);
    if (enc_open) esp_h264_enc_close(enc);
    if (enc) esp_h264_enc_del(enc);
    free(out);
    if (sock >= 0) close(sock);
    if (fd >= 0) close(fd);
    if (initialized) esp_video_deinit();
    atomic_store(&video_running, false);
    ESP_LOGW(TAG, "Video task stopped; telemetry remains active");
    vTaskDelete(NULL);
}

void rov_video_start(void)
{
    atomic_store(&video_running, true);
    if (xTaskCreate(video_task, "rov_video", 8192, NULL, 3, NULL) != pdPASS) {
        atomic_store(&video_running, false);
        ESP_LOGE(TAG, "Cannot create video task; telemetry continues");
    }
}

void rov_video_status_json(char *buf, size_t size)
{
    snprintf(buf, size, "\"video_frames\":%u,\"video_sent\":%u,\"video_errors\":%u,\"video_drops\":%u",
      atomic_load(&status_frames), atomic_load(&status_sent), atomic_load(&status_errors), atomic_load(&status_drops));
}

bool rov_video_stop_for_restart(void)
{
    atomic_store(&stop_requested, true);
    for (int i = 0; i < 300 && atomic_load(&video_running); ++i)
        vTaskDelay(pdMS_TO_TICKS(10));
    return !atomic_load(&video_running);
}
