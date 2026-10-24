# Wi-Fi firmware updates

OTA updates install companion firmware through the maintenance dashboard. The update writes an inactive application slot and keeps a recovery path if the new application fails its startup checks.

## Update from the dashboard

1. Recover the ROV and disarm the flight controller. Keep power stable.
2. Connect to **ROV-Maintenance** and open **http://192.168.4.1**. The same
   dashboard is available over the tether at http://192.168.137.2.
3. Confirm the controller shows **Disarmed**. After an OTA reboot, allow one
   minute for startup checks before attempting another update.
4. Under **Update the companion**, choose the newly built **rov_companion.bin**,
   enter the maintenance Wi-Fi password, acknowledge the restart, and upload.
5. Wait for **Firmware verified**. The P4 restarts after two seconds. Reconnect
   to the hotspot if necessary and reload after about 20 seconds. Verify the
   firmware version and that QGroundControl telemetry and video return.

Upload the application binary only: not a bootloader, partition table, merged
flash image, FC firmware or C6 firmware. Maximum application size is 4 MiB.
The initial USB installation is already necessary to establish the OTA layout.

## Update behavior

The status page is public on the local Ethernet/hotspot networks. Uploads require
the maintenance password in an HTTP request header. The hotspot uses WPA2;
the web interface uses plain HTTP, so use a trusted tether/network. Firmware
images are checked for ESP32-P4 chip identity, project name, compatible revision,
image structure and checksum/hash. This build does not enforce signed firmware
or Secure Boot: the password controls who can install a compatible image.

A valid, CRC-checked ArduSub autopilot HEARTBEAT must arrive over USB within the
last three seconds and report disarmed. Heartbeats from other MAVLink component
types, corrupted messages and heartbeats from before a USB reconnect do not
unlock updates. Heartbeat signing is not authenticated by this bridge; the USB
controller link is the trusted source. Unknown or armed state rejects an update.

During an upload, command forwarding is inhibited. Any USB command already in
flight is given time to finish before flash writing begins. Controller state is
checked throughout the transfer and before selecting the next boot image.
If it becomes armed or stale, the update is aborted. Telemetry and video continue
during upload where resources permit, and briefly disconnect during reboot.
The updater neither arms nor disarms the vehicle itself.

An interrupted, incorrect or corrupt upload leaves the previous boot image
selected. A complete valid image is written to an inactive slot and selected
only after validation. Keep the firmware password header across builds so
future images retain your credentials.

## Partitions and rollback

The bench board has 32 MB flash. NVS and PHY data retain their original
offsets. The layout preserves a 4 MiB factory recovery app at 0x10000,
adds OTA metadata at 0x410000, and provides 4 MiB app slots at 0x420000
and 0x820000. Updates alternate between the OTA slots; they do not overwrite
the factory image. The USB-flashed factory app includes the updater.

ESP-IDF bootloader rollback is enabled. A new OTA app stays pending until it
has run for a minute, started the HTTP maintenance service, and retained at
least 128 KiB free heap. A reset/crash before confirmation causes bootloader
rollback. Failure to satisfy these checks within two minutes requests rollback.
These startup checks are not a full camera, controller or vehicle health test.
After confirmation, later failures do not automatically roll back.

## Building and recovery

Use ESP-IDF 5.5.2 and the committed dependency lockfile. On a fresh checkout,
copy main/rov_wifi_credentials.h.example to main/rov_wifi_credentials.h and
set your own password. The populated header and binary contain credentials
and must stay out of the public repository.

```
idf.py -B build-ota build
```

Upload **build-ota/rov_companion.bin** through the dashboard. For initial USB
installation or recovery, with the ROV recovered and disarmed:

```
idf.py -B build-ota -p COM_PORT flash
```

USB flashing also writes the initial OTA metadata, selecting the factory image.
It does not erase NVS. Restore the known-good source/partition layout before
flashing an older backup; do not mix old bootloaders and new partition tables.

## Dashboard

All styling, icons and scripts are served by the P4; no Internet or external
assets are needed. The responsive yellow/dark interface shows tether and USB
state, fresh controller arm state, uptime, memory, counters, measured video rate,
firmware version, running partition and startup-check state. Video rate uses
differences between samples rather than a configured target. Lost connections
mark data stale and disable uploading. During an upload the browser displays
progress and waits for device-side validation.

## Host checks

Run python tests/test_heartbeat.py with a host GCC available. The 168 cases cover
MAVLink v1/v2, armed/disarmed states, component identity, truncation and CRC errors.
Browser checks additionally exercise desktop/mobile layout and locked states.
These checks complement the actual board update tests; they do not simulate
all physical disconnect or power failure cases.


## ESP32-P4 restart handling

The application registers a shutdown handler that stops camera capture/encoding
and the Ethernet driver before a software restart. This prevents DMA activity
from carrying into the next boot on ESP-IDF 5.5.2. See the related upstream
[ESP32-P4 EMAC restart issue](https://github.com/espressif/esp-idf/issues/19085).
During the first bench update, a watchdog failure at startup triggered a rollback to the factory image. Version 1.2.1 stops the camera and Ethernet driver before restarting; the next uploaded image booted from ota_0 and passed the PSRAM startup test.

## Bench validation — 20 October 2026

These are the results from the OTA bench checks:

- ESP-IDF 5.5.2 build and USB migration passed; firmware version 1.2.1.
- The physical controller's disarmed heartbeat unlocked uploading.
- Actual HTTP tests returned 401 for absent/wrong passwords, 413 for an incomplete
  image, and 400 for an incompatible header and a corrupted firmware hash.
- The complete application was uploaded through the dashboard at 192.168.4.1; device validation returned success and selected ota_0.
- Desktop and 390 px phone layouts were checked, including offline/startup/armed
  upload locks, failed-upload feedback and clearing the entered password.
- At 64 seconds, the OTA-installed application logged "Startup check passed;
  update confirmed". Subsequent samples showed Ethernet and USB connected,
  increasing telemetry/command counters, about 25 fps, zero video errors or
  send drops, and a telemetry-drop counter stable at its startup value of 99.
- Flash writes temporarily reduced video rate and increased intentional command
  drops while forwarding was inhibited; the C6 SDIO driver logged queue pressure
  during the transfer. Normal operation recovered after restart.
- Exhaustive power-loss and endurance testing remains incomplete.
