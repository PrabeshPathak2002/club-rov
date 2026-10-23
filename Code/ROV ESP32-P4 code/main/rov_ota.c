#include <stdatomic.h>
#include <string.h>
#include <stdlib.h>
#include "esp_app_desc.h"
#include "esp_app_format.h"
#include "esp_ota_ops.h"
#include "esp_timer.h"
#include "esp_log.h"
#include "esp_system.h"
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "rov.h"
#include "rov_ota.h"
#include "rov_wifi_credentials.h"

static const char *TAG = "rov_ota";
static atomic_bool server_ready, reboot_pending;

static esp_err_t reply(httpd_req_t *req, const char *status, const char *message)
{
    httpd_resp_set_status(req, status);
    httpd_resp_set_type(req, "text/plain");
    httpd_resp_set_hdr(req, "Cache-Control", "no-store");
    httpd_resp_set_hdr(req, "Connection", "close");
    esp_err_t sent = httpd_resp_send(req, message, HTTPD_RESP_USE_STRLEN);
    // Let ESP-IDF drain any unread body after the response; an immediate close can reset TCP before the client receives the error.
    return sent;
}

static bool authorized(httpd_req_t *req)
{
    char key[64] = {0};
    size_t n = httpd_req_get_hdr_value_len(req, "X-ROV-Update-Key");
    if (n != sizeof(ROV_WIFI_PASSWORD)-1 || n >= sizeof(key) ||
        httpd_req_get_hdr_value_str(req, "X-ROV-Update-Key", key, sizeof(key)) != ESP_OK) return false;
    unsigned diff = 0;
    for (size_t i = 0; i < sizeof(ROV_WIFI_PASSWORD)-1; ++i) diff |= (unsigned char)key[i] ^ ROV_WIFI_PASSWORD[i];
    memset(key, 0, sizeof(key));
    return diff == 0;
}

static void reboot_task(void *arg)
{
    vTaskDelay(pdMS_TO_TICKS(2000));
    esp_restart();
}

