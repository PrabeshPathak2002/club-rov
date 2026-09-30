# Validation plan

This is a proposed sequence, not a record of completed physical tests. Existing mechanical checks concern CAD geometry and meshes; they do not establish depth, lifting capacity, printed-part strength, or endurance.

Before each test, create a [report](templates/TEST_REPORT.md) stating measurable acceptance criteria, configuration, supervision, and abort conditions. Leave a test pending when necessary limits or instruments are unavailable.

1. **Unpowered inspection:** identify revisions, match connector pins, inspect prints/seals, and review wiring. Resolve discrepancies before power-up.
2. **Electronics bench:** with propulsion disabled, establish power rails, USB, Ethernet, telemetry, and camera behavior. Record voltage, current, versions, and reconnection observations.
3. **Failure behavior:** test Ethernet, USB, and PoE loss in a controlled setup. Verify ARK behavior and backup switchover independently. Companion traffic must not mask the loss of the operator connection.
4. **Propulsion:** verify channel mapping, neutral, direction, and reverse operation in a supervised arrangement following thruster operating instructions. Record actual vehicle positions rather than assuming connector numbers match them.
5. **Enclosure qualification:** establish a suitable leak/pressure procedure with the responsible supervisor before installing valuable electronics or attempting the target depth. Record conditions, duration, equipment, and criteria.
6. **Shallow water:** assess trim, buoyancy, controllability, communications, recovery, and post-test enclosure condition.
7. **Progressive depth and endurance:** expand conditions only through reviewed tests supported by component ratings and earlier results. Publish demonstrated conditions, not just the design target.
8. **Sensors and demonstrations:** qualify calibration, timestamps, logging, and effects on control/video. Establish repeatable preflight and recovery procedures before public events.

Retain concise reports, relevant measurements, credential-free configuration snapshots, and selected photos. Record failed or aborted tests. Link large logs and videos from a deliberate data archive rather than adding them casually to source history.
