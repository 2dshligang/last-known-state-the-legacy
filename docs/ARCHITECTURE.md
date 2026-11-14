# Technical architecture

## Runtime namespace

All production C# code uses the `LKS` root namespace. Systems should be separated into assemblies as the project grows to keep dependencies explicit and compilation times manageable.

## Dependency direction

Gameplay contracts define stable interfaces, events and serializable data shapes. World and scene code consumes these contracts. Presentation code must not become the authoritative source for quest, science or save state.

Recommended dependency direction:

`Contracts -> Data -> Systems/Science/Quests -> World adapters -> Scenes/Cinematics`

## Save data

Save data must be versioned. Store stable identifiers rather than direct scene object references. Loading must tolerate missing optional content and report incompatible schema versions clearly.

## Scene integration

Developer 1 owns main scenes. Developer 2 supplies systems as tested prefabs, ScriptableObject data, contracts or bootstrap components with documented setup steps. Scene edits for integration are performed or explicitly coordinated by Developer 1.

## Automation

Editor generators must be idempotent: repeated execution must update known generated content without duplicating objects or deleting manually authored content.

