idf_component_register(SRCS "main.c" "rov_ota.c" "rov_heartbeat.c" "rov_maintenance.c" "rov_video.c" "rov_camera_probe.c" "rov_ethernet.c" "rov_bridge.c" "mavlink_framing.c"
                    INCLUDE_DIRS "."
                    EMBED_TXTFILES "dashboard.html"
                    REQUIRES app_update esp_app_format esp_wifi esp_http_server nvs_flash esp_video esp_cam_sensor_imx esp_h264 esp_driver_i2c esp_eth esp_netif esp_event esp_timer usb lwip)
