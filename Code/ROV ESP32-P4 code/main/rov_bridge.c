#include "rov.h"
#include "rov_heartbeat.h"
#include <stdio.h>
#include "mavlink_framing.h"
#include <assert.h>
#include <errno.h>
#include <inttypes.h>
#include <stdatomic.h>
#include <string.h>
#include "sdkconfig.h"
#include "esp_log.h"
#include "esp_timer.h"
#include "esp_system.h"
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "freertos/queue.h"
#include "usb/usb_host.h"
#include "usb/cdc_acm_host.h"
#include "lwip/sockets.h"
#include "lwip/inet.h"

#define UDP_CAPACITY 1472
#define QUEUE_DEPTH 32
#define TELEMETRY_MAX_AGE_US 250000
#ifndef CONFIG_ROV_FORWARD_COMMANDS
#define CONFIG_ROV_FORWARD_COMMANDS 0
#endif
static const char *TAG = "rov_bridge";

typedef struct {
    int64_t received_us;
    uint32_t usb_generation;
    uint32_t net_generation;
    size_t size;
    uint8_t data[ROV_MAVLINK_MAX_FRAME];
} packet_t;

static QueueHandle_t telemetry_queue, command_queue;
static atomic_bool usb_ready, usb_fault;
static atomic_bool update_active;
static portMUX_TYPE heartbeat_mux = portMUX_INITIALIZER_UNLOCKED;
static int64_t heartbeat_us;
static uint32_t heartbeat_generation;
static bool heartbeat_seen, heartbeat_disarmed;

const char *rov_bridge_fc_state(void);
bool rov_bridge_update_allowed(void);
bool rov_bridge_begin_update(void);
void rov_bridge_end_update(void);
static atomic_uint usb_generation;
static atomic_uint usb_rx_frames, udp_tx_frames, usb_tx_frames, dropped, rejected;
// Only the CDC receive callback uses this parser; reset after close drains callbacks.
static rov_framer_t usb_parser;
static int64_t last_usb_rx_us;

static void enqueue_frame(const uint8_t *data, size_t size, void *context)
{
    QueueHandle_t queue = context;
    packet_t packet = {
        .received_us = esp_timer_get_time(),
        .usb_generation = atomic_load(&usb_generation),
        .net_generation = rov_ethernet_generation(),
        .size = size,
    };
    if (!rov_ethernet_ready() || atomic_load(&usb_fault) ||
        (queue == command_queue && atomic_load(&update_active))) {
        atomic_fetch_add(&dropped, 1);
        return;
    }
    memcpy(packet.data, data, size);
    if (xQueueSend(queue, &packet, 0) != pdTRUE) atomic_fetch_add(&dropped, 1);
}

static void telemetry_frame(const uint8_t *data, size_t size, void *context)
{
    bool disarmed;
    if (rov_heartbeat_disarmed(data, size, &disarmed)) {
        portENTER_CRITICAL(&heartbeat_mux);
        heartbeat_us = esp_timer_get_time();
        heartbeat_generation = atomic_load(&usb_generation);
        heartbeat_disarmed = disarmed;
        heartbeat_seen = true;
        portEXIT_CRITICAL(&heartbeat_mux);
    }
    atomic_fetch_add(&usb_rx_frames, 1);
    enqueue_frame(data, size, telemetry_queue);
}

static bool usb_receive(const uint8_t *data, size_t size, void *context)
{
    int64_t now = esp_timer_get_time();
    if (now - last_usb_rx_us > TELEMETRY_MAX_AGE_US) usb_parser = (rov_framer_t){0};
    last_usb_rx_us = now;
    rov_framer_feed(&usb_parser, data, size, telemetry_frame, NULL);
    return true;
}

static void usb_event(const cdc_acm_host_dev_event_data_t *event, void *context)
{
    if (event->type == CDC_ACM_HOST_DEVICE_DISCONNECTED || event->type == CDC_ACM_HOST_ERROR) {
        // The owner task closes the device; never race close against a USB write.
        atomic_store(&usb_ready, false);
        atomic_store(&usb_fault, true);
    }
}

static void usb_library_task(void *context)
{
    while (1) {
        uint32_t flags;
        ESP_ERROR_CHECK(usb_host_lib_handle_events(portMAX_DELAY, &flags));
        if (flags & USB_HOST_LIB_EVENT_FLAGS_NO_CLIENTS) {
            esp_err_t err = usb_host_device_free_all();
            if (err != ESP_OK && err != ESP_ERR_NOT_FINISHED)
                ESP_LOGW(TAG, "USB free: %s", esp_err_to_name(err));
        }
    }
}

static bool current_packet(const packet_t *p, int64_t max_age)
{
    return rov_ethernet_ready() && atomic_load(&usb_ready) &&
           p->usb_generation == atomic_load(&usb_generation) &&
           p->net_generation == rov_ethernet_generation() &&
           esp_timer_get_time() - p->received_us <= max_age;
}

