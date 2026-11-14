# Automation foundation

This change adds shared automation under `Automation/`. It leaves Developer 1 and Developer 2 owned Unity folders unchanged. Review this shared-tooling placement as the coordination step required by `OWNERSHIP.md`.

- [Start here](../Automation/README.md)
- [Verified 2026 challenge Resources and science mechanics](../Automation/docs/SCIENCE.md)
- [Playable architecture proposal](../Automation/docs/ARCHITECTURE.md)
- [Two-PC workflow and merge safety](../Automation/docs/AUTOMATION.md)
- [Setup runbook](../Automation/docs/RUNBOOK.md)
- [48 ready prompts: 21 world, 21 systems, 6 coordinator](../Automation/prompts/INDEX.md)
- [Actual validation and missing dependencies](../Automation/docs/VALIDATION.md)

`.github/workflows/lks-tooling.yml` runs portable tooling tests and immutable source verification on hosted Linux. A green run means only that these tools/data checks passed. It does **not** mean Unity compiled or a game can be released.

The full Unity test/build workflow remains `Automation/templates/ci.yml.example`. Install it after a functioning exact-version Editor, Windows modules, legal CI license and isolated runner are provisioned. Required checks, owner account mapping, trusted merge policy and native auto-merge need repository administrator configuration. The current account has push access but no admin permission; auto-merge was found disabled.

The local Unity executable reports 6000.6.9f1, but scratch creation failed twice with exit 199. Its expected licensing-client and Package Manager executables are absent. No production project settings/packages, scene files or authoritative gameplay contracts were invented to hide that failure.

The design documents are proposals subordinate to existing authoritative contracts and ownership rules. Follow existing branch naming and coordinate future changes to root settings. Do not merge or release this as a completed game.
