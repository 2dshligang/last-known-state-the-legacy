# W16 verification

## Observed presentation

Nine matched Windows x64 views at 1920×1080 cover arrival, power, ECHO, shelter, seedlings, laboratory, report, exterior and overlook. All nine views were also inspected from the Unity Editor at 1600×1000. The same player camera poses, FOV 65 and PC quality preset come from the W15A fixture. W15A supplied the accepted visual baseline. The only later base changes before W16 were S18 test files.

In the player the warm arrival and power bays are separated from the cooler ECHO and shelter bays. The direct white hotspot over the shelter no longer dominates the doorway. The rock banks and equipment cast sunlight shadows, the exterior has a dusty gradient and the shared photograph is visible. The ECHO sign remained legible at 800×600. An sRGB luminance spot check in the same letter region measured about 10.89:1 (99th-percentile letter pixel to median background), compared with 5.61:1 in W15A. This figure is for one cropped sign. Deep shadows along some ceiling rails, repetitive corridor modules and repeated exterior rock contours remain visible. A complete low-vision or color-vision check is outside this spot check.

The muted arrival still sampled 160 frames with vSync off at 1920×1080 on RTX 2060 SUPER and Ryzen 5 5600. W15A p50/p95 were 2.997/11.868 ms with 328,892,646 bytes allocated; the final W16 capture reported p50/p95 of 5.340/82.542 ms with 334,299,366 bytes allocated. A repeat with Unity Editor closed reported 2.232/9.047 ms and 325,714,150 bytes. The tail spike was not reproduced in that repeat. W18 still needs repeated traversal profiling. These fixed-camera samples do not establish gameplay frame rates or GPU cost outdoors.

## Commands

`$unity` is Unity 6000.6.9f1, `$project` is the W16 worktree, and the log/XML names are listed below.

```powershell
& $unity -batchmode -nographics -projectPath $project -buildTarget Win64 -quit -executeMethod LKS.Editor.World.W16StationLighting.Apply -logFile $applyLog
& $unity -batchmode -nographics -projectPath $project -buildTarget Win64 -quit -executeMethod LKS.Editor.World.W04Verification.Verify -logFile $routeLog
& $unity -batchmode -force-d3d11 -projectPath $project -buildTarget Win64 -quit -executeMethod LKS.Editor.World.W15AVisualSetup.Verify -logFile $artLog
& $unity -batchmode -force-d3d11 -projectPath $project -buildTarget Win64 -quit -executeMethod LKS.Editor.World.W16StationLighting.Verify -logFile $lightLog
& $unity -batchmode -force-d3d11 -noaudio -projectPath $project -buildTarget Win64 -runTests -testPlatform PlayMode -testResults $playXml -logFile $playLog
& $unity -batchmode -nographics -projectPath $project -buildTarget Win64 -runTests -testPlatform EditMode -testResults $editXml -logFile $editLog
$env:LKS_STATION_BUILD_PATH = $playerPath
& $unity -batchmode -nographics -projectPath $project -buildTarget Win64 -quit -executeMethod LKS.Editor.World.W14StationBuild.BuildWindows -logFile $buildLog
```

The [W15A replay fixture](../W15A/W15AViewCapture.cs) was temporarily installed under `Assets/_Game/World/W06/` for the capture build, then removed before the regular build. The editor preview used the same poses and the existing `W04Verification.Capture` URP render request in a temporary editor helper. For a new paired Windows recording, follow the [W15A capture steps](../W15A/README.md#reproduction) in a disposable tree.

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

## Evidence

W04 generation was byte-identical on two successive runs. Room, route and authoring checks covered 1,890 clearance/floor samples, 13 interaction rays and five exterior boundaries on the accepted layout. The W04 manual-content fixture also passed. The modified scene has the same 936 serialized documents, with 17 lighting/editor-camera records changed. The W15A pass verified 50 visual renderers and 20 collider-free prefabs. W16 verified seven lights, a supported sky shader, fog and the skybox camera. Automation passed 17 tests on the Unity QA base. After rebasing onto O02, the current Node suite passed 20 tests and the validator covered 83 files and 9 archived science records. Git confirmed identical Assets, Packages and ProjectSettings before and after the tooling-only rebase.

Local logs are `w16-apply-final.log`, `w16-w04-verify.log`, `w16-w15a-verify.log`, `w16-final-verify.log`, `w16-editor-emission.log`, `w16-capture-emission-build.log`, `w16-final-player-wide.log` and `w16-final-player-small.log`. The repeat is in `w16-repeat-player-wide.log`.

The Unity Play Mode run with audio disabled passed 24 tests, including both accepted S18 integrated-route cases. Edit Mode passed 86 tests. Both Unity suites and the Node suite reported zero failures or skips. The standalone Windows x64 build succeeded with Station.unity and without the capture helpers. Its window opened with audio disabled, loaded the game and exited with code 0. Build and player logs contained no shader errors. The final release evidence is in `w16-play.xml`, `w16-edit.xml`, `w16-release-win64.log` and `w16-release-player.log`. Route exploration in the standalone player was sampled with the camera fixture. An input-driven manual Windows run, outdoor GPU profiling and sustained 60 FPS remain to be verified.

The final repository audit found 314 unique Unity metadata GUIDs and checked 592 asset/prefab/scene records with the Unity policy. The W01 and W15A content hashes verified. The new lighting assets are plain text. Existing LFS assets passed the hash checks.
