#pragma once
#include <stdbool.h>
#include <stdint.h>
#include <stddef.h>

// Only CRC-valid ArduSub autopilot heartbeats on the controller USB link qualify.
bool rov_heartbeat_disarmed(const uint8_t *frame, size_t size, bool *disarmed);
