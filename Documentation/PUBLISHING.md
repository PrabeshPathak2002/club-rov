# Publishing and attribution

Public repository: [PrabeshPathak2002/club-rov](https://github.com/PrabeshPathak2002/club-rov).

## Published source set

The publication set includes project documentation, companion source and tests, the licensed IMX sensor component, selected custom-part exports and the assembly preview. Private credentials, generated firmware, machine configuration, raw logs, reference datasheets and commercial CAD collections are excluded.

The IMX component comes from [mushBrainDave/esp32-p4-imx-camera](https://github.com/mushBrainDave/esp32-p4-imx-camera), revision `9a3f8ca28e93526e0854ba45ad3b461b33ef2dac`; its Apache-2.0 LICENSE and UPSTREAM.txt remain alongside the vendored source. ESP-IDF component dependencies are downloaded from their original registries using the lockfile, retaining their own licenses.

This project builds on software and hardware from ArduPilot/ArduSub, Espressif, Waveshare, ARK Electronics, Blue Robotics, DAKEFPV and APISQUEEN. The frame layout was also informed by a Hydrocean S6 reference. No affiliation or equivalent performance is implied. The viewing assembly contains imported commercial-component geometry; its scope and attribution remain described in [Models](../Models/README.md). It is a historical viewing asset, not a current manufacturing release.

A project-wide reuse license has not been selected. Public visibility does not grant a license, and third-party rights remain with their owners. Component/model attribution and contributor agreement on a license remain pending.

## Local history

A dated local archive preserves records of camera bring-up, animation, superseded CAD, tool scripts and bench logs. The older OneDrive checkout and its design-history archive are also retained. Neither private archive is uploaded. Some historical September design-package links refer to those local-only files; [Hardware](../Hardware/README.md) is the current public revision index.

Never upload `rov_wifi_credentials.h`, password notes, built firmware, full local configuration or unrelated screenshots. Before publishing firmware binaries, a credential-provisioning approach that does not embed the maintenance password is required. Selected dashboard screenshots show UI only; new photography should be reviewed before publication.
