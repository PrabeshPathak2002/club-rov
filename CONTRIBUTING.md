# Contributing

Club members are encouraged to learn from and contribute to this project. Start with the [new member guide](Documentation/NEW_MEMBERS.md). Choose a small task from the [roadmap](Documentation/ROADMAP.md), agree on an expected result with a project mentor, and work in a separate branch.

A pull request should explain the problem, the change, how it was checked, and what remains uncertain. Update relevant documentation and BOM entries. Request review from someone familiar with the subsystem before merging.

Write reference documentation in a neutral, project-focused voice. Use direct instructions for procedures and dated evidence for validation records. Keep personal development journals separate from setup and reference guides. Write setup guides for a fresh clone on another computer: list prerequisites, use repository-relative paths or clearly labeled placeholders, and state what scripts configure automatically. Avoid assumptions such as “already installed on this desktop.” Keep machine-specific observations in dated validation records and label them as test conditions, not reader prerequisites.

Use explicit units, exact part/firmware revisions where known, and TBD where unknown. Distinguish planned, implemented, bench-tested, and water-tested behavior. Link original sources and explain important tradeoffs. Record failed tests as well as successes.

Do not commit credentials, machine-specific configuration, personal paths, or raw session logs. Confirm redistribution rights before adding third-party assets. The ignore rules keep credentials, generated firmware and private logs out of Git. Current custom-part exports live under Hardware; review new binary assets before adding them.
