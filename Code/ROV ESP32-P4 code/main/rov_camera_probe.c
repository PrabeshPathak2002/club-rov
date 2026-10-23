#include "driver/i2c_master.h"
#include "esp_log.h"
#include "sdkconfig.h"

// Waveshare ESP32-P4-WIFI6-POE-ETH J3: SDA GPIO7, SCL GPIO8.
// This reads identification only; it does not configure or start CSI streaming.
void rov_camera_probe(void)
{
#if CONFIG_ROV_CAMERA_PROBE
    const char *tag = "rov_camera";
    i2c_master_bus_handle_t bus = NULL;
    i2c_master_dev_handle_t sensor = NULL;
    const i2c_master_bus_config_t bus_cfg = {
        .i2c_port = I2C_NUM_0,
        .sda_io_num = GPIO_NUM_7,
        .scl_io_num = GPIO_NUM_8,
        .clk_source = I2C_CLK_SRC_DEFAULT,
        .glitch_ignore_cnt = 7,
        .flags.enable_internal_pullup = true,
    };
    esp_err_t err = i2c_new_master_bus(&bus_cfg, &bus);
    if (err != ESP_OK) {
        ESP_LOGW(tag, "Camera probe skipped: I2C bus: %s", esp_err_to_name(err));
        return;
    }
    const i2c_device_config_t dev_cfg = {
        .dev_addr_length = I2C_ADDR_BIT_LEN_7,
        .device_address = 0x10,
        .scl_speed_hz = 100000,
    };
    err = i2c_master_bus_add_device(bus, &dev_cfg, &sensor);
    if (err == ESP_OK) {
        const uint8_t reg[] = {0x00, 0x00};
        uint8_t id[2] = {0};
        err = i2c_master_transmit_receive(sensor, reg, sizeof(reg), id, sizeof(id), 100);
        if (err == ESP_OK) {
            unsigned chip_id = ((unsigned)id[0] << 8) | id[1];
            if (chip_id == 0x0219) {
                ESP_LOGI(tag, "IMX219 detected: address=0x10 chip_id=0x%04x; video not started", chip_id);
            } else {
                ESP_LOGW(tag, "Unexpected camera ID 0x%04x (expected 0x0219)", chip_id);
            }
        } else {
            ESP_LOGW(tag, "IMX219 ID read failed: %s; telemetry continues", esp_err_to_name(err));
        }
        esp_err_t cleanup = i2c_master_bus_rm_device(sensor);
        if (cleanup != ESP_OK) {
            ESP_LOGW(tag, "Camera device cleanup: %s", esp_err_to_name(cleanup));
            return;
        }
    } else {
        ESP_LOGW(tag, "Camera device setup: %s", esp_err_to_name(err));
    }
    err = i2c_del_master_bus(bus);
    if (err != ESP_OK) ESP_LOGW(tag, "Camera bus cleanup: %s", esp_err_to_name(err));
#endif
}
