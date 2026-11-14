# Unity setup and O01 handoff

Open the repository root in Unity Hub using **Unity 6000.6.9f1**
(revision `8c45277fb9c5`) with Windows x64 build support and a Unity
license available to your account. Clone with Git LFS installed and run
`git lfs pull` to fetch any large assets in your branch. Open the existing
project in Hub. The first import needs access to `packages.unity.com`.

The package manifest and lockfile pin **URP 17.6.0**, **Input System 1.20.1**
and **Test Framework 1.8.0** for both developers. Keep the package pins and
folder GUIDs when branching. Coordinate upgrades through the repository
review process.

## Check the project

1. Let Unity finish importing packages. Resolve any red Console errors before
   starting work.
2. Open `Assets/_Game/Scenes/Bootstrap.unity`. In Project Settings, verify
   that Graphics selects `PC_RPAsset` and the Standalone quality setting uses it.
3. Enter Play Mode to check the bootstrap camera. The scene is a minimal Unity
   template scene used as the Windows build entry point.
4. Run new gameplay tests from Window > General > Test Runner as they are added.

Run these read-only machine checks from the repository root. Set
`$unityExe` to the executable selected in Unity Hub.

```powershell
$unityExe = 'C:\unity\6000.6.9f1\Editor\Unity.exe'
$editor = Split-Path -Parent $unityExe
git --version
git lfs version
git status --short --branch
git log -1 --oneline
git ls-remote origin refs/heads/main
(Get-Item -LiteralPath $unityExe).VersionInfo.ProductVersion
Test-Path (Join-Path $editor 'Data/Resources/Licensing/Client/Unity.Licensing.Client.exe')
Test-Path (Join-Path $editor 'Data/Resources/PackageManager/Server/UnityPackageManager.exe')
Test-Path (Join-Path $editor 'Data/PlaybackEngines/WindowsStandaloneSupport/Variations/win64_player_nondevelopment_mono')
& (Join-Path $editor 'Data/DotNetSdk/dotnet.exe') --list-sdks
Get-PSDrive -Name (Get-Item -LiteralPath .).PSDrive.Name | Select-Object Name,Free
```

On November 14, 2026 the development machine reported Git 2.54.0,
Git LFS 3.7.1, around 124 GiB free, bundled .NET SDK 8.0.318, and Windows x64 Mono and IL2CPP build
modules. The installed editor under `C:\unity` resolved the existing license
entitlement and started Package Manager under the desktop account.
Repeat the checks on the second developer's machine and confirm there is a
license available in Unity Hub.

The first import and script compilation of this baseline completed under
the desktop account with exit code 0. Unity loaded the bootstrap scene and
reported a successful Windows x64 build. The project currently has no
project-owned automated gameplay tests. See the pull request for additional
verification and outstanding checks.

The built player initialized the engine, Input System and PhysX in a
headless startup check. The harness ended it at the 45-second timeout
because it stayed running. Verify the visible scene and normal exit in
the editor or in a windowed build.

To check import, compilation and a Windows x64 build from PowerShell, close
the project in Unity and run this command from the repository root. Save the
build and log outside the checkout.

```powershell
$project = (Get-Location).Path
$output = Join-Path $env:TEMP ('lks-o01-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $output | Out-Null
$player = Join-Path $output 'LKS-O01.exe'
$log = Join-Path $output 'unity.log'
$arguments = @(
    '-batchmode', '-nographics', '-quit',
    '-projectPath', ('"' + $project + '"'),
    '-buildTarget', 'Win64',
    '-buildWindows64Player', ('"' + $player + '"'),
    '-logFile', ('"' + $log + '"')
)
$process = Start-Process -FilePath $unityExe -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
if ($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $player)) {
    throw "Unity build failed. Exit code $($process.ExitCode). Log at $log"
}
```

## Ownership and handoff

The initial project settings and stable folder metadata are shared setup
work. Developer 1 owns the bootstrap scene and all production scenes.
Developer 2 can branch from the accepted baseline for S01 and begin in
the existing `Contracts`, `Systems` and `Tests` folders. No gameplay API
version exists yet. W01 models and the automation foundation are reviewed
in their respective pull requests.

The URP settings and bootstrap scene come from the installed Unity
`com.unity.template.urp-blank` 17.2.6. See [Unity template notice](UNITY_TEMPLATE_NOTICE.md).
