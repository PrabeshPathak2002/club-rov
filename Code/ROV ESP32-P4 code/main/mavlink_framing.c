#include "mavlink_framing.h"

static size_t frame_size(const uint8_t *p)
{
    return p[0] == 0xfe ? (size_t)p[1] + 8 :
           (size_t)p[1] + 12 + ((p[2] & 1) ? 13 : 0);
}

void rov_framer_feed(rov_framer_t *p, const uint8_t *data, size_t size,
                     rov_frame_fn callback, void *context)
{
    for (size_t i = 0; i < size; ++i) {
        uint8_t byte = data[i];
        if (p->used == 0 && byte != 0xfe && byte != 0xfd) {
            continue;
        }
        p->data[p->used++] = byte;
        if (p->used == 3) {
            if (p->data[0] == 0xfd && (p->data[2] & ~1u)) {
                p->used = p->expected = 0;
                continue;
            }
            p->expected = frame_size(p->data);
        }
        if (p->expected && p->used == p->expected) {
            callback(p->data, p->used, context);
            p->used = p->expected = 0;
        }
    }
}

bool rov_mavlink_datagram_valid(const uint8_t *data, size_t size)
{
    if (!size) return false;
    while (size) {
        if (size < 3 || (data[0] != 0xfe && data[0] != 0xfd)) return false;
        if (data[0] == 0xfd && (data[2] & ~1u)) return false;
        size_t n = frame_size(data);
        if (n > size) return false;
        data += n;
        size -= n;
    }
    return true;
}
