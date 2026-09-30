# New member learning path

Begin with an unpowered vehicle or its design files and work with a project mentor. Prior underwater robotics experience is not required.

## 1. Understand the vehicle

Read the project README and [architecture](ARCHITECTURE.md). Identify the enclosure, six thrusters, flight controller, companion, tether, and power sources. Explain how a command reaches a motor and how video returns to the surface.

Deliverable: an annotated sketch and questions for your mentor.

## 2. Practice mechanical work

Read the [print guide](../Final%20Design/Guides/PRINT_GUIDE.md) and [BOM](../Final%20Design/BOM/Current_ROV_BOM.md). Start with a fit coupon. Record material, orientation, slicer settings, and measured fit. Practice heat-set inserts on a spare coupon with supervision.

Deliverable: a fit report with dimensions and photos. Fit alone does not establish strength or pressure capability. Binary print files currently belong to the complete local design package.

## 3. Trace the electronics

Use actual board documentation to identify connector orientation, polarity, signal ground, VBAT, and regulated supplies. The architecture overview is not a pin-by-pin wiring guide. Keep propulsion disabled during initial communications work.

Deliverable: a reviewed wiring sketch with exact revisions and unresolved connections marked.

## 4. Learn the software

Read [Code/README.md](../Code/README.md). Once firmware exists, reproduce a build before changing behavior. Learn what a MAVLink heartbeat means and why video alone does not prove a healthy control link.

Deliverable: a reproducible build or documentation improvement. Until firmware is available, diagram normal operation, USB reconnection, and tether loss.

## 5. Participate in supervised testing

Follow the [testing plan](TESTING.md). Agree on acceptance criteria, abort conditions, and operator/observer/recovery responsibilities before starting. Powered propulsion and water trials require the club's designated supervisor.

Deliverable: a completed [test report](templates/TEST_REPORT.md), including unexpected results and follow-up actions.

## 6. Contribute and teach

Take a small change through a pull request using [CONTRIBUTING.md](../CONTRIBUTING.md). Explain it to another member. At demonstrations, distinguish tested capabilities from planned ones.

## Common terms

- ROV: remotely operated vehicle, controlled from the surface.
- AUV: autonomous underwater vehicle.
- ESC: electronic speed controller for a motor.
- Companion: the ESP32 communications/camera board in this project.
- MAVLink: the planned flight-controller messaging protocol.
- PoE: power over Ethernet through compatible equipment.
- BOM: bill of materials.
