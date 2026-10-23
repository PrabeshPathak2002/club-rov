# Publishing and attribution

My public repository: [PrabeshPathak2002/club-rov](https://github.com/PrabeshPathak2002/club-rov).

## Published source set

I publish the project documentation, companion source and tests, licensed IMX sensor component, current custom-part exports and assembly preview. I keep private credentials, generated firmware, machine configuration, raw logs, reference datasheets and commercial CAD collections out of the upload.

The IMX component comes from [mushBrainDave/esp32-p4-imx-camera](https://github.com/mushBrainDave/esp32-p4-imx-camera), revision `9a3f8ca28e93526e0854ba45ad3b461b33ef2dac`; its Apache-2.0 LICENSE and UPSTREAM.txt remain alongside the vendored source. ESP-IDF component dependencies are downloaded from their original registries using the lockfile, retaining their own licenses.

This project builds on software and hardware from ArduPilot/ArduSub, Espressif, Waveshare, ARK Electronics, Blue Robotics, DAKEFPV and APISQUEEN. I also used a Hydrocean S6 reference as inspiration for the frame layout. No affiliation or equivalent performance is implied. The viewing assembly contains imported commercial-component geometry; its scope and attribution remain described in [Models](../Models/README.md). It is a historical viewing asset, not a current manufacturing release.

I have not chosen a project-wide reuse license yet. Public visibility does not grant a license, and third-party rights remain with their owners. I still need to finish the component/model attribution and agree on a license with the contributors.

## Local history

I keep a dated local archive of camera bring-up, animation, superseded CAD, tool scripts and bench logs. I also kept the older OneDrive checkout and its design-history archive. Neither private archive is uploaded. Some historical September design-package links refer to those local-only files; [Hardware](../Hardware/README.md) is the current public revision index.

Never upload `rov_wifi_credentials.h`, password notes, built firmware, full local configuration or unrelated screenshots. Before publishing firmware binaries, I need a credential-provisioning approach that does not embed my maintenance password. Selected dashboard screenshots show UI only; new photography should be reviewed before publication.
