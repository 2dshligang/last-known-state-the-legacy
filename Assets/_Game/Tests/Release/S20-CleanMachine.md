# Windows player troubleshooting

## The game does not start

Extract all files from the supplied Windows x64 ZIP into a writable folder. Open the executable from that folder with its `_Data` folder, Unity libraries and other shipped files beside it. If any of those files is missing, extract a fresh copy of the original ZIP. Compare the ZIP's SHA-256 with the value in the release notes if a fresh copy still fails.

If Windows specifically reports a missing `VCRUNTIME140` or `MSVCP140` DLL, install or repair Microsoft's supported **x64 Visual C++ v14 Redistributable**. If it names a game or Unity DLL, re-extract the full ZIP first. [Microsoft's official download](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist/)

## Black screen or graphics initialization error

Update the GPU driver through the GPU vendor or the PC manufacturer. To try a windowed Direct3D 11 launch, open PowerShell in the extracted folder and run the command below. Substitute the actual executable name from the archive. [Unity player arguments](https://docs.unity.com/en-us/engine/6000.6/manual/unity-editor/command-line-arguments/player)

```powershell
.\Station.exe -force-d3d11 -screen-fullscreen 0 -screen-width 1280 -screen-height 720
```

## Continue cannot load the campaign

Try **Continue** once. The game can recover from a valid previous save automatically. If it reports a corrupt or incompatible save, close the game and back up the entire `Saves\player` folder before starting a new campaign. On the S19 baseline the folder is under `%USERPROFILE%\AppData\LocalLow\DefaultCompany\LAST KNOWN STATE - THE LEGACY\`. The campaign's primary file is `slot-campaign.lks` and its previous copy, when available, is `slot-campaign.lks.bak`.

To start with an empty slot, move the backed-up originals out of `Saves\player` with the game closed, then choose **New Game**. Keep any older files if you want help recovering them.

## What to report

Send the build identifier, Windows version, GPU, exact error message and the step at which it happened. For a visual problem, include a cropped screenshot of the game window. Remove account names, local paths and unrelated personal information from any excerpt you choose to share.
