# S18 automated station route

S18 uses [accepted S17](https://github.com/2dshligang/last-known-state-the-legacy/pull/35), [W13](https://github.com/2dshligang/last-known-state-the-legacy/pull/34), S01 API 1.0 and S16 disk schema 2. Tests live under Developer 2's Tests path. The station scene and gameplay code are unchanged.

## Run and interpret the evidence

- Run `LKS.Tests.StationRoutePrerequisiteTests` in EditMode and `LKS.Tests.StationIntegratedRouteTests` in PlayMode. In batch mode run Unity with `-runTests -testPlatform EditMode` or `PlayMode`, a separate `-testResults` XML path and a `-logFile`. Do not add `-quit`. NUnit assertions identify the failing command, reason or checkpoint.
- The two PlayMode routes start the saved `Assets/_Game/Scenes/Station.unity` with its real `StationInteraction` and `SessionBootstrap`. Arrival uses the authored approach, prompt and scene ray. In graphical PlayMode the fixture injects mouse aim and E through the installed InputAction. Batch PlayMode has no locked cursor and invokes the same scene ray-submit handler using the camera ray. Later chapters use public command APIs and a fixed clock after a real disk reload. No quest completion flags are set by the tests.
- The positive route recovers essential power, pauses for archival reading, checks ECHO replay and source categories, collects in an alternate order, saves a running blank, cleans and analyzes, rejects a claim of life, submits a supported revision, queues the report and reaches `quest.ending` with `Dad... I did it.`. The negative route completes the same prerequisites but leaves the report unqueued and checks the ending stays blocked after disk reload.
- `FullWorldUiRequiresReportAndEndingBindings` reports the blocked UI path explicitly in the PlayMode XML. The existing strict `Automation/tools/Invoke-UnityChecks.ps1` treats skipped tests as nonpassing. Keep that behavior until the full UI route is executable.

## Next owner

[Accepted W14](https://github.com/2dshligang/last-known-state-the-legacy/pull/38) supplies the farewell and arrival. [W15](https://github.com/2dshligang/last-known-state-the-legacy/pull/39) owns the report, transmission controls and photograph presentation. Preserve `quest.report`, `quest.ending`, `StationEnding.QueueAcceptedReport`, and the existing ending cue. Once W15 is accepted, replace the blocked test with an end-to-end route that submits the report and queues transmission through W15, inspects the photograph, observes the final text and verifies that Continue restores it without replaying completion. Developer 2 owns the test and accepts the UI integration once the real route passes.
