# ESP32-P4 ROV companion — 1.2.1

I use the Waveshare **ESP32-P4-WIFI6-POE-ETH** as the ROV companion, with firmware written in C using **ESP-IDF 5.5.2**. My board has a P4 revision 1.3, 32 MB flash, 32 MB PSRAM and a C6 Wi-Fi coprocessor. The ARK runs ArduSub and handles motor control.

## Features

- USB CDC-ACM ↔ Ethernet UDP MAVLink transport, bounded queues and reconnect handling.
- Raspberry Pi Camera V2.1 / IMX219 CSI capture, hardware H.264 and RTP/UDP video.
- WPA2 maintenance hotspot, responsive status dashboard, authenticated application updates and boot rollback.

I have checked Ethernet telemetry, camera video, the dashboard and a real OTA update on the bench. My [build notes](../../Documentation/STATUS.md) track those results and the pending propulsion, failsafe and water tests. ROS, backup-power supervision and onboard autonomy are still future work.

## Fresh checkout on Windows

Install/select ESP-IDF 5.5.2, open its PowerShell terminal and enter this project directory. Copy the credential template locally and choose a unique 12–63 character password:

```powershell
Copy-Item main/rov_wifi_credentials.h.example main/rov_wifi_credentials.h
# Edit the new header before building; never commit it.
idf.py -B build-ota set-target esp32p4
idf.py -B build-ota menuconfig
idf.py -B build-ota build
```

Review chip revision and board settings before flashing. The defaults match my pre-v3 bench board and 100 Hz scheduler; do not use `--force` to bypass a revision mismatch. The dependency manager restores packages using `dependencies.lock`. I use the IMX camera component under `components/`, with its original license and upstream revision retained. No sibling checkout or absolute machine path is required.

Initial USB install/recovery, with propulsion disconnected:

```powershell
idf.py -B build-ota -p COM_PORT flash monitor
```

Replace `COM_PORT` with the actual programming port. Exit monitor with Ctrl+]. Close other serial monitors first. The initial flash installs the OTA partition layout; later dashboard uploads use only `build-ota/rov_companion.bin`. Binaries embed the maintenance password and must not be published. See [OTA and rollback](OTA.md).

## Network and camera

- ROV `192.168.137.2/24`; surface `192.168.137.1/24`.
- MAVLink UDP `14555` on the P4 ↔ `14550` on the surface. Only the configured source IP/port is accepted.
- Video RTP/H.264 to surface port `5600`, configured 4 Mbit/s; approximately 25 fps at 1632 × 1232 on the bench. [Video setup](VIDEO.md).
- Hotspot `ROV-Maintenance`; dashboard `http://192.168.4.1`, also `http://192.168.137.2` over Ethernet. [Wi-Fi notes](WIFI.md).
- ARK USB CDC interface 0; the separate SLCAN interface is not MAVLink. My ARK enumerates as `1209:5740`; configurable source defaults accept any single CDC device.

## Source tour

- `main/main.c`: starts services and prints health counters.
- `rov_ethernet.c`: IP101 Ethernet setup and connection generation.
- `rov_bridge.c`: USB reconnects, bounded queues, UDP endpoint filtering and forwarding.
- `mavlink_framing.c`: portable MAVLink frame boundaries; endpoint CRC/signature verification remains the endpoints' job.
- `rov_camera_probe.c`: IMX219 identity check; `rov_video.c`: capture, encoding, RTP and graceful shutdown.
- `rov_maintenance.c` and `dashboard.html`: local status service and UI.
- `rov_heartbeat.c`: validates heartbeat CRC/type and extracts arm state for OTA.
- `rov_ota.c`: upload validation, inactive-slot writing, forwarding lock and startup confirmation.
- `partitions.csv`: factory recovery image, OTA metadata and two 4 MiB application slots.

The bridge does not arm, mix motors or create a fake heartbeat. Source-IP filtering is not authentication. OTA uses a password over local HTTP and image integrity checks; signed firmware / Secure Boot are not enforced. Keep the maintenance network trusted. [OTA.md](OTA.md) explains rollback and health-check limits.

## Host verification

With GCC and Python available:

```powershell
New-Item -ItemType Directory -Force build-host
gcc -std=c11 -Wall -Wextra -Werror -Wno-unused-parameter -I main main/mavlink_framing.c tests/test_framing.c -o build-host/test_framing.exe
./build-host/test_framing.exe
python tests/test_heartbeat.py
```

These tests check framing and heartbeat parsing, not actual power wiring, thruster behavior, pressure tolerance or control-link failsafes. Bench records for video and OTA are in the linked notes.
