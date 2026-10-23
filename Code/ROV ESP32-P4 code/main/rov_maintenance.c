#include <stdio.h>
#include <string.h>
#include "esp_wifi.h"
#include "esp_netif.h"
#include "esp_http_server.h"
#include "esp_timer.h"
#include "esp_system.h"
#include "esp_log.h"
#include "nvs_flash.h"
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "rov.h"
#include "rov_ota.h"
#include "esp_app_desc.h"
#include "esp_ota_ops.h"
#include "rov_wifi_credentials.h"

static const char *TAG = "rov_maintenance";
extern const char page[] asm("_binary_dashboard_html_start");

static esp_err_t root_handler(httpd_req_t *req)
{
    httpd_resp_set_type(req, "text/html");
    httpd_resp_set_hdr(req, "Cache-Control", "no-store");
    return httpd_resp_send(req, page, HTTPD_RESP_USE_STRLEN);
}

static esp_err_t status_handler(httpd_req_t *req)
{
    char bridge[320], video[200], response[1000];
    rov_bridge_status_json(bridge, sizeof(bridge));
    rov_video_status_json(video, sizeof(video));
    snprintf(response, sizeof(response), "{\"uptime_s\":%lld,\"heap\":%lu,\"version\":\"%s\",\"partition\":\"%s\",\"boot_state\":\"%s\",\"fc_state\":\"%s\",\"update_allowed\":%s,%s,%s}",
             (long long)(esp_timer_get_time()/1000000), (unsigned long)esp_get_free_heap_size(),
             esp_app_get_description()->version, esp_ota_get_running_partition()->label,
             rov_ota_boot_state(), rov_bridge_fc_state(), rov_bridge_update_allowed() ? "true" : "false", bridge, video);
    httpd_resp_set_type(req, "application/json");
    httpd_resp_set_hdr(req, "Cache-Control", "no-store");
    return httpd_resp_send(req, response, HTTPD_RESP_USE_STRLEN);
}

static void maintenance_task(void *arg)
{
    vTaskDelay(pdMS_TO_TICKS(7000));
    esp_err_t err = nvs_flash_init();
    // Never erase existing NVS automatically to start maintenance.
    if (err != ESP_OK) goto failed;
    esp_netif_t *ap = esp_netif_create_default_wifi_ap();
    if (!ap) { err = ESP_ERR_NO_MEM; goto failed; }
    wifi_init_config_t init = WIFI_INIT_CONFIG_DEFAULT();
    err = esp_wifi_init(&init);
    if (err != ESP_OK) goto failed;
    err = esp_wifi_set_storage(WIFI_STORAGE_RAM);
    if (err != ESP_OK) goto failed;
    wifi_config_t config = {.ap = {
        .ssid = "ROV-Maintenance",
        .ssid_len = sizeof("ROV-Maintenance") - 1,
        .channel = 6, .max_connection = 3,
        .authmode = WIFI_AUTH_WPA2_PSK,
        .pmf_cfg = {.required = false},
    }};
    _Static_assert(sizeof(ROV_WIFI_PASSWORD) >= 13 && sizeof(ROV_WIFI_PASSWORD) <= 64,
                   "Wi-Fi password must be 12-63 characters");
    memcpy(config.ap.password, ROV_WIFI_PASSWORD, sizeof(ROV_WIFI_PASSWORD));
    err = esp_wifi_set_mode(WIFI_MODE_AP);
    if (err != ESP_OK) goto failed;
    err = esp_wifi_set_config(WIFI_IF_AP, &config);
    if (err != ESP_OK) goto failed;
    err = esp_wifi_start();
    if (err != ESP_OK) goto failed;
    httpd_config_t hc = HTTPD_DEFAULT_CONFIG();
    hc.task_priority = 2;
    hc.stack_size = 8192;
    hc.recv_wait_timeout = 5;
    hc.send_wait_timeout = 5;
    hc.max_open_sockets = 3;
    hc.lru_purge_enable = true;
    httpd_handle_t server = NULL;
    err = httpd_start(&server, &hc);
    if (err != ESP_OK) goto failed;
    const httpd_uri_t root = {.uri = "/", .method = HTTP_GET, .handler = root_handler};
    const httpd_uri_t status = {.uri = "/api/status", .method = HTTP_GET, .handler = status_handler};
    const httpd_uri_t upload = {.uri = "/api/firmware", .method = HTTP_POST, .handler = rov_ota_upload};
    err = httpd_register_uri_handler(server, &root);
    if (err == ESP_OK) err = httpd_register_uri_handler(server, &status);
    if (err == ESP_OK) err = httpd_register_uri_handler(server, &upload);
    if (err != ESP_OK) { httpd_stop(server); goto failed; }
    rov_ota_server_ready();
    ESP_LOGI(TAG, "WPA2 hotspot ROV-Maintenance ready: http://192.168.4.1 (dashboard + authenticated firmware update)");
    vTaskDelete(NULL);
    return;
failed:
    ESP_LOGE(TAG, "Maintenance unavailable: %s; telemetry/video remain active", esp_err_to_name(err));
    vTaskDelete(NULL);
}

void rov_maintenance_start(void)
{
    if (xTaskCreate(maintenance_task, "maintenance", 6144, NULL, 2, NULL) != pdPASS)
        ESP_LOGE(TAG, "Could not create maintenance task");
}
