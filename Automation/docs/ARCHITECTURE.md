# Playable architecture and scope

## Outcome and limits

Build a Windows x64 Unity 6.6 URP first-person narrative game with a 20-25 minute first playthrough. All shipped text, credits, tutorials, logs and scientific explanations are English. No voice recording, synthesized speech, spoken video, dialogue audio or narration. Optional licensed ambience and environmental effects must never convey required puzzle information alone.

This document proposes integration contracts. Developer 2 must reconcile them with existing authoritative contracts before implementing them. It does not create a second API authority or authorize cross-owner scene edits.

## Story and chronology

2059: eight-year-old Alexei says goodbye to Dmitry before the first ASTERION expedition. An engineering cascade loses the expedition. 2089: Alexei returns, restores safe access, reconstructs the failure and completes the research. Use exact dates to avoid the apparent 8 + 30 = 38 discrepancy: a proposed birthday is 2050-03-15, departure 2059-01-20, return 2089-11-14. Alexei is 8 and 39 respectively. These are fictional authoring choices.

The incident is not a hostile AI plot. A controller treats stale/modelled values as current observations; a dependency cascade exhausts backup capability. The player proves the sequence from records with different confidence levels. Avoid graphic death, jump scares, coercive countdowns and dense prose for the challenge's young audience. Loss and remembrance remain readable, optional detail expands in an archive, and no audio is necessary.

The final interaction is looking at the photograph. The final narrative screen must display exactly `Dad... I did it.` with three ASCII periods. Credits are accessible afterward through a deliberate action, not an automatic overlay that obscures the ending.

## Target pacing, to be measured

| Time | Place and activity | Playable result |
|---|---|---|
| 0-2 min | Text-only farewell and arrival; skippable after first viewing | Establish the photograph, movement and interact controls. |
| 2-6 min | Airlock and utility room | Select loads, restore air circulation, learn finite power. |
| 6-10 min | Archive and ECHO | Identify timestamp/quality mismatch and reconstruct the cascade. |
| 10-13 min | Shelter/seedling preservation decision | Radiation training event, food-system trade-off, recovery route. |
| 13-19 min | Short exterior loop and laboratory | Collect three specimens, run a blank/control and evidence comparison. |
| 19-23 min | Compose and queue the research report | Revise overclaims, acknowledge uncertainty, complete the research. |
| 23-25 min | Surface overlook and photograph | Quiet visual/text ending; save completed state. |

These are design budgets, not measured playtime. Pause simulation when a long document, settings menu or focused terminal is open. Avoid mandatory waiting for communication delay. Add text hints after repeated failed actions, preserving exploration rather than solving the task automatically. Keep one compact station and one exterior loop; no combat, inventory crafting tree, multiplayer, procedural campaign or open world.

## Ownership and assembly boundaries

| Owner | Authoritative paths | Responsibilities |
|---|---|---|
| Developer 1 | `Assets/_Game/World/`, `Art/`, `Scenes/`, `Prefabs/`, `Cinematics/`, `Editor/World/` | Main scenes, world presentation, collision, prefab wiring, generation and graphical performance. |
| Developer 2 | `Assets/_Game/Systems/`, `Quests/`, `Science/`, `Data/`, `Tests/`, `Contracts/` | Rules, state, saves, science import, content, tests and authoritative interfaces. |
| Coordinator, explicitly assigned | `Automation/`, `.github/`, `docs/`, `Packages/`, `ProjectSettings/`, root configuration | CI, version/package pins, shared changes and repository policy. |

Define who owns root folder `.meta` files at setup. A developer creates folder metadata for their subtree; shared parents are created once by the coordinator and committed before parallel work. Developer 1 is the only writer of main scene files even when a system bug is found. Developer 2 requests scene changes with a handoff file rather than editing them.

Proposed assembly graph: `LKS.Contracts` at the bottom; `LKS.Systems`, `LKS.Science` and `LKS.Quests` depend on contracts; `LKS.World` consumes those APIs for binding; `LKS.Editor.World` contains editor-only assembly tools; tests reference their subjects. Contracts should be plain C# where possible. Keep editor code out of runtime assemblies. Pin the exact Unity patch, URP, Input System, Test Framework and any importer package in project files; never use floating versions or upgrade on one PC alone.

## Contracts to freeze in S01

