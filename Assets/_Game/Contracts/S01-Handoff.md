# S01 contract handoff

API version 1.0. Baseline: O01 commit `8b1d8cc`. Runtime implementations follow in S02 and later. This change adds no operational station, research data, or scene wiring.

## Dependencies

- `LKS.Contracts` depends on the C# standard library.
- `LKS.Contracts.Tests` depends on Contracts and the Unity Test Framework and is Editor-only.
- Future Systems, Quests and Science assemblies depend on Contracts. World adapters depend on Contracts and obtain session services at the bootstrap boundary in S02. Contracts never depend on those assemblies.

## Binding steps for Developer 1

1. Reference `LKS.Contracts` from the World adapter assembly. Use `LKS.Tests.FixtureInteractable` as a wiring example.
2. Assign a serialized `StableId` per authored target and preserve its value on regeneration. IDs are lowercase ASCII letters and digits separated by dots or hyphens, beginning with a letter. Validate all registered targets together with `StableId.ValidateUnique` at bootstrap.
3. Read availability with `IInteractable.Read(actor)`. Resolve the prompt key through the English catalog. On input, call `Request(actor, action)` and handle the returned code. The request must revalidate availability. `AlreadyApplied` succeeds without replaying the effect. Display the resolved result message.
4. Render the initial service snapshot, subscribe for updates and dispose each subscription on disable or session replacement. Read, command and dispatch on the session's main thread. Services publish immutable snapshots after committing accepted state, with increasing per-session sequence numbers and stable subject and cause IDs. Dispatch in sequence after commands return. Skip callbacks already disposed at dispatch time.
5. Keep quest and service state in the session. Treat read-only collection snapshots as immutable owned copies, including collections supplied by service implementations. Wire narrative requests to text, portrait and camera only.

The fixture uses `actor.fixture`, `action.inspect` and `fixture.archive`. These are test IDs. S02 supplies the composition root and production registry. Coordinate production IDs before wiring W04.

## Compatibility and evidence

Breaking API or ID semantics require a major contract version increment and coordinated migration. Additive compatible changes use a minor increment. Save schema versions are independent. Persist stable IDs and preserve unknown measurements as null. A science model or fictional ECHO record carries its own category regardless of its references.

Run `LKS.Contracts.Tests` in Unity EditMode. S02 owns session initialization and dispatch. S16 owns migration, atomic writes and backup recovery. Developer 1 owns all scene changes.
