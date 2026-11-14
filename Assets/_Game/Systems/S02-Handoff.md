# S02 session bootstrap

Uses the S01 contract API 1.0 from main. LKS.Systems depends on LKS.Contracts and UnityEngine. The S02 tests run in the Editor through the Unity Test Framework. See the [accepted S01 PR](https://github.com/2dshligang/last-known-state-the-legacy/pull/4).

## Developer 1 integration

1. Add SessionBootstrap to an active object owned by the gameplay scene. Assign the object to the scene adapter and reference LKS.Systems and LKS.Contracts in the adapter assembly.
2. When starting a game, call Restart with the real IScienceCatalog, the required record IDs and a factory that creates fresh run-local interaction adapters. Pass an explicit empty record list when the scene has no required records. Catalogs and targets are borrowed references. The bootstrap owns the registry, clock notifications and subscriptions.
3. Show any returned English diagnostics and keep gameplay disabled if the initial start failed. A failed restart preserves the current run. On success, bind to Current, resolve targets by StableId, read Snapshot and subscribe for updates. Snapshot is elapsed game time in seconds and starts at zero.
4. Call Restart for New Game and rebind to Current on success. StopSession or disabling the bootstrap ends the run and drops queued notifications. Release consumer references to the previous run at that point. Startup is explicit on every scene activation that requires a new run.
5. Use the default scaled Unity clock for the scene. Pass an ISessionClock for deterministic iteration. The clock must provide finite, monotonic values within a run. Run initialization, reads, commands and observers on Unity's main thread. Observer callbacks run after Tick via Dispatch in LateUpdate. Subscriptions apply to future ticks and must be disposed by the consumer on disable.

Session timing notifications use subject ID `session.run` and cause ID `cause.clock-advanced`. The fixture IDs `record.fixture` and `target.fixture` are test-only. Use authored IDs from accepted content when integrating. An elapsed-time notification cannot complete a quest.

## Verification and next owner

Run LKS.Systems.Tests and LKS.Contracts.Tests in Unity EditMode. Verify in the scene that a missing catalog produces an English diagnostic and that New Game resets elapsed time and binds fresh adapters. Developer 1 owns scene wiring and UI. Developer 2 adds gameplay rules in S03 and production science content in S10.
