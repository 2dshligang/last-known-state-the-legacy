#requires -Version 7.0
$ErrorActionPreference = 'Stop'
$checker = Join-Path $PSScriptRoot 'Test-S20ReleaseEvidence.ps1'
$pwsh = (Get-Process -Id $PID).Path
$temp = New-Item -ItemType Directory -Path (Join-Path ([IO.Path]::GetTempPath()) ('lks-s20-' + [Guid]::NewGuid()))
$candidate = 'a' * 40
$evidencePath = Join-Path $temp.FullName 'evidence.json'
$archive = Join-Path $temp.FullName 'candidate.zip'
$fixture = Join-Path $temp.FullName 'fixture.txt'

function Assert-Evidence([string]$Test, [string]$Check, [string]$Expected, [int]$ExitCode,
    [switch]$OmitArchive) {
    $evidence | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath $evidencePath
    $arguments = @('-NoProfile', '-File', $checker, '-CandidateSha', $candidate, '-EvidencePath', $evidencePath)
    if (!$OmitArchive) { $arguments += @('-WindowsArchive', $archive) }
    $output = & $pwsh @arguments
    $actualExit = $LASTEXITCODE
    $report = $output | ConvertFrom-Json -AsHashtable
    if ($actualExit -ne $ExitCode -or $report.checks[$Check] -cne $Expected -or
        $report.readyForReleaseReview -ne ($ExitCode -eq 0) -or ($output -match 'PRIVATE_FIXTURE_MARKER')) {
        throw "Failed: $Test"
    }
    Write-Output "PASS $Test"
}

try {
    Set-Content -LiteralPath $fixture -Value 'Synthetic archive fixture.'
    Compress-Archive -LiteralPath $fixture -DestinationPath $archive
    $digest = (Get-FileHash -LiteralPath $archive).Hash.ToLowerInvariant()
    $evidence = @{ schemaVersion = 1; candidateSha = $candidate; checks = @{} }
    foreach ($id in 'edit-mode', 'play-mode') {
        $evidence.checks[$id] = @{
            status = 'pass'; candidateSha = $candidate; total = 2; passed = 2; failed = 0
            skipped = 0; inconclusive = 0; raw = 'PRIVATE_FIXTURE_MARKER'
        }
    }
    $evidence.checks['o03-tooling'] = @{ status = 'pass'; candidateSha = $candidate; runId = '1234' }
    foreach ($id in 'checkpoint-save-load', 'offline-muted-route', 'english-rendered', 'no-spoken-audio',
        'source-rights', 'windows-smoke', 'distribution-privacy', 'w20-visual') {
        $evidence.checks[$id] = @{ status = 'pass'; candidateSha = $candidate; windowsArchiveSha256 = $digest }
    }

    Assert-Evidence 'complete matching evidence excludes arbitrary text' 'w20-visual' 'pass' 0
    Assert-Evidence 'archive required for Windows evidence' 'windows-smoke' 'archive-mismatch' 1 -OmitArchive
    $evidence.checks['w20-visual'].windowsArchiveSha256 = 'b' * 64
    Assert-Evidence 'visual evidence from another archive is blocked' 'w20-visual' 'archive-mismatch' 1
    $evidence.checks['w20-visual'].windowsArchiveSha256 = $digest
    $evidence.checks['o03-tooling'].candidateSha = 'b' * 40
    Assert-Evidence 'CI from another commit is blocked' 'o03-tooling' 'candidate-mismatch' 1
    $evidence.checks['o03-tooling'].candidateSha = $candidate
    $evidence.checks['o03-tooling'].Remove('runId')
    Assert-Evidence 'CI without a run ID is blocked' 'o03-tooling' 'missing-run-id' 1
    $evidence.checks['o03-tooling'].runId = '1234'
    $evidence.checks['play-mode'].skipped = 1
    Assert-Evidence 'skipped test prevents a pass' 'play-mode' 'invalid-test-counts' 1
    $evidence.checks['play-mode'].skipped = 0
    $evidence.checks['checkpoint-save-load'].status = 'fail'
    Assert-Evidence 'save failure holds the release' 'checkpoint-save-load' 'fail' 1
    $evidence.checks['checkpoint-save-load'].status = 'pass'
    $evidence.checks.Remove('offline-muted-route')
    Assert-Evidence 'missing route evidence remains not run' 'offline-muted-route' 'not-run' 1
}
finally {
    foreach ($file in $evidencePath, $fixture, $archive) {
        if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file }
    }
    Remove-Item -LiteralPath $temp.FullName
}
