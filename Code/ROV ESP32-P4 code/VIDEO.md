# ROV camera streaming

The combined firmware captures the Raspberry Pi Camera v2.1 (IMX219), uses the
ESP32-P4 ISP to produce the packed YUV420 format, encodes H.264 in hardware,
and sends RTP/UDP to the configured surface IP. MAVLink remains on its separate
USB/Ethernet bridge.

## QGroundControl

- Surface Ethernet address: `192.168.137.1/24`.
- ESP32 address: `192.168.137.2/24`.
- Video source: **UDP h.264 Video Stream**.
- Receive port: **5600** (or `0.0.0.0:5600` if the QGC version asks for a URL).
- Disable **Disabled When Disarmed** for a bench test.
- Aspect ratio: **0.0 / automatic**; the source is 1632 × 1232, not 16:9.
- MAVLink uses UDP 14550 on the surface and 14555 on the ESP32.

The stream uses RTP payload type 96, a 90 kHz timestamp clock, and RFC 6184
single-NAL/FU-A packetization. Datagram payloads stay below the Ethernet MTU.
The encoder targets 30 fps, 4 Mbit/s, with an IDR approximately every 30
encoded frames. Actual frame rate must be measured on hardware.

## Firmware

Build from this directory in the consolidated checkout. [OTA.md](OTA.md) records the current partition layout and build procedure.

From its ESP-IDF 5.5.2 terminal:

```powershell
idf.py -B build-ota build
idf.py -B build-ota -p COM_PORT flash
```

The sensor mode crops eight columns from the binned 1640-pixel image at the
sensor, yielding a 1632-pixel stride divisible by 16. Changing only the encoder
width would corrupt the image. PSRAM is enabled at 200 MHz with a 256 KB L2
cache and 128-byte cache lines. Flash capacity was read as 32 MB; the custom
partition table retains the original NVS/PHY offsets, a factory image and two 4 MiB OTA slots.

Video initialization and processing run in a priority-3 task. The USB/UDP
telemetry tasks are retained. Camera errors stop the video task rather than
intentionally rebooting the board. Hardware crashes are still possible during
bring-up; this firmware is not a validated underwater operational release.

## Dependencies and attribution

- ESP-IDF 5.5.2.
- Espressif `esp_video` 2.4.x, `esp_h264` 1.3.x; exact versions are recorded in
  the firmware's `dependencies.lock`.
- Vendored `esp_cam_sensor_imx` 0.4.0, Apache-2.0:
  https://github.com/mushBrainDave/esp32-p4-imx-camera
  commit `9a3f8ca28e93526e0854ba45ad3b461b33ef2dac`.
  Its license and source revision are retained in the component directory.
- Capture and encoder setup are adapted from the driver's `imx219_video`
  example. The RTP transport is specific to this ROV firmware.

## Diagnostics and rollback

`capture_rtp.py` collects a bounded sample on UDP 5600 and reconstructs an
Annex-B H.264 file for decoder checks. Close the QGC video receiver first:
only one receiver should bind that port during the diagnostic test.

`telemetry-working-backup` contains the earlier working telemetry binary,
bootloader, partition table, flash arguments and source configuration.
`camera-probe-working-backup` contains the state before video integration.
These are recovery artifacts, not an automated rollback partition.

Validation results will be recorded after the hardware run. A successful
compile alone does not establish camera capture or video decoding.

## Hardware validation — 2026-10-20

Build and flash succeeded on COM8, ESP32-P4 revision 1.3, ESP-IDF 5.5.2.
PSRAM initialized at 200 MHz and passed its startup memory test. The camera
reported IMX219 and 1632x1232 RAW10, followed by H264 RTP streaming.
Between timestamps 107452 and 132542 ms, encoded/sent frames increased from
2504 to 3131: approximately 25.0 fps. Video errors and network send drops
stayed at zero. Telemetry remained eth=1, usb=1; RX, TX and command counters
increased, the cumulative boot-time drop count stayed at 93, and reported
free heap stayed at 21125064 bytes. This establishes a short bench check,
not endurance, underwater qualification, or measured glass-to-glass latency.

The receiving QGroundControl is on a separate laptop; this programming PC's
Ethernet is disconnected. A local UDP capture therefore received no packets
and was not a video-decoder validation. The user answered yes when asked
whether video was visible on the receiving laptop. No independent screenshot
or decoded frame from that laptop was collected.

Eight host tests of the actual C packetizer passed: single NAL and FU-A
boundary sizes, exact NAL reconstruction, MTU bound, RTP marker placement,
timestamp, and sequence-number rollover.

On Windows, the project CMake stages the ISP tuning JSON into a deterministic
TEMP subdirectory without spaces to work around esp_ipa 2.3's path splitter.
No changes were made to the managed component source for this workaround.
