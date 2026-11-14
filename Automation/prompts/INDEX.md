# Ready-to-use task prompts

There are 48 complete English prompts: 21 for Developer 1 (W), 21 for Developer 2 (S), and 6 shared automation tasks (O). Open the matching file and give its entire contents to the assigned Codex agent. Keep one writer per checkout; DeepSeek may review a redacted diff and acceptance evidence without writing concurrently. These are a work backlog, not 48 simultaneously running agents.

Run O01 first, then S01 and W01 can progress independently. O02/O03 establish provenance and CI. Contracts must merge before consumer integration. Follow each prompt's dependencies; do not execute W01-W21 in isolation from S tasks. W19/S19 converge on integration; W20/S20 share a candidate; W21/S21 verify and document it. O04 enables guarded merging after real server checks; O05/O06 provide delivery and bounded diagnosis.

The repository currently uses feature/<area>-<summary>, fix/<area>-<summary> and docs/<summary>. Keep that convention, for example feature/world-W04-station and feature/systems-S04-power, unless the team explicitly changes it. Source/resource updates never override AGENTS.md.

## Coordinator

- [O01: Repository and machine baseline](O01.md) - requires None.
- [O02: Provenance ingestion and asset quarantine](O02.md) - requires O01.
- [O03: CI foundation and full validation gates](O03.md) - requires O01, O02.
- [O04: Trusted ownership and safe automatic merging](O04.md) - requires O03.
- [O05: Immutable Windows delivery](O05.md) - requires O03, W20, S20.
- [O06: Bounded diagnostic and repair loop](O06.md) - requires O03, O04.

## Developer 1 - world and presentation

- [W01: Legal asset discovery and practical fallback](W01.md) - requires O01.
- [W02: URP import and asset inspection](W02.md) - requires W01.
- [W03: Station visual grammar and modular kit](W03.md) - requires W02.
- [W04: Compact station layout and safe generator](W04.md) - requires W03, S01.
- [W05: First-person controller and input focus](W05.md) - requires W04, S02.
- [W06: Traversal, doors and interaction readability](W06.md) - requires W05, S03.
- [W07: Power console presentation](W07.md) - requires W06, S04.
- [W08: Shelter and seedling preservation set](W08.md) - requires W07, S05, S06.
- [W09: Archive terminal and readable document view](W09.md) - requires W08, S07.
- [W10: ECHO spatial reconstruction](W10.md) - requires W09, S08.
- [W11: ECHO comparison and causality interface](W11.md) - requires W10, S09.
- [W12: Mars exterior and sample context](W12.md) - requires W11, S10.
- [W13: Laboratory bench and analysis presentation](W13.md) - requires W12, S11.
- [W14: Farewell and arrival without speech](W14.md) - requires W13, S13.
- [W15: Research report, photograph and ending](W15.md) - requires W14, S12, S14.
- [W16: Lighting and silent atmosphere](W16.md) - requires W15.
- [W17: Visual accessibility and content audit](W17.md) - requires W16, S15.
- [W18: Measured world performance and memory](W18.md) - requires W17, S17.
- [W19: Full scene integration and checkpoint route](W19.md) - requires W18, S18.
- [W20: Windows build configuration and visual smoke](W20.md) - requires W19, S19, O03.
- [W21: Final world acceptance and demonstration](W21.md) - requires W20, S20.

## Developer 2 - authoritative systems and science

- [S01: Authoritative contracts and dependency graph](S01.md) - requires O01.
- [S02: Composition root and stable service lifecycle](S02.md) - requires S01.
- [S03: Interaction, quest graph and recoverable progress](S03.md) - requires S02.
- [S04: Deterministic limited power network](S04.md) - requires S03.
- [S05: Life support and radiation decision rules](S05.md) - requires S04.
- [S06: Food preservation and queued communications](S06.md) - requires S05.
- [S07: English content catalog and archive metadata](S07.md) - requires S03, O02.
- [S08: ECHO evidence records and replay engine](S08.md) - requires S07, S05.
- [S09: Incident reconstruction and causal validation](S09.md) - requires S08.
- [S10: Scientific snapshot importer and sample catalog](S10.md) - requires S07, O02.
- [S11: Sample collection, blank control and analysis](S11.md) - requires S10, S04.
- [S12: Evidence-limited research report](S12.md) - requires S11, S09.
- [S13: Narrative timeline and accessible presentation requests](S13.md) - requires S12.
- [S14: Ending predicate and completed-game state](S14.md) - requires S13, S06.
- [S15: English-only and no-speech release audits](S15.md) - requires S14, S07.
- [S16: Versioned atomic saves and recovery](S16.md) - requires S14.
- [S17: Determinism, tutorial hints and pacing telemetry](S17.md) - requires S16.
- [S18: Automated integrated gameplay route](S18.md) - requires S17.
- [S19: Source-use coverage and license inventory](S19.md) - requires S18, W18.
- [S20: Release regression and clean-machine support](S20.md) - requires S19, W19.
- [S21: Submission science explanation and final handoff](S21.md) - requires S20, W21.
