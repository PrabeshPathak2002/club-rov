# Contributing

Club members are encouraged to learn from and contribute to this project. Start with the [new member guide](Documentation/NEW_MEMBERS.md). Choose a small task from the [roadmap](Documentation/ROADMAP.md), agree on an expected result with a project mentor, and work in a separate branch.

A pull request should explain the problem, the change, how it was checked, and what remains uncertain. Update relevant documentation and BOM entries. Request review from someone familiar with the subsystem before merging.

## Documentation conventions

Write reference guides in a neutral, project-focused voice and use direct instructions for procedures. Review each guide as if the reader has only a fresh clone:

- List required software, hardware, versions and working directories before commands. State what setup scripts configure automatically.
- Use repository-relative paths or labeled placeholders. Identify private archives, credentials, generated outputs and separately installed tools explicitly.
- Keep current instructions separate from dated test history. A test observation describes its environment; it is not a prerequisite already satisfied on the reader's computer.
- Label superseded designs and historical inventories. Direct readers to the applicable revision instead of describing an older snapshot as the current release.
- Check local links, heading anchors and screenshots against the current source. Retain measured results and limitations when editing prose.

Personal development journals and raw session narratives should remain separate from setup and reference guides.

Use explicit units, exact part/firmware revisions where known, and TBD where unknown. Distinguish planned, implemented, bench-tested, and water-tested behavior. Link original sources and explain important tradeoffs. Record failed tests as well as successes.

Do not commit credentials, machine-specific configuration, personal paths, or raw session logs. Confirm redistribution rights before adding third-party assets. The ignore rules keep credentials, generated firmware and private logs out of Git. Current custom-part exports live under Hardware; review new binary assets before adding them.
