# W20 Windows build configuration and visual smoke

This handoff covers Developer 1 World verification from accepted main `c9a5ac6c14d6be13c46259055027e88b2f26593d`. W19 #50 (`b96fac4`), S19 #41 (`8e9b24f`) and O03 #44 (`540dfca`) are accepted. S20 #49 is the release regression baseline and records a release hold. W19A #52 is a draft and needs new exact-candidate evidence when accepted.

## Configuration

- Use Unity 6000.6.9f1 revision `8c45277fb9c5`, URP 17.6.0 and Windows x64 with `BuildTarget.StandaloneWindows64`. The playable build has exactly one enabled scene, `Assets/_Game/Scenes/Station.unity`, through the explicit World entry `LKS.Editor.World.W14StationBuild.BuildWindows`.
- The accepted generic EditorBuildSettings profile enables `Bootstrap.unity` only. The coordinator's [PR #46](https://github.com/2dshligang/last-known-state-the-legacy/pull/46) covers making O03 call the Station builder and check its success marker. Review that change with the coordinator. This W20 work leaves shared ProjectSettings and Automation under their existing owners.
- The detailed release source audit builds the same scene via `LKS.StationMediaAudit.BuildAndVerify`. If O05 selects that output, preserve its exact archived file set. If O05 selects a different build, audit the detailed BuildReport for that build.
- Preserve `StationSceneEntry.Path` and `Resolve(Scene)`, S01 API 1.0, S16 save schema 2 and slot `campaign`, S19 credits API 1, and `quest.echo-evidence`, `quest.report`, `quest.ending`, and `door.exterior-airlock`.

## Commands

Start with a clean checkout of the agreed SHA, hydrate Git LFS and record the reviewed revision before Unity imports the project.

```powershell
git status --short
git rev-parse HEAD
git lfs pull
$unity = 'C:/Program Files/Unity/Hub/Editor/6000.6.9f1/Editor/Unity.exe'
$env:LKS_S19_OUTPUT = Join-Path $PWD 'Builds/W20-internal'
& $unity -batchmode -force-d3d11 -noaudio -buildTarget Win64 -projectPath $PWD -quit -executeMethod LKS.StationMediaAudit.BuildAndVerify -logFile "$PWD/TestResults/W20/s19-build.log"
```

Launch the resulting `Station.exe` with its entire directory present. This smoke uses `-force-d3d11 -noaudio -screen-fullscreen 0 -screen-width 800 -screen-height 600`. W21 should use ordinary controls and the final packaged executable for its timed run. Test at 1280×720 and 1920×1080 as well.

## Observed smoke on accepted main

The new worktree was clean before the editor imported it. Git LFS hydration and S19's clean Win64 build both exited 0. `TestResults/W20/s19-build.log` recorded `S19_MEDIA_AUDIT_PASS`. The detailed report identifies 27 packed project media assets. `TestResults/W20/source-sha.txt` records the build commit.

The internal output and its relocated copy matched all 195 file hashes. `Station.exe` SHA-256 is `64c123d06925c94979acc4c1a2500d8448b78fce47c290600ba8217c9785e9a7`. This is an executable hash; the W20 release archive still needs its own hash from O05. A scan of 15 text and config files found one absolute development path in Unity's `Station_BackUpThisFolder_ButDontShipItWithYourGame/x86_64/lib_burst_generated.txt`. Exclude the entire backup directory and the three S19 inventory TSVs from distribution. Keep the audit files beside the release evidence.

The relocated player rendered at 800×600 with audio disabled on Ryzen 7 5800X / RTX 4060. Unity logged 6000.6.9f1 revision `8c45277fb9c5` and D3D11. The [farewell](.w20-evidence/.startup-800x600.png) rendered, and Space moved into the station with the [Arrival check objective](.w20-evidence/.arrival-800x600.png). The log reported stripped or unsupported URP Depth of Field and Panini variants; the observed prologue and first-person scenes rendered. Keep the log with the smoke record and check final materials on the release archive.

Escape sent through the Windows automation did not give an observed pause panel in this run. Repeat with a physical keyboard and confirm the terminal and cursor handoff before acceptance. Exact-archive keyboard/mouse route, final photography and credits, other resolutions, and clean-machine offline launch remain open. W19A was tested separately.

## Release handoff

Coordinator and O05 select the accepted SHA and publish the complete Windows player after reviewing the requisite notices, archive privacy, hash and rollback link. Extract on a clean Windows machine and start offline with normal graphics. Tie the build log, screenshots, machine details and S20 manifest to the same archive hash. W19A frames document its separately built interface and do not qualify the W20 archive.

Once the W20 archive is accepted, W21 records a clean-profile, offline and muted run. Start the timer on New Game, show a power trade-off and safe recovery, compare ECHO records and the gap, sample and run a blank, correct a rejected report, and stop on `Dad... I did it.` Record elapsed time and hardware, then archive captures and follow the [S20 regression contract](../Tests/Release/S20-ReleaseRegression.md). S21 #51 can use the same clip references and exact archive SHA. Retain the previous working build while the candidate is reviewed.
