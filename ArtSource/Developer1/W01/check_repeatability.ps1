param(
    [Parameter(Mandatory=$true)][string]$BlenderPath,
    [Parameter(Mandatory=$true)][string]$ScratchRoot
)
$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$scratch = [System.IO.Path]::GetFullPath($ScratchRoot)
if (Test-Path -LiteralPath $scratch) { throw 'ScratchRoot must be a new directory; no existing content will be overwritten.' }
New-Item -ItemType Directory -Path (Join-Path $scratch 'ArtSource\Developer1\W01') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $scratch 'Assets\_Game\Art') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'Assets\_Game\Art\W01') -Destination (Join-Path $scratch 'Assets\_Game\Art\W01') -Recurse
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'Assets\_Game\Art\W01.meta') -Destination (Join-Path $scratch 'Assets\_Game\Art\W01.meta')
Get-ChildItem -LiteralPath $PSScriptRoot -File | Copy-Item -Destination (Join-Path $scratch 'ArtSource\Developer1\W01')
$generator = Join-Path $scratch 'ArtSource\Developer1\W01\build_assets.py'
$manual = Join-Path $scratch 'Assets\_Game\Art\W01\manual-artist-note.txt'
[System.IO.File]::WriteAllText($manual, 'Manual content must survive generation.')
$manualHash = (Get-FileHash -LiteralPath $manual).Hash

function Snapshot {
    Get-ChildItem -LiteralPath $scratch -Recurse -File | Sort-Object FullName | ForEach-Object {
        $_.FullName + ':' + (Get-FileHash -LiteralPath $_.FullName).Hash
    }
}
$before = @(Snapshot)
for ($run=1; $run -le 2; $run++) {
    & $BlenderPath --background --factory-startup --python-exit-code 1 --python $generator
    if ($LASTEXITCODE -ne 0) { throw "Repeat run $run failed." }
}
if (Compare-Object $before @(Snapshot)) { throw 'Repeat runs changed files.' }
if ((Get-FileHash -LiteralPath $manual).Hash -ne $manualHash) { throw 'Manual content changed.' }

$changed = Join-Path $scratch 'Assets\_Game\Art\W01\floor-4m.obj'
[System.IO.File]::AppendAllText($changed, "# Intentional artist edit for the rejection test.`n")
$beforeFailure = @(Snapshot)
& $BlenderPath --background --factory-startup --python-exit-code 1 --python $generator
if ($LASTEXITCODE -ne 1) { throw 'Expected changed-output rejection with exit code 1.' }
if (Compare-Object $beforeFailure @(Snapshot)) { throw 'Failed generation changed files.' }
Write-Output 'PASS: two repeat runs changed zero files; manual content preserved; edited output rejected before any write.'
