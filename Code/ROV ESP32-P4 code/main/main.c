#include "rov.h"
#include "rov_ota.h"
#include "esp_log.h"
#include "esp_system.h"
#include "esp_chip_info.h"
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"

void rov_camera_probe(void);
void rov_video_start(void);

static void prepare_restart(void)
{
    if (!rov_video_stop_for_restart()) ESP_LOGW("rov", "Video shutdown timed out");
    rov_ethernet_stop_for_restart();
}

void app_main(void)
{
    esp_chip_info_t chip;
    esp_chip_info(&chip);
    ESP_LOGI("rov", "Club ROV companion; IDF %s; chip revision %u",
             esp_get_idf_version(), chip.revision);
    rov_ethernet_start();
    rov_bridge_start();
    ESP_ERROR_CHECK(esp_register_shutdown_handler(prepare_restart));
    rov_ota_boot_check_start();
    rov_camera_probe();
    rov_video_start();
    rov_maintenance_start();
    while (1) {
        vTaskDelay(pdMS_TO_TICKS(5000));
        rov_bridge_log_status();
    }
}
