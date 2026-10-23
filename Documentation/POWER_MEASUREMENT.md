# Power and thermal measurements

No measured whole-vehicle power budget or thermal qualification is available yet. The owner has a multimeter; component specifications are estimates, not measured draw.

Measure the propulsion battery branch and PoE electronics branch separately. Record voltage and current together: DC input power is approximately V × I. A 55 A ESC rating is not its idle draw, and 30–40% command is not 30–40% electrical power. Motor output power leaves the enclosure; the entire propulsion input does not become heat inside it.

## Bench sequence

1. With propulsion disabled, record companion input at idle, then with Ethernet, ARK USB telemetry, camera encoding and maintenance Wi-Fi running together. Record whether the ARK is being powered through USB so its consumption is not counted twice.
2. Repeat at the PoE input using suitable equipment. A USB input reading bypasses the PoE converter; injector readings also include tether losses.
3. Measure each secured, submerged ESC/thruster branch progressively in both directions, then representative combined operation with suitably rated equipment.
4. Log enclosure air temperature and component temperatures during realistic operation. Check power changes during boot, camera startup and OTA uploads.
5. Record battery energy delivered and useful runtime; do not estimate endurance from nominal mAh alone.

Use a rated shunt/wattmeter or suitable DC clamp for the thruster bank. Do not route the propulsion bank through an ordinary multimeter's 10 A input, or put a current-mode meter directly across the battery. Fully charged conventional 4S LiPo voltage is 16.8 V; it is not a regulated 14 V supply.

Every report should state firmware, attached loads, measurement location, meter model/rating, voltage, current, duration, temperature, uncertainties and observed peaks. A slow meter can miss transients. Use the [test report template](templates/TEST_REPORT.md) and leave unmeasured values pending.
