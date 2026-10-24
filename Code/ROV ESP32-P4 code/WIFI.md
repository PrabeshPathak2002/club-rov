# Maintenance Wi-Fi

The maintenance hotspot provides status checks and companion updates without a programming cable. Ethernet remains the link for vehicle telemetry and video.

Connect to **ROV-Maintenance** with your configured password and open
**http://192.168.4.1**. Stay connected if your device reports no Internet.
Use the hotspot above water or on the bench. The dashboard is also available
over Ethernet at http://192.168.137.2.

The ESP32-C6 supplies WPA2 Wi-Fi over four-bit SDIO to the ESP32-P4. The access
point uses channel 6, allows three clients and starts about 14 seconds after
reset. SDIO pins: CLK18, CMD19, D0=14, D1=15, D2=16, D3=17; reset GPIO54.
Libraries: esp_hosted 2.12.12 and esp_wifi_remote 1.6.5, ESP-IDF 5.5.2.

The dashboard shows live diagnostics and supports password-authorized firmware
uploads. See **[OTA.md](OTA.md)** for current build commands, update instructions,
disarm requirements, validation, partition layout and rollback behavior.
MAVLink and camera UDP sockets bind to Ethernet. The hotspot supplies no vehicle
command bridge, camera video or Internet routing.

The local main/rov_wifi_credentials.h is excluded from Git. For a fresh checkout,
copy the supplied .example header and choose a unique 12-63 character password.
Do not publish the populated header, local password notes or deployed binaries.

## Compatibility notes

The factory C6 firmware reports version 0.0.0, producing an ESP-Hosted
host/coprocessor version warning. The bench configuration uses a 100 Hz FreeRTOS tick,
also producing a recommendation for 1000 Hz. Startup and simultaneous telemetry, video and dashboard operation were bench-tested. The factory C6 firmware and bench scheduler setting were retained. Investigate these warnings if RPC timeouts or
instability occur. These observations do not constitute an endurance test.
