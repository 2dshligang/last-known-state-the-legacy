# Repository ownership

## Developer 1 — world and presentation

Developer 1 owns 3D content, world building, Unity scenes, graphics, prefabs and cinematics:

- `Assets/_Game/World/`
- `Assets/_Game/Art/`
- `Assets/_Game/Scenes/`
- `Assets/_Game/Prefabs/`
- `Assets/_Game/Cinematics/`
- `Assets/_Game/Editor/World/`

Developer 1 is the sole owner of main Unity scene files. Cross-system integration should use prefabs, contracts, events and documented scene entry points rather than uncoordinated scene edits.

## Developer 2 — gameplay and data

Developer 2 owns gameplay logic, quests, narrative implementation, scientific systems, saves, tests and authoritative contracts:

- `Assets/_Game/Systems/`
- `Assets/_Game/Quests/`
- `Assets/_Game/Science/`
- `Assets/_Game/Data/`
- `Assets/_Game/Tests/`
- `Assets/_Game/Contracts/`

## Shared changes

A change outside the author's ownership area requires explicit coordination in the pull request. State the files, reason, integration plan and requested reviewer. Avoid mixing world edits and gameplay contract changes in the same commit when they can be reviewed independently.