static void usb_device_task(void *context)
{
    const cdc_acm_host_device_config_t config = {
        .connection_timeout_ms = 1000,
        .out_buffer_size = 512,
        .in_buffer_size = 512,
        .event_cb = usb_event,
        .data_cb = usb_receive,
    };
    while (1) {
        atomic_store(&usb_ready, false);
        atomic_store(&usb_fault, false);
        atomic_fetch_add(&usb_generation, 1);
        usb_parser = (rov_framer_t){0};
        cdc_acm_dev_hdl_t device = NULL;
        esp_err_t err = cdc_acm_host_open(CONFIG_ROV_USB_VID, CONFIG_ROV_USB_PID,
                                        CONFIG_ROV_USB_INTERFACE, &config, &device);
        if (err != ESP_OK) {
            vTaskDelay(pdMS_TO_TICKS(500));
            continue;
        }
        cdc_acm_host_desc_print(device);
        // ArduSub CDC is a virtual serial link; this does not set a UART baud rate.
        err = cdc_acm_host_set_control_line_state(device, true, false);
        if (err != ESP_OK) ESP_LOGW(TAG, "CDC DTR: %s", esp_err_to_name(err));
        if (!atomic_load(&usb_fault)) atomic_store(&usb_ready, true);
        ESP_LOGI(TAG, "CDC opened; forwarding MAVLink (generation %u)",
                 atomic_load(&usb_generation));
        while (!atomic_load(&usb_fault)) {
            packet_t packet;
            if (xQueueReceive(command_queue, &packet, pdMS_TO_TICKS(10)) != pdTRUE) continue;
            if (atomic_load(&update_active) || !current_packet(&packet, CONFIG_ROV_COMMAND_MAX_AGE_MS * 1000LL)) {
                atomic_fetch_add(&dropped, 1);
                continue;
            }
            err = cdc_acm_host_data_tx_blocking(device, packet.data, packet.size, 50);
            if (err == ESP_OK) atomic_fetch_add(&usb_tx_frames, 1);
            else {
                atomic_fetch_add(&dropped, 1);
                atomic_store(&usb_fault, true);
                ESP_LOGW(TAG, "USB TX: %s; reconnecting without retry", esp_err_to_name(err));
            }
        }
        atomic_store(&usb_ready, false);
        atomic_fetch_add(&usb_generation, 1);
        err = cdc_acm_host_close(device);
        ESP_ERROR_CHECK(err);
        ESP_LOGW(TAG, "CDC closed; waiting for reconnect");
        vTaskDelay(pdMS_TO_TICKS(250));
    }
}

static void udp_task(void *context)
{
    struct sockaddr_in peer = {.sin_family = AF_INET, .sin_port = htons(CONFIG_ROV_TOPSIDE_PORT)};
    ESP_ERROR_CHECK(inet_pton(AF_INET, CONFIG_ROV_TOPSIDE_IP, &peer.sin_addr) == 1 ?
                    ESP_OK : ESP_ERR_INVALID_ARG);
    struct sockaddr_in local = {.sin_family = AF_INET, .sin_port = htons(CONFIG_ROV_LOCAL_PORT),
                                .sin_addr.s_addr = htonl(INADDR_ANY)};
    // One extra byte detects oversized datagrams instead of forwarding truncations.
    uint8_t input[UDP_CAPACITY + 1];
    while (1) {
        if (!rov_ethernet_ready()) {
            vTaskDelay(pdMS_TO_TICKS(100));
            continue;
        }
        uint32_t generation = rov_ethernet_generation();
        int sock = socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP);
        if (sock < 0) { vTaskDelay(pdMS_TO_TICKS(1000)); continue; }
        struct timeval timeout = {.tv_usec = 10000};
        if (!rov_bind_ethernet_socket(sock) || setsockopt(sock, SOL_SOCKET, SO_RCVTIMEO, &timeout, sizeof(timeout)) < 0 ||
            setsockopt(sock, SOL_SOCKET, SO_SNDTIMEO, &timeout, sizeof(timeout)) < 0 ||
            bind(sock, (struct sockaddr *)&local, sizeof(local)) < 0) {
            ESP_LOGE(TAG, "UDP setup failed: errno %d", errno);
            close(sock);
            vTaskDelay(pdMS_TO_TICKS(1000));
            continue;
        }
        ESP_LOGI(TAG, "UDP :%d <-> %s:%d; commands %s", CONFIG_ROV_LOCAL_PORT,
                 CONFIG_ROV_TOPSIDE_IP, CONFIG_ROV_TOPSIDE_PORT,
                 CONFIG_ROV_FORWARD_COMMANDS ? "enabled" : "disabled");
        while (rov_ethernet_ready() && generation == rov_ethernet_generation()) {
            struct sockaddr_in source;
            socklen_t source_size = sizeof(source);
            int n = recvfrom(sock, input, sizeof(input), 0, (struct sockaddr *)&source, &source_size);
            if (n > 0) {
                if (n > UDP_CAPACITY || source.sin_addr.s_addr != peer.sin_addr.s_addr ||
                    source.sin_port != peer.sin_port || !rov_mavlink_datagram_valid(input, n)) {
                    atomic_fetch_add(&rejected, 1);
                } else if (CONFIG_ROV_FORWARD_COMMANDS && atomic_load(&usb_ready) &&
                           generation == rov_ethernet_generation()) {
                    rov_framer_t parser = {0};
                    rov_framer_feed(&parser, input, n, enqueue_frame, command_queue);
                } else atomic_fetch_add(&dropped, 1);
            } else if (n < 0 && errno != EAGAIN && errno != EWOULDBLOCK && errno != EINTR) {
                ESP_LOGW(TAG, "UDP receive: errno %d; reopening", errno);
                break;
            }
            // Bound each batch so telemetry never starves incoming commands.
            for (unsigned i = 0; i < 16; ++i) {
                packet_t packet;
                if (xQueueReceive(telemetry_queue, &packet, 0) != pdTRUE) break;
                if (!current_packet(&packet, TELEMETRY_MAX_AGE_US)) {
                    atomic_fetch_add(&dropped, 1);
                    continue;
                }
                if (sendto(sock, packet.data, packet.size, 0,
                           (struct sockaddr *)&peer, sizeof(peer)) == (int)packet.size)
                    atomic_fetch_add(&udp_tx_frames, 1);
                else atomic_fetch_add(&dropped, 1);
            }
        }
        close(sock);
    }
}

