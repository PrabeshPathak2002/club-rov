#include "rov_heartbeat.h"

static uint16_t crc_byte(uint16_t crc, uint8_t byte)
{
    uint8_t t = byte ^ (uint8_t)crc;
    t ^= (uint8_t)(t << 4);
    return (crc >> 8) ^ ((uint16_t)t << 8) ^ ((uint16_t)t << 3) ^ (t >> 4);
}

bool rov_heartbeat_disarmed(const uint8_t *f, size_t size, bool *disarmed)
{
    if (!f || !disarmed || size < 3 || (f[0] != 0xfe && f[0] != 0xfd)) return false;
    bool v2 = f[0] == 0xfd;
    size_t h = v2 ? 10 : 6;
    size_t signature = v2 && (f[2] & 1) ? 13 : 0;
    if ((v2 && (f[2] & ~1u)) || size != h + 9 + 2 + signature || f[1] != 9) return false;
    // Component 1 is the autopilot. System ID may be configured by the user.
    if (f[v2 ? 6 : 4] != 1 || f[v2 ? 7 : 5] != 0 ||
        (v2 && (f[8] != 0 || f[9] != 0))) return false;
    const uint8_t *p = f + h;
    if (p[4] != 12 || p[5] != 3 || p[8] != 3) return false; // submarine, ArduPilot, MAVLink 3
    uint16_t crc = 0xffff;
    for (size_t i = 1; i < h + 9; ++i) crc = crc_byte(crc, f[i]);
    crc = crc_byte(crc, 50); // HEARTBEAT CRC extra from MAVLink common.xml
    if (f[h+9] != (uint8_t)crc || f[h+10] != (uint8_t)(crc >> 8)) return false;
    *disarmed = !(p[6] & 128);
    return true;
}
