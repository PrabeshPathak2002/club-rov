#pragma once
#include <stdbool.h>
#include <stdint.h>
#include <stddef.h>

void rov_ethernet_start(void);
void rov_ethernet_stop_for_restart(void);
bool rov_video_stop_for_restart(void);
bool rov_ethernet_ready(void);
uint32_t rov_ethernet_generation(void);
void rov_bridge_start(void);
bool rov_bridge_update_allowed(void);
bool rov_bridge_begin_update(void);
void rov_bridge_end_update(void);
const char *rov_bridge_fc_state(void);
void rov_bridge_log_status(void);
void rov_maintenance_start(void);
void rov_bridge_status_json(char *buf, size_t size);
void rov_video_status_json(char *buf, size_t size);
bool rov_bind_ethernet_socket(int sock);
