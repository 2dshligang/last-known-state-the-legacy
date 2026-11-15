# W18 profile and release verification

## Raw evidence

[profiles.zip](profiles.zip) contains paired release CSVs and JSON summaries, the sun-shadow control and the paired development shader measurements. Each run also includes its original profile markers from the player log. Reproduce the summary with `node summarize-profile.mjs frames.csv player.log report.json`. The JSON includes each run's exact player configuration, finite sample counts, per-room p50/p95/p99, first 16-callback transition statistics and the memory samples.

The sampled poses, renderer settings and commands are in `W18ProfileRoute.cs` and `W18ProfileBuild.cs`. Five room stills were taken after timing finished. The optimized evidence pass added ECHO recorded/model/missing stills after timing; the measured loop itself uses the same positions and operations.

## Unity commands

Temporarily copy `W18ProfileRoute.cs` from this handoff to `Assets/_Game/World/W06/` and `W18ProfileBuild.cs` to `Assets/_Game/Editor/World/` in a disposable worktree. Use the accepted baseline and W18 on the same PC. The build helper sets Frame Timing Stats for that executable and restores the editor setting afterward. See the [Unity API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/PlayerSettings-enableFrameTimingStats.html).

```powershell
$env:LKS_W18_PLAYER = $profilePath
& $unity -batchmode -nographics -projectPath $project -buildTarget Win64 -quit -executeMethod LKS.Editor.World.W18ProfileBuild.Build -logFile $buildLog
& $profilePath -lksW18Profile $captureDirectory -force-d3d11 -noaudio -screen-fullscreen 0 -popupwindow -screen-width 1920 -screen-height 1080 -logFile $captureLog
```

For the diagnostic shader probe set `LKS_W18_DEVELOPMENT=1` at build time. For the baseline sun-shadow control add `-lksW18ShadowControl -lksW18NoShots` to the player invocation. The normal runs omit this control flag. Keep Unity Editor closed while sampling the standalone player.

Remove both temporary helpers from Assets before verifying the ordinary candidate:

```powershell
& $unity -batchmode -nographics -projectPath $project -buildTarget Win64 -quit -executeMethod LKS.Editor.World.W04Verification.Verify -logFile $routeLog
& $unity -batchmode -force-d3d11 -projectPath $project -buildTarget Win64 -quit -executeMethod LKS.Editor.World.W16StationLighting.Verify -logFile $lightLog
& $unity -batchmode -nographics -projectPath $project -buildTarget Win64 -quit -executeMethod LKS.StationReleaseAudit.VerifyContent -logFile $auditLog
& $unity -batchmode -force-d3d11 -noaudio -projectPath $project -buildTarget Win64 -runTests -testPlatform PlayMode -testResults $playXml -logFile $playLog
& $unity -batchmode -nographics -projectPath $project -buildTarget Win64 -runTests -testPlatform EditMode -testResults $editXml -logFile $editLog
$env:LKS_STATION_BUILD_PATH = $playerPath
& $unity -batchmode -nographics -projectPath $project -buildTarget Win64 -quit -executeMethod LKS.Editor.World.W14StationBuild.BuildWindows -logFile $releaseLog
```

```powershell
Push-Location Automation
node --test tests/*.test.mjs
node tools/verify.mjs
Pop-Location
node Automation/tools/unity-policy.mjs
node ArtSource/Developer1/W01/verify_assets.mjs
node ArtSource/Developer1/W15A/verify_assets.mjs
git diff --check
git lfs fsck --objects
```

## Recorded regression results

Unity 6000.6.9f1 / URP 17.6.0. The explicit Station Windows x64 build completed. It loaded the station with audio disabled and exited with code 0. Play Mode passed 24 tests, including two S18 route tests and two ECHO replay tests. Edit Mode passed 86 tests, including four S17 pacing tests. Both suites had zero failures and zero skips. W04 confirmed two byte-identical no-op generations, 192 registered station objects, 1890 original clearance/floor samples, 13 interaction rays, all five exterior boundaries and protection of manually authored content. W16 lighting verification and the S15 static audit passed.

Unity evidence files on the profiling machine are `w18-w04-verify.log`, `w18-w16-verify.log`, `w18-content-audit.log`, `w18-play.xml`, `w18-edit.xml`, `w18-release-build.log` and `w18-release-player.log`. The production executable is in the local `w18-release-win64` folder with its data files.

## Accepted O03 checks

The published branch is based on O03 (#44, `540dfca`). Git compared the Unity Assets, Packages and ProjectSettings before and after the tooling-only rebase and found identical content. The accepted local command passed all phases:

```powershell
./Automation/tools/Invoke-UnityChecks.ps1 -ProjectPath . -UnityPath 'C:/unity/6000.6.9f1/Editor/Unity.exe' -Mode All
./Automation/tests/Test-UnityGate.ps1
```

O03 passed Compile, C# quality, EditMode, PlayMode and Win64 Build. It checked the pinned analyzers on 31 compiler responses against the existing whitespace budget. Node passed all 20 tests, the provenance validator checked 89 files and the asset policy counted 598 files. The seven archived profile JSON files reproduced byte-for-byte from the enclosed CSV and log markers.

O03's generic Build uses the existing build-scene list, whose enabled entry is Bootstrap. The explicit W14 Station build above supplies the profiled game scene and the observed launch evidence.