| Contract | Requirement |
|---|---|
| `StableId` | Serialized stable ID, collision detection, survives scene regeneration and saves. |
| `IInteractable` | Read-only availability/prompt plus explicit request returning success/failure reason. No UI bypass of authority. |
| `IPowerService` | Allocation commands, immutable snapshot, priority rules, transitions with reason codes. |
| `ILifeSupportService` | Safe/degraded state derived from power and resources; recoverable failure. |
| `IEchoService` | Evidence availability and ordered reconstruction, provenance/quality flags, repeatable playback. |
| `ISampleService` / `IResearchService` | Collection, contamination/control validity, interpretation and report acceptance. |
| `IQuestService` | Explicit prerequisites and idempotent progress updates, no stringly scene-name logic. |
| `ISaveService` | Versioned snapshot, migration policy, atomic write, backup and content hash references. |
| `IScienceCatalog` | Validated immutable records with source IDs and explicit observation/model/fiction categories. |
| `INarrativeView` | English text/portrait/camera requests; never audio speech. |

Public events carry a sequence number, cause, stable ID and immutable state. Use a main-thread event queue and explicit subscription disposal; no global static event subscriptions that survive a new game. World scripts must not mutate quest flags, power counters or scientific validity directly. Tests can inject a deterministic clock and a fake content catalog.

## Energy and life support

Use physical units internally: kW for rate, kWh for battery, seconds for simulation time. Convert seconds to hours explicitly. For nonnegative generation/load, charge by `max(generation-load,0) * dt/3600 * chargeEfficiency`; discharge by `max(load-generation,0) * dt/3600 / dischargeEfficiency`; clamp to capacity and handle empty battery by deterministic load shedding. Tick at a fixed simulation step; frame rate must not change outcomes.

The example in `samples/science/engineering-model.json` supplies **fictional balancing values**: 6 kW generation; lab, archive and support loads cannot all run indefinitely. It is not a NASA station specification. Every choice previews the expected trade-off in plain English. Recovery must exist from any legal allocation; essential access/air reserve is protected to avoid softlocks. Preserve the chosen load set and battery in saves. Radiation shelter costs can come from actuators, pumps and operational changes; shielding itself is passive.

## ECHO and research

ECHO is a visual reconstruction from logs, not time travel. Use timestamped silhouettes/props, captions and an evidence panel. Mark missing records and estimates visibly. Player orders three critical events and distinguishes stale telemetry, forecast and measured state. Confidence is constrained by evidence completeness. The causal explanation must be invariant across presentation and report.

Three samples suffice: a sedimentary analog, a mineral/alteration comparison and a control/contaminated specimen. Inspecting context, sampling and running the control each changes what can be concluded. The analysis UI shows observation separately from interpretation. Do not fabricate photorealistic NASA rover images or measured spectra for the fictional specimens. Every fictional lab response is labeled simulation; real reference plots retain their source and context.

## Reproducible scene assembly

Developer 1's generator creates a dedicated `LKS_Generated` subtree using stable IDs and a saved generation manifest. It updates only its own tracked objects. It must not delete all scene roots or recreate the entire scene. Manually authored content remains outside that subtree, or is explicitly marked protected. Preview changes before first adoption; block modifications when the scene has unsaved manual edits.

Create materials/prefabs only if missing, preserve `.meta` GUIDs, resolve by asset GUID where possible and use serialized configuration instead of hardcoded hierarchy paths. Run generation twice and compare object counts, IDs, references and serialized changes. A third run after a manual prop insertion must preserve that prop. Safe cleanup only removes previously generated objects recorded in the manifest and never arbitrary files.

## Definition of playable completion

- New Game to photograph ending in an actual Windows player, offline and muted, without the Editor inspector or debug shortcuts.
- Integration tests cover interrupted power operations, repeated ECHO, out-of-order sampling, invalid report revision, pause/resume, save/load at every chapter and restart after ending.
- Scene validation catches missing scripts, broken references, duplicate IDs, unreachable essential interactions, unassigned input/UI and absent TMP fonts.
- An English content catalog, TMP asset/scene inspection, runtime text capture and human screenshot review cover text that regex alone misses. Audit textures for embedded text and all audio assets for speech.
- Target 1920x1080 at 60 FPS on RTX 2060 Super / Ryzen 5 5600 / 32 GB is measured in a player build. Record CPU/GPU frame times, memory, loading and shader spikes; headless CI is not visual or performance proof.
- Keep a known-good build before major changes. If scope threatens completion, remove optional assets/detail; preserve the complete playable loop.
