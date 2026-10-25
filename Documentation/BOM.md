# Current ROV bill of materials

Build selection reviewed 24 October 2026. Quantities below are for one six-thruster ROV. This is the primary build list; the [September CAD inventory](../Final%20Design/BOM/September_2026_CAD_Inventory.md) is historical reference only.

**On hand** and **ordered** reflect the last recorded inventory on 23 October; confirm stock before purchasing. **Selected** identifies a build choice, not confirmed ownership. **Provisional** quantities or dimensions require a final fit/wiring check.

## Electronics and propulsion

- **6 × APISQUEEN U01 thrusters** — four horizontal, two vertical. Confirm current inventory.
- **1 × ARK FPV flight controller** — on hand; ArduSub 4.7.2.
- **1 × Waveshare ESP32-P4-WIFI6-POE-ETH** — on hand; communications and camera companion.
- **1 × Raspberry Pi Camera V2.1 / IMX219 with matching CSI cable** — camera on hand and bench-tested; verify cable availability.
- **1 × DAKEFPV four-in-one 55 A ESC** — on hand; four motor channels, measured 20 × 20 mm mounting pattern.
- **2 × Blue Robotics Basic ESCs** — selected; two additional motor channels.
- **1 × Zeee 4S 3300 mAh battery** — selected; 132 × 43 × 29 mm, ±2 mm overall dimensions.
- **1 × Bar02 depth sensor** — on hand; shallow-water sensor, installation pending.

ESC firmware identification, settings and wiring belong in [electronics setup](ELECTRONICS.md), not separate BOM entries.

## Connectors and power distribution

- **6 × AMASS MR30 mated pairs** — ordered; one three-phase connection per thruster. Not yet soldered in the last bench record.
- **4 × XT60 mated pairs** — provisional harness count; confirm the final branch arrangement before ordering.
- **3 × EW-LP12 six-pin motor connector sets** — selected; each set requires a panel socket and matching cable plug, carrying two thrusters. Electrical and sealing suitability still require verification.
- **1 × LP20 single-hole RJ45 connector set** — selected; panel socket and matching plug. This is the selected Ethernet fitting, replacing the earlier M20/LP16 options.
- **2 × copper distribution bars** — one positive, one negative; planned from approximately 6.35 mm stock. Finished size, terminal spacing, current capacity and insulating enclosure remain provisional.
- **1 × PRV/vacuum fitting** — exact fitting and ownership to confirm; an M10 clearance opening is reserved in the endcap study.

A **mated pair/set** includes both connecting halves; it is not two additional installed connections. Endcap openings for the Bar02 and PRV are machining features, not extra components. The Bar02 itself is counted above. No additional PDB or XT60 splitter is specified for the proposed two-busbar arrangement.

## Enclosure and printed parts

- **1 × reusable enclosure assembly** — existing 4-inch, 200 mm tube with its flanges and retained endcap/seal hardware. Verify the actual assembly before replacing or ordering parts.
- **1 × replacement acrylic endcap** — 6.35 mm stock, using the current LP20/LP12 fit-study layout. Coupon fit and pressure qualification remain pending; count it as a replacement, not a second enclosure.
- **1 × printed frame set** — collars, decks, covers, side frames and handle. Use the September frame references with the later revisions identified in [Hardware](../Hardware/README.md); a complete combined revision export is still pending.
- **6 × revised integral rail-slot thruster mounts** — four horizontal and two vertical; use [current mount exports](../Hardware/ThrusterMounts/README.md). The obsolete separate clamp jaws are not additional parts.
- **1 × ARK/ESC stack adapter** and **4 × 7 mm M3 spacers** — use the [stack exports](../Hardware/StackAdapter/README.md).
- **1 × electronics tray** — reuse the existing tray where suitable. The wire-management holders are a packaging study, not released print parts.

Printed parts use **Bambu Lab PETG Basic, 100% infill**. Fit coupons, CAD reference solids and clearance envelopes are test/design aids, not installed vehicle parts or additional assembly quantities.

### Fasteners

The current thruster-mount selection uses **6 × M4 × 50 mm bolts, 6 × M4 nuts and 6 × M4 washers**. Confirm actual grip length and washer fit before purchasing. These belong to the six mounts above and are not an additional mount assembly.

Frame/enclosure hardware and ARK/ESC M3 screw lengths still require a consolidated recount against the final assembly. Do not add the historical totals of 38 frame screws and 30 imported screw bodies to a shopping list: they describe an older CAD snapshot, not a verified current fastener requirement. Existing fasteners may be reused where their dimensions and condition are suitable.

## Items requiring final sizing or inventory checks

Complete these selections before turning the BOM into an order:

- **Power harness:** wire gauges/lengths, battery fuse and holder, ring terminals, insulation and busbar cover/retention.
- **Signal harnesses:** ARK-to-ESC leads, Bar02 I2C lead, USB host connection and internal Ethernet lead; verify supplied cables before buying duplicates.
- **Cable retention and seals:** heat shrink, strain relief, straps/ties and any missing or replacement seals.
- **Trim and detection:** buoyancy material, ballast and leak-detection parts; sizes/models remain unselected.
- **Support equipment:** PoE source/tether, compatible 4S balance charger, surface computer and controller. Verify existing club equipment rather than treating each as a new vehicle purchase.

The temporary F722 configuration board, old Turnigy 4000 mAh battery, superseded connector alternatives, test coupons and optional future recovery rigging are excluded from the installed build list. Preserve test/configuration tools separately; they are not extra ROV components.
