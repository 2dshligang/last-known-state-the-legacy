# S18 automated station route

S18 uses [accepted S17](https://github.com/2dshligang/last-known-state-the-legacy/pull/35), [W14](https://github.com/2dshligang/last-known-state-the-legacy/pull/38), [W15](https://github.com/2dshligang/last-known-state-the-legacy/pull/39), S01 API 1.0 and S16 disk schema 2. The tests are under Developer 2's Tests path.

## Run and interpret the evidence

- Run `LKS.Tests.StationRoutePrerequisiteTests` in EditMode and `LKS.Tests.StationIntegratedRouteTests` in PlayMode. In batch mode use Unity `-runTests -testPlatform EditMode` or `PlayMode`, a separate `-testResults` XML path and a `-logFile`. Do not add `-quit`. Assertions identify the failing command or scene binding.
- Both PlayMode routes load the saved `Assets/_Game/Scenes/Station.unity` and start at the installed W14 opening. Arrival uses the authored approach and scene ray. Graphical PlayMode injects mouse aim and E through the installed InputAction. Batch PlayMode has no locked cursor and uses the same installed ray-submit handler. Power, ECHO, sampling and analysis use public domain commands with a fixed clock and real disk reloads. No test sets quest completion flags.
- The W15 report uses the installed terminal controls to reject a claim of life, accept a supported revision and queue transmission. The positive route opens the photograph through the scene ray, checks exact text `Dad... I did it.` and uses the installed menu to continue the stored finale without a second completion event. The negative route completes the same prerequisites, leaves the report unqueued and verifies that the overlook rejects access after reload.
- The PlayMode runner reports normal test counts and no expected skips. The tests exercise actual screen handlers and scene bindings. They do not independently establish Windows graphics, legibility or real keyboard capture in batch mode.

## Next owner

Developer 2 maintains the route tests and follows up in S19 with the source-use inventory once W18 is accepted. Developer 1 owns these scene bindings and should preserve `quest.report`, `quest.ending`, the report controls and the S16 campaign slot if the UI changes. The world-owned W15 suite separately covers walking to the exterior approach and the restored opening passage.
