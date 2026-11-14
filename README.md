# LAST KNOWN STATE: THE LEGACY

`LAST KNOWN STATE: THE LEGACY` is a short first-person 3D narrative game for Windows x64, built with Unity 6.6 and the Universal Render Pipeline.

## Premise

In 2059, eight-year-old Alexei says goodbye to his father, Dmitry, before the first ASTERION expedition leaves Earth to investigate ancient Martian environments and potential biosignatures. The expedition is lost after a cascading technical failure.

In 2089, Alexei returns to Mars. He explores the abandoned station, restores life-support infrastructure, reconstructs archived events and completes his father's unfinished scientific research.

Target playtime: 20–25 minutes.

## Core gameplay

1. Explore a Mars station in first person.
2. Restore a limited electrical power network.
3. Investigate the historical incident through ECHO reconstruction.
4. Collect and analyze geological samples.
5. Complete the unfinished research.
6. Unlock the narrative ending.

## Technology

- Unity 6000.6.9f1
- Universal Render Pipeline (URP) 17.6.0
- C# under the `LKS` namespace
- Windows x64
- Performance target: 1920x1080 at 60 FPS on RTX 2060 Super, Ryzen 5 5600 and 32 GB RAM

The performance specification is a target and must not be reported as measured until profiling has been performed on representative hardware.

## Team ownership

- Developer 1: 3D world, art, Unity scenes, prefabs, graphics and cinematics. Developer 1 is the sole owner of main Unity scene files.
- Developer 2: gameplay systems, quests, narrative implementation, scientific systems, data, automated tests, authoritative gameplay contracts and saves.

Do not edit files owned by the other developer without explicit coordination. See [Ownership](docs/OWNERSHIP.md) and [Contributing](docs/CONTRIBUTING.md).

## Project status

The repository contains a Unity project with a bootstrap scene for checking the editor and Windows x64 build setup. See the [Unity setup instructions](docs/UNITY_SETUP.md).

## Non-negotiable content rules

- All player-facing content is English.
- No voice acting, synthesized speech, spoken dialogue or voiceover narration.
- The complete game must remain understandable when muted.
- Scientific claims must distinguish established findings, engineering models and fictional story details.
- Organic molecules and potential biosignatures are not proof of life.

## Getting started

1. Install Unity 6000.6.9f1 with Windows Build Support and sign in to Unity Hub on your machine.
2. Clone the repository with Git LFS enabled.
3. Open the repository root from Unity Hub.
4. Confirm that URP is installed and assigned before creating production scenes.
5. Work only in your owned directories or coordinate an ownership exception in the pull request.

## Documentation

- [Project charter](docs/PROJECT_CHARTER.md)
- [Ownership](docs/OWNERSHIP.md)
- [Contributing](docs/CONTRIBUTING.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Scientific integrity](docs/SCIENTIFIC_INTEGRITY.md)
- [Definition of done](docs/DEFINITION_OF_DONE.md)