void rov_bridge_start(void)
{
    telemetry_queue = xQueueCreate(QUEUE_DEPTH, sizeof(packet_t));
    command_queue = xQueueCreate(QUEUE_DEPTH, sizeof(packet_t));
    ESP_ERROR_CHECK(telemetry_queue && command_queue ? ESP_OK : ESP_ERR_NO_MEM);
    const usb_host_config_t config = {.skip_phy_setup = false, .intr_flags = ESP_INTR_FLAG_LEVEL1};
    ESP_ERROR_CHECK(usb_host_install(&config));
    ESP_ERROR_CHECK(xTaskCreate(usb_library_task, "usb_lib", 4096, NULL, 20, NULL) == pdPASS ?
                    ESP_OK : ESP_ERR_NO_MEM);
    ESP_ERROR_CHECK(cdc_acm_host_install(NULL));
    ESP_ERROR_CHECK(xTaskCreate(usb_device_task, "ark_cdc", 4096, NULL, 5, NULL) == pdPASS ?
                    ESP_OK : ESP_ERR_NO_MEM);
    ESP_ERROR_CHECK(xTaskCreate(udp_task, "mav_udp", 6144, NULL, 5, NULL) == pdPASS ?
                    ESP_OK : ESP_ERR_NO_MEM);
}

void rov_bridge_log_status(void)
{
    ESP_LOGI(TAG, "eth=%d usb=%d usb_rx=%u udp_tx=%u usb_tx=%u dropped=%u rejected=%u heap=%" PRIu32,
             rov_ethernet_ready(), atomic_load(&usb_ready), atomic_load(&usb_rx_frames),
             atomic_load(&udp_tx_frames), atomic_load(&usb_tx_frames), atomic_load(&dropped),
             atomic_load(&rejected), esp_get_free_heap_size());
}

void rov_bridge_status_json(char *buf, size_t size)
{
    snprintf(buf, size, "\"ethernet\":%s,\"usb\":%s,\"usb_rx\":%u,\"udp_tx\":%u,\"usb_tx\":%u,\"dropped\":%u,\"rejected\":%u",
      rov_ethernet_ready() ? "true" : "false", atomic_load(&usb_ready) ? "true" : "false",
      atomic_load(&usb_rx_frames), atomic_load(&udp_tx_frames), atomic_load(&usb_tx_frames),
      atomic_load(&dropped), atomic_load(&rejected));
}

// 0 unknown, 1 disarmed, 2 armed. USB reconnection invalidates old heartbeats.
static int fc_state_snapshot(void)
{
    portENTER_CRITICAL(&heartbeat_mux);
    bool recent = heartbeat_seen && heartbeat_generation == atomic_load(&usb_generation) &&
                  esp_timer_get_time() - heartbeat_us < 3000000LL;
    bool disarmed = heartbeat_disarmed;
    portEXIT_CRITICAL(&heartbeat_mux);
    if (!recent || !atomic_load(&usb_ready) || atomic_load(&usb_fault)) return 0;
    return disarmed ? 1 : 2;
}

const char *rov_bridge_fc_state(void)
{
    int state = fc_state_snapshot();
    return state == 1 ? "disarmed" : state == 2 ? "armed" : "unknown";
}

bool rov_bridge_update_allowed(void) { return fc_state_snapshot() == 1; }
bool rov_bridge_begin_update(void)
{
    bool expected = false;
    if (!atomic_compare_exchange_strong(&update_active, &expected, true)) return false;
    if (rov_bridge_update_allowed()) return true;
    atomic_store(&update_active, false);
    return false;
}
void rov_bridge_end_update(void) { atomic_store(&update_active, false); }
