# W17 verification

## Automated Windows visual review

The route produced 25 screens at 800×600 and the same 25 at 1920×1080 with audio muted. It started at the W14 farewell, used a fresh isolated save, opened the installed panels, reached the exact W15 finale, opened English credits and resumed the saved ending. The fixture required keyboard focus to toggle Large in pause, resume exploration, close shelter, select the lab plan in the seedling cabinet, and select the layered-rock lab reading. Page Down moved the expanded longest archive record's scroll position. The fixture uses the same panel handlers as the game and invokes key events directly.

The review corrected clipped text in the seedling choices and enlarged the report reference in Large mode. It moved interaction instructions below the field note, leaving the crosshair and note readable. Modal views keep opaque or dark panel backgrounds. The compact edit controls in power, ECHO and the report remain smaller at 800×600. A full operating-system-keyboard manual run and independent assistive-device testing remain for broader release review.

- [800×600 opening and station controls](small-01.png), [evidence and field](small-02.png), [report and final](small-03.png).
- [1920×1080 opening and station controls](wide-01.png), [evidence and field](wide-02.png), [report and final](wide-03.png).
- [Large pause](pause-large.png), [longest archive entry scrolled](archive-scrolled.png), [field board and prompt](field-sample.png), [final](finale.png), [eight imported rasters](raster-review.png).
- [Rendered player text](player-text.jsonl) is extracted from the actual 800×600 development-player log. The `Development Build` watermark belongs to the evidence runs.

## Commands

`$unity` is Unity 6000.6.9f1 and `$project` is the W17 worktree. In batch mode, give each command an absolute `-logFile` path.

```powershell
& $unity -batchmode -nographics -projectPath $project -buildTarget Win64 -quit -executeMethod LKS.StationReleaseAudit.VerifyContent -logFile $staticLog
$env:LKS_RELEASE_EVIDENCE = $evidenceJson
& $unity -batchmode -nographics -projectPath $project -buildTarget Win64 -quit -executeMethod LKS.StationReleaseAudit.VerifyRelease -logFile $s15Log
& $unity -batchmode -force-d3d11 -projectPath $project -buildTarget Win64 -quit -executeMethod LKS.Editor.World.W17FontInventory.Verify -logFile $fontLog
& $unity -batchmode -force-d3d11 -noaudio -projectPath $project -buildTarget Win64 -runTests -testPlatform PlayMode -testResults $playXml -logFile $playLog
& $unity -batchmode -nographics -projectPath $project -buildTarget Win64 -runTests -testPlatform EditMode -testResults $editXml -logFile $editLog
$env:LKS_STATION_BUILD_PATH = $normalPlayer
& $unity -batchmode -nographics -projectPath $project -buildTarget Win64 -quit -executeMethod LKS.Editor.World.W14StationBuild.BuildWindows -logFile $buildLog
```

For the screenshot and runtime-text gate, use a disposable worktree and temporarily copy this folder's `W17EvidenceFixture.cs` into `Assets/_Game/World/W06` and `W17EvidenceBuild.cs` into `Assets/_Game/Editor/World`. Set `LKS_W17_PLAYER` to an absolute development-build executable path. Run `LKS.Editor.World.W17EvidenceBuild.Build` with `-batchmode -nographics -quit`. Launch each resolution as shown, changing width/height and destination for the second run.

```powershell
& $developmentPlayer -lksW17Evidence $imageDirectory -force-d3d11 -noaudio -screen-fullscreen 0 -popupwindow -screen-width 800 -screen-height 600 -logFile $playerLog
```

Create the external S15 evidence JSON as described in the accepted [S15 handoff](../../../Assets/_Game/Data/S15-Handoff.md), using that player log, the raster review, the muted route log and an empty media array. The diagnostic source files and their Unity metas were removed from Assets before the regular build.

```powershell
Push-Location Automation
node --test tests/*.test.mjs
node tools/verify.mjs
Pop-Location
node ArtSource/Developer1/W01/verify_assets.mjs
node ArtSource/Developer1/W15A/verify_assets.mjs
node Automation/tools/unity-policy.mjs
git diff --check
git lfs fsck --objects
```

## Results

- Unity 6000.6.9f1 and URP 17.6.0. Release Play Mode 24 passed (both S18 route cases included), Edit Mode 86 passed, Node 20 passed. All reported zero failures and skips. The Windows x64 build of Station.unity and its no-audio startup smoke exited with code 0.
- The S15 static audit and both final Windows release-audit inputs passed, with 269 inspected assets and zero findings. The 800×600 runtime excerpt contains 29 log snapshots across 14 surfaces, including all nine required S15 surfaces. The Unicode source scan found no Cyrillic in the scanned game UI, scene, prefab and catalog files.
- Use `w17-content-final.log`, `w17-font-final.log`, `w17-release-s15-small.log`, `w17-release-s15-wide.log`, `w17-release-play.xml`, `w17-release-edit.xml`, `w17-release-build.log` and `w17-release-player.log`. The final diagnostic Windows logs are `release-route-small.log` and `release-route-wide.log`. They record 25 captures each and a successful muted route.
