#include "mavlink_framing.h"
#include <assert.h>
#include <stdio.h>
#include <string.h>

static uint8_t expected[ROV_MAVLINK_MAX_FRAME];
static size_t expected_size, count;
static void received(const uint8_t *data, size_t size, void *context)
{
    assert(size == expected_size);
    assert(memcmp(data, expected, size) == 0);
    ++count;
}

int main(void)
{
    // Every payload length, v1/v2/signed-v2, every possible USB split point.
    // Embedded magic bytes and signature bytes must survive without re-encoding.
    for (unsigned version = 0; version < 3; ++version) {
        for (unsigned payload = 0; payload <= 255; ++payload) {
            expected_size = payload + (version == 0 ? 8 : version == 1 ? 12 : 25);
            for (size_t i = 0; i < expected_size; ++i) expected[i] = (uint8_t)(i * 17);
            expected[0] = version == 0 ? 0xfe : 0xfd;
            expected[1] = payload;
            expected[2] = version == 2 ? 1 : 0;
            assert(rov_mavlink_datagram_valid(expected, expected_size));
            assert(!rov_mavlink_datagram_valid(expected, expected_size - 1));
            for (size_t split = 0; split <= expected_size; ++split) {
                rov_framer_t p = {0};
                size_t before = count;
                rov_framer_feed(&p, expected, split, received, NULL);
                rov_framer_feed(&p, expected + split, expected_size - split, received, NULL);
                assert(count == before + 1 && p.used == 0);
            }
            rov_framer_t p = {0};
            for (size_t i = 0; i < expected_size; ++i)
                rov_framer_feed(&p, expected + i, 1, received, NULL);
        }
    }
    uint8_t combined[560];
    memcpy(combined, expected, expected_size);
    memcpy(combined + expected_size, expected, expected_size);
    assert(rov_mavlink_datagram_valid(combined, sizeof(combined)));
    rov_framer_t p = {0};
    size_t before = count;
    rov_framer_feed(&p, combined, sizeof(combined), received, NULL);
    assert(count == before + 2);
    assert(!rov_mavlink_datagram_valid(combined, sizeof(combined) - 1));
    assert(!rov_mavlink_datagram_valid(combined, 0));
    uint8_t junk[] = {0, 1, 2, 0xfd, 0, 0x80};
    rov_framer_feed(&p, junk, sizeof(junk), received, NULL);
    rov_framer_feed(&p, expected, expected_size, received, NULL);
    assert(count == before + 3);
    expected[2] = 2;
    assert(!rov_mavlink_datagram_valid(expected, expected_size));
    printf("PASS: %zu exact frame deliveries; fragmentation, signed frames, bounds and malformed datagrams\n", count);
    return 0;
}