esp_err_t rov_ota_upload(httpd_req_t *req)
{
    if (!authorized(req)) return reply(req, "401 Unauthorized", "Incorrect maintenance password.");
    if (atomic_load(&reboot_pending)) return reply(req, "409 Conflict", "Restart already scheduled.");
    esp_ota_img_states_t state;
    const esp_partition_t *running = esp_ota_get_running_partition();
    if (esp_ota_get_state_partition(running, &state) == ESP_OK && state == ESP_OTA_IMG_PENDING_VERIFY)
        return reply(req, "409 Conflict", "Startup checks are still running. Wait one minute.");
    const esp_partition_t *target = esp_ota_get_next_update_partition(NULL);
    const size_t prefix = sizeof(esp_image_header_t) + sizeof(esp_image_segment_header_t) + sizeof(esp_app_desc_t);
    if (!target || req->content_len < prefix || req->content_len > target->size)
        return reply(req, "413 Content Too Large", "Choose a complete rov_companion.bin application image, at most 4 MB.");
    if (!rov_bridge_begin_update())
        return reply(req, "409 Conflict", "Update locked: connect the flight controller and disarm it. A recent ArduSub heartbeat is required.");
    // Drain any USB command write already in flight (the bridge bounds it to 50 ms).
    vTaskDelay(pdMS_TO_TICKS(100));
    esp_ota_handle_t handle = 0;
    bool opened = false;
    const char *failure = "Upload failed. Previous firmware remains selected; reconnect and retry.";
    uint8_t *buf = malloc(4096);
    if (!buf) goto failed;
    size_t received = 0;
    int64_t deadline = esp_timer_get_time() + 180000000LL;
    // Receive enough header bytes to reject another application before erasing flash.
    while (received < prefix) {
        if (!rov_bridge_update_allowed() || esp_timer_get_time() > deadline) goto failed;
        int n = httpd_req_recv(req, (char *)buf + received, prefix-received);
        if (n <= 0) goto failed;
        received += n;
    }
    esp_image_header_t header;
    esp_app_desc_t app;
    memcpy(&header, buf, sizeof(header));
    memcpy(&app, buf+sizeof(header)+sizeof(esp_image_segment_header_t), sizeof(app));
    if (header.magic != ESP_IMAGE_HEADER_MAGIC || header.chip_id != ESP_CHIP_ID_ESP32P4 ||
        app.magic_word != ESP_APP_DESC_MAGIC_WORD ||
        memcmp(app.project_name, esp_app_get_description()->project_name, sizeof(app.project_name)) != 0) {
        failure = "Wrong firmware: upload the ESP32-P4 rov_companion.bin application, not a merged image or bootloader.";
        goto failed;
    }
    if (!rov_bridge_update_allowed()) goto failed;
    esp_err_t err = esp_ota_begin(target, req->content_len, &handle);
    if (err != ESP_OK) { ESP_LOGE(TAG, "OTA begin: %s", esp_err_to_name(err)); goto failed; }
    opened = true;
    if (esp_ota_write(handle, buf, received) != ESP_OK) goto failed;
    while (received < req->content_len) {
        if (!rov_bridge_update_allowed() || esp_timer_get_time() > deadline) {
            failure = "Update cancelled: controller state became armed or unknown, or upload timed out.";
            goto failed;
        }
        size_t count = req->content_len - received;
        if (count > 4096) count = 4096;
        int n = httpd_req_recv(req, (char *)buf, count);
        if (n <= 0 || esp_ota_write(handle, buf, n) != ESP_OK) goto failed;
        received += n;
        vTaskDelay(1); // Keep the bridge and USB tasks responsive during upload.
    }
    if (!rov_bridge_update_allowed()) goto failed;
    err = esp_ota_end(handle); // Chip revision, image structure and checksum/hash validation.
    opened = false;
    if (err != ESP_OK) { failure = "Firmware validation failed. Previous firmware remains selected."; goto failed; }
    if (!rov_bridge_update_allowed() || esp_ota_set_boot_partition(target) != ESP_OK) goto failed;
    free(buf);
    atomic_store(&reboot_pending, true);
    ESP_LOGI(TAG, "Validated %u bytes; next boot: %s", (unsigned)received, target->label);
    // Keep commands inhibited through the restart and leave time to deliver HTTP success.
    if (xTaskCreate(reboot_task, "ota_reboot", 3072, NULL, 2, NULL) != pdPASS) {
        esp_ota_set_boot_partition(running);
        atomic_store(&reboot_pending, false);
        rov_bridge_end_update();
        return reply(req, "500 Internal Server Error", "Could not schedule restart; retry the update.");
    }
    return reply(req, "200 OK", "Firmware verified. Restarting; reconnect to ROV-Maintenance in about 20 seconds. Startup checks take one minute.");
failed:
    if (opened) esp_ota_abort(handle);
    free(buf);
    rov_bridge_end_update();
    ESP_LOGW(TAG, "%s", failure);
    return reply(req, "400 Bad Request", failure);
}

static void verify_boot_task(void *arg)
{
    esp_ota_img_states_t state;
    const esp_partition_t *running = esp_ota_get_running_partition();
    if (esp_ota_get_state_partition(running, &state) != ESP_OK || state != ESP_OTA_IMG_PENDING_VERIFY) {
        vTaskDelete(NULL);
        return;
    }
    // Confirm only after a stable minute and the maintenance HTTP service starts.
    for (int elapsed = 0; elapsed < 120; ++elapsed) {
        vTaskDelay(pdMS_TO_TICKS(1000));
        if (elapsed >= 59 && atomic_load(&server_ready) && esp_get_free_heap_size() > 128*1024) {
            esp_err_t err = esp_ota_mark_app_valid_cancel_rollback();
            if (err == ESP_OK) { ESP_LOGI(TAG, "Startup check passed; update confirmed"); vTaskDelete(NULL); return; }
        }
    }
    ESP_LOGE(TAG, "Startup check failed; rolling back");
    esp_ota_mark_app_invalid_rollback_and_reboot();
    esp_restart();
}

void rov_ota_server_ready(void) { atomic_store(&server_ready, true); }
void rov_ota_boot_check_start(void)
{
    if (xTaskCreate(verify_boot_task, "ota_verify", 4096, NULL, 2, NULL) != pdPASS)
        ESP_LOGE(TAG, "Could not create startup verifier; pending update will roll back on next restart");
}

const char *rov_ota_boot_state(void)
{
    esp_ota_img_states_t state;
    if (atomic_load(&reboot_pending)) return "restarting";
    if (esp_ota_get_state_partition(esp_ota_get_running_partition(), &state) == ESP_OK && state == ESP_OTA_IMG_PENDING_VERIFY)
        return "checking";
    return "ready";
}
