# W15A verification

Unity `6000.6.9f1`, URP `17.6.0`, Windows x64. Tests ran on a Ryzen 5 5600 and RTX 2060 SUPER. See the [visual evidence and W16 handoff](README.md) for camera and performance configuration.

| Check | Result | Local evidence filename |
| --- | --- | --- |
| W15A Preview, Apply and Verify | 50 placements and 20 collider-free prefabs. Clean apply and W04 regeneration left the scene byte-identical. | `w15a-release-preview.log`, `w15a-release-repeat.log`, `w15a-release-w04-generate.log`, `w15a-release-verify.log` |
| W04 integrated scene | 192 registered objects; 13 anchors and route/collider invariants passed. | `w15a-release-w04-verify.log` |
| Player route and UI, Play Mode | 22 passed, 0 failed, 0 skipped. Full new/restore route to the ending, ECHO evidence and focus transitions, power recovery, shelter/seedlings, samples, analysis and report. | `w15a-release-play.xml` |
| Edit Mode | 85 passed, 0 failed, 0 skipped. | `w15a-release-edit.xml` |
| Automation Node suite | 17 passed, 0 failed, 0 skipped. Verification checked 79 files, 5 generated assets and 9 archived science records. | Standard output |
| Asset inventory | Reviewed JPEGs and 20 actual FBX byte hashes and GUIDs verified, with project meta/GUID policy and W01 asset verification. | `verify_assets.mjs`, `sources.json` |
| Windows capture builds | Baseline and W15A built and ran at both resolutions. Nine full-resolution pairs and an 800×600 ECHO pair are in the contact sheets. | `w15a-baseline-build.log`, `w15a-release-capture-build.log`, `before-wide.log`, `before-small.log`, `release-wide.log`, `release-small.log` |
| Windows release build | Successful build of `Assets/_Game/Scenes/Station.unity` with the evidence fixture removed from `Assets`. | `w15a-release-win64.log` |
| Windows player launch | The final player loaded the station with `-noaudio` and exited normally with code 0. | `w15a-release-player.log` |

At 800×600 the ECHO sign was readable in the player capture. Sampling sRGB luminance of its letter region gave about 5.61:1 for the 99th-percentile letter pixel versus median background. This is a spot check on one sign. Visual cues also retain labels and distinct control shapes.

## Executed commands

The Unity batch commands write the above filenames with `-logFile`. In the snippets, `$unity` is the installed editor executable, `$project` is the task worktree, and the other variables name local log, XML, or build paths.

```powershell
& $unity -batchmode -nographics -projectPath $project -buildTarget Win64 -quit -executeMethod LKS.Editor.World.W15AVisualSetup.Preview -logFile $previewLog
& $unity -batchmode -nographics -projectPath $project -buildTarget Win64 -quit -executeMethod LKS.Editor.World.W15AVisualSetup.Apply -logFile $applyLog
& $unity -batchmode -nographics -projectPath $project -buildTarget Win64 -quit -executeMethod LKS.Editor.World.W04StationGenerator.Generate -logFile $generateLog
& $unity -batchmode -nographics -projectPath $project -buildTarget Win64 -quit -executeMethod LKS.Editor.World.W04Verification.Verify -logFile $worldLog
& $unity -batchmode -force-d3d11 -projectPath $project -buildTarget Win64 -quit -executeMethod LKS.Editor.World.W15AVisualSetup.Verify -logFile $visualLog
& $unity -batchmode -force-d3d11 -projectPath $project -buildTarget Win64 -runTests -testPlatform PlayMode -testResults $playXml -logFile $playLog
& $unity -batchmode -nographics -projectPath $project -buildTarget Win64 -runTests -testPlatform EditMode -testResults $editXml -logFile $editLog
$env:LKS_STATION_BUILD_PATH = $playerPath
& $unity -batchmode -nographics -projectPath $project -buildTarget Win64 -quit -executeMethod LKS.Editor.World.W14StationBuild.BuildWindows -logFile $buildLog

node Automation/tools/unity-policy.mjs
node ArtSource/Developer1/W01/verify_assets.mjs
node ArtSource/Developer1/W15A/verify_assets.mjs
Push-Location Automation
node --test tests/*.test.mjs
node tools/verify.mjs
Pop-Location
git diff --check
git lfs fsck --objects
```

Play Mode provides automated route and saved-session evidence. The screenshots use a separate muted camera fixture. Neither runs a manual offline player campaign. Sustained route performance, a complete color-vision pass and per-prop UV2 readiness remain open.
