# Camera streaming

The initial ROV uses a Raspberry Pi Camera V2.1 / IMX219. The ESP32-P4 captures it over CSI, passes the image through the ISP, encodes H.264 in hardware and sends RTP/UDP video to the surface. MAVLink uses a separate USB/Ethernet bridge.

## QGroundControl setup

- Surface Ethernet address: `192.168.137.1/24`.
- ESP32 address: `192.168.137.2/24`.
- Video source: **UDP h.264 Video Stream**.
- Receive port: **5600**, or `0.0.0.0:5600` if QGC asks for a URL.
- Disable **Disabled When Disarmed** for bench viewing.
- Aspect ratio: **0.0 / automatic**. The source is 1632 × 1232 rather than 16:9.
- MAVLink ports: 14550 on the surface and 14555 on the ESP32.

The stream uses RTP payload type 96, a 90 kHz clock and RFC 6184 single-NAL/FU-A packetization. Packets stay below the Ethernet MTU. The encoder targets 30 fps and 4 Mbit/s, with an IDR approximately every 30 encoded frames. A short bench measurement recorded approximately 25 fps.

## Build and image format

Complete the [fresh-checkout setup](README.md#fresh-checkout-on-windows), including the local credential header and target selection. Then build from this firmware directory in an ESP-IDF 5.5.2 terminal:

```powershell
idf.py -B build-ota build
idf.py -B build-ota -p COM_PORT flash
```

Use the actual programming port in place of `COM_PORT`. See [OTA.md](OTA.md) for the current partition layout, credential setup and update procedure.

The sensor mode crops eight columns from the binned 1640-pixel image, giving a 1632-pixel stride divisible by 16. Changing only the encoder width would corrupt the image. The bench configuration uses PSRAM at 200 MHz, a 256 KB L2 cache and 128-byte cache lines. The board has 32 MB flash; the partition table retains the original NVS/PHY offsets, a factory recovery image and two 4 MiB OTA slots.

Video runs in a priority-3 task alongside the telemetry tasks. Camera errors stop the video task. Longer fault and endurance tests are required before underwater operation.

## Dependencies and credit

- ESP-IDF 5.5.2.
- Espressif `esp_video` 2.4.x and `esp_h264` 1.3.x, pinned by `dependencies.lock`.
- [mushBrainDave/esp32-p4-imx-camera](https://github.com/mushBrainDave/esp32-p4-imx-camera), `esp_cam_sensor_imx` 0.4.0, Apache-2.0, revision `9a3f8ca28e93526e0854ba45ad3b461b33ef2dac`. Its license and revision are retained alongside the component.

The capture and encoder setup is adapted from that driver's `imx219_video` example. The ROV firmware adds the RTP transport and combines it with the telemetry bridge.

## Bench notes — 20 October 2026

The build and flash completed on the P4 revision 1.3 using ESP-IDF 5.5.2. PSRAM initialized at 200 MHz and passed its startup test. The camera identified as IMX219 and started 1632 × 1232 RAW10 capture followed by H.264 RTP streaming.

Between log timestamps 107452 and 132542 ms, encoded/sent frames rose from 2504 to 3131, about 25.0 fps. Video errors and send drops stayed at zero. Ethernet and USB remained connected, telemetry/command counters increased, the boot-time drop counter stayed at 93 and free heap stayed at 21,125,064 bytes.

Video was visible in QGroundControl on a separate receiving laptop. No decoded frame or screenshot was saved from that laptop during this check. A UDP capture on the programming computer received no packets because the Ethernet stream was going to the other laptop; that capture was not a decoder test. Glass-to-glass latency and endurance testing remain incomplete.

Eight host checks of the C packetizer passed, covering single-NAL/FU-A boundaries, exact reconstruction, MTU bounds, marker placement, timestamps and sequence rollover. The original capture helper, logs and earlier telemetry/camera backups are in the private working-history archive. Those old backups predate the current OTA layout and should not be flashed over it without reviewing the bootloader and partition table.

On Windows, the CMake setup stages the ISP tuning JSON in a deterministic temporary directory without spaces to work around `esp_ipa` 2.3's path splitter. This avoids modifying downloaded component sources.
