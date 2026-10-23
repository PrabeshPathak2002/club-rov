#pragma once
#include "esp_http_server.h"
esp_err_t rov_ota_upload(httpd_req_t *req);
void rov_ota_boot_check_start(void);
void rov_ota_server_ready(void);
const char *rov_ota_boot_state(void);
