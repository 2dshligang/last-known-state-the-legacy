# Repository instructions

## Project

- Title: LAST KNOWN STATE: THE LEGACY
- Engine: Unity 6.6, Universal Render Pipeline
- Language: C#
- Namespace: `LKS`
- Platform: Windows x64
- Target playtime: 20–25 minutes
- Team: two developers using Codex

## Player-facing language

Every player-facing string must be English. This includes menus, objectives, archives, prompts, terminals, tutorials, scientific explanations, reports, credits and endings. Do not add placeholder Russian text. Check every TextMeshPro text asset before release.

## Audio

Do not add voice acting, synthesized speech, spoken dialogue, voiceover narration or AI character voices. Narrative dialogue must be readable on screen. The game must remain fully understandable when muted, and audio must never be required to solve a puzzle.

## Scientific integrity

Distinguish actual findings, engineering models and fictional story details. Organic molecules do not prove life, and potential biosignatures are not confirmed evidence of life. Never fabricate citations or measurements.

## Ownership

Developer 1 owns:

- `Assets/_Game/World/`
- `Assets/_Game/Art/`
- `Assets/_Game/Scenes/`
- `Assets/_Game/Prefabs/`
- `Assets/_Game/Cinematics/`
- `Assets/_Game/Editor/World/`

Developer 2 owns:

- `Assets/_Game/Systems/`
- `Assets/_Game/Quests/`
- `Assets/_Game/Science/`
- `Assets/_Game/Data/`
- `Assets/_Game/Tests/`
- `Assets/_Game/Contracts/`

Developer 1 is the sole owner of main Unity scene files. Developer 2 owns authoritative gameplay contracts. Do not edit files owned by the other developer without explicit coordination.

## Required workflow

1. Inspect the repository and recent changes.
2. Read existing contracts and project instructions.
3. Identify exactly which files must change.
4. Implement the requested feature completely.
5. Run available compilation and automated tests.
6. Verify in Unity Editor when access exists.
7. Do not claim visual correctness based only on compilation.
8. Document required manual verification.
9. Report broken or missing dependencies.
10. Never overwrite known-good features unnecessarily.

Prefer safe, repeatable and idempotent Unity Editor automation for scene, prefab and material setup.

Every task report must include: files created or modified, implementation details, tests actually executed, test results, Unity Editor actions required, integration risks, remaining issues and a suggested Git commit.

