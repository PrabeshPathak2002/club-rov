#pragma once
#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

#define ROV_MAVLINK_MAX_FRAME 280
typedef struct {
    uint8_t data[ROV_MAVLINK_MAX_FRAME];
    size_t used;
    size_t expected;
} rov_framer_t;
typedef void (*rov_frame_fn)(const uint8_t *, size_t, void *);
void rov_framer_feed(rov_framer_t *parser, const uint8_t *data, size_t size,
                     rov_frame_fn callback, void *context);
// Structural validation only; preserve CRC/signature bytes for the endpoints.
bool rov_mavlink_datagram_valid(const uint8_t *data, size_t size);
