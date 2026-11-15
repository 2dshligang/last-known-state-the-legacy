# W19 Station integration handoff

Developer 1 owns the scene bindings, playable build command and World route tests. Developer 2 owns campaign state, S18 and the service contracts.

- [W18 PR #47](https://github.com/2dshligang/last-known-state-the-legacy/pull/47) is the accepted scene, lighting, and performance baseline at 1650abb.
- [S18 PR #40](https://github.com/2dshligang/last-known-state-the-legacy/pull/40) supplies independent positive and negative route tests. Its handoff is in Assets/_Game/Tests/Route/S18-Handoff.md.
- S01 API 1.0, S13 opening checkpoint 1, S16 disk schema 2, S17 pacing API 1. The production slot is campaign. StationInteraction creates the S16 storage for the player; World route tests inject their own temporary directory and use the usual campaign menu.

## Entry and bindings

In Play Mode, open Assets/_Game/Scenes/Station.unity (scene name Station). For Developer 2's Editor Play Mode tests, load StationSceneEntry.Path with EditorSceneManager.LoadSceneAsyncInPlayMode and resolve StationSceneEntry.Resolve(SceneManager.GetSceneByPath(StationSceneEntry.Path)). Yield one frame before reading Session. For the Windows player, use LKS > World > Build Station for Windows or LKS.Editor.World.W14StationBuild.BuildWindows in batch mode. The existing LKS_STATION_BUILD_PATH variable controls the output path. Bootstrap is the generic Unity profile's baseline scene. The playable build explicitly uses Station.

| Binding | Target and installed view |
| --- | --- |
| W05 / S02 | A single Station root holds StationFirstPerson, SessionBootstrap and StationInteraction. It binds station.spawn, the player camera, input and layout. |
| S03 / S04 / S05 / S06 | quest.arrival, quest.safe-power, target.power-recovery, target.shelter-controls and target.seedling-plan. The power terminal and the two shelter views own presentation. The airlock reads essential supply. |
| W09 / W10 / W11 | quest.echo-evidence binds the archive reader, replay and investigation. The pinned observation is donki.2017-09-04t23-52-00-sep-001. |
| W12 / W13 | sample.layered-mudstone, sample.iron-rich-rock, sample.procedural-blank, quest.sampling and quest.analysis. The runtime laboratory reads S11. |
| W14 / W15 | S13 opening, quest.report and quest.ending create their adapters on the player. W15 shares the W14 portrait and reads the S14 cue. The message is Dad... I did it. |

Run LKS > World > Validate Station Bindings or execute LKS.Editor.World.W19StationValidation.Verify. The validator opens both saved scenes in preview mode. It checks references on all scene components, missing script and prefab sources, unique generated IDs, the accepted route IDs and their exact approach links, and mandatory player, door and terminal references. It leaves the saved scene untouched. The Windows Station builder runs this validation before building.

## Repeatable route and verification

Run LKS.Editor.World.W04Verification.Verify in an isolated batch Editor session. It performs two byte-identical generation passes, checks both exterior directions and all interaction approaches, then exercises preservation of manually authored content. It restores the scene and layout after the test.

Run LKS.Editor.World.StationResearchTerminalTests.NewCampaignRestoresArrivalAndReachesTheEndingThroughTheSavedStation in Play Mode with -noaudio. Also run the accepted S18 route suite. The World test reads the opening, uses the character controller to move between actual approach points, submits the installed interaction rays and uses the panel commands. It saves and reloads the opening, life support, ECHO, collection, active laboratory run, reviewed analysis, queued report and the final photograph. The report queue and transmitter are local fictional simulation. The route does not open a browser or request online mission data. The positive walkthrough checks the exact ending; the S18 negative case checks that a reload cannot unlock the photograph before the report is queued.

The controller route is automated at simulated movement steps. It does not measure human pacing, validate every visible panel, or establish Windows keyboard capture across the whole campaign. For manual release acceptance, disconnect the test PC, start the explicit Windows Station build with -noaudio, use the visible controls through the same checkpoint sequence, and check the final photograph and text. Use the W18 profile instructions for any human-paced 1920x1080 performance measurements.

Observed on Unity 6000.6.9f1: 24 Play Mode tests passed with audio disabled, including both S18 routes, and 86 Edit Mode tests passed. W04 produced identical scene bytes twice and preserved manual objects and references. The W19 validator scanned 2,813 populated Station component references, 13 Bootstrap references, 192 generated IDs and 13 playable anchors. The S15 content audit passed.

A temporary Windows x64 capture driver repeated the same controller route at 1280x720 with audio disabled. The inspected frames showed the farewell, confirmed ECHO sequence, queued report, and the restored final photograph. Local evidence is in TestResults/W19, including PlayMode-final.xml, W19StationValidation.log, W04Verification.log, ProbePlayer.log and 04-ending.png. Those outputs are ignored. The capture driver is excluded from the normal build.

Next owner: Developer 1 W20 owns final player-facing acceptance. Coordinate generic build-profile routing with the automation owner. Developer 2 retains S18 and S19 source-inventory ownership.
