#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ProjectPath,
    [Parameter(Mandatory)][string]$UnityPath,
    [Parameter(Mandatory)][ValidateSet('Compile','EditMode','PlayMode','Build')][string]$Mode,
    [ValidateRange(1,120)][int]$TimeoutMinutes = 30
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'UnityGate.ps1')
$project = (Resolve-Path -LiteralPath $ProjectPath).Path
$unity = (Resolve-Path -LiteralPath $UnityPath).Path
$versionFile = Join-Path $project 'ProjectSettings/ProjectVersion.txt'
if (!(Test-Path -LiteralPath $versionFile)) { throw 'A real Unity project is required.' }
if (Test-Path -LiteralPath (Join-Path $project 'Temp/UnityLockfile')) { throw 'Project is already open. Use a separate CI checkout.' }
$versionText = Get-Content -Raw -LiteralPath $versionFile
if ($versionText -notmatch 'm_EditorVersion: (6000\.6\.[0-9]+[a-z][0-9]+)') { throw 'Expected a pinned Unity 6.6 editor version.' }
$expectedVersion = $Matches[1]
$revisionPattern = '(?m)^m_EditorVersionWithRevision: ' + [regex]::Escape($expectedVersion) + ' \(([a-f0-9]{12})\)\s*$'
if ($versionText -notmatch $revisionPattern) { throw 'Expected a pinned Unity editor revision.' }
$expectedProductVersion = $expectedVersion + '_' + $Matches[1]
$installedVersion = (Get-Item -LiteralPath $unity).VersionInfo.ProductVersion
if ($installedVersion -cne $expectedProductVersion) { throw "Editor mismatch: project expects $expectedProductVersion, found $installedVersion" }
$runId = [Guid]::NewGuid().ToString('N')
$out = Join-Path $project "TestResults/$runId"
New-Item -ItemType Directory -Path $out | Out-Null
$log = Join-Path $out "$Mode.log"
$xml = $null
$exe = $null
$argsList = @('-batchmode','-projectPath',$project,'-logFile',$log,'-buildTarget','Win64')
if ($Mode -eq 'Compile') {
    $argsList += @('-nographics','-quit')
} elseif ($Mode -eq 'Build') {
    $buildDir = Join-Path $project "Builds/$runId"
    New-Item -ItemType Directory -Path $buildDir | Out-Null
    $exe = Join-Path $buildDir 'LastKnownState.exe'
    $argsList += @('-quit','-buildWindows64Player',$exe)
} else {
    $xml = Join-Path $out "$Mode.xml"
    $argsList += @('-runTests','-testPlatform',$Mode,'-testResults',$xml)
    if($Mode -eq 'EditMode') { $argsList += '-nographics' }
    # Do not add -quit: it can terminate the test runner before completion.
}
$info = [Diagnostics.ProcessStartInfo]::new()
$info.FileName = $unity
$info.UseShellExecute = $false
$info.CreateNoWindow = $true
foreach($argument in $argsList) { $info.ArgumentList.Add($argument) }
$process = [Diagnostics.Process]::Start($info)
if (!$process.WaitForExit($TimeoutMinutes * 60000)) {
    $process.Kill($true)
    throw "Unity timed out. Log: $log"
}
Assert-LksUnityResult -Mode $Mode -ExitCode $process.ExitCode -LogPath $log -XmlPath $xml -PlayerPath $exe
if ($Mode -eq 'Build') {
    Get-FileHash -LiteralPath $exe -Algorithm SHA256 | Format-List
}
Write-Output "$Mode passed. Diagnostics: $out"
