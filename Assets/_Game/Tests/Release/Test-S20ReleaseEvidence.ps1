#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('\A[0-9a-f]{40}\z')][string]$CandidateSha,
    [Parameter(Mandatory)][string]$EvidencePath,
    [string]$WindowsArchive
)

$ErrorActionPreference = 'Stop'
$evidence = Get-Content -LiteralPath $EvidencePath -Raw | ConvertFrom-Json -AsHashtable
if ($evidence.schemaVersion -ne 1 -or $evidence.candidateSha -cne $CandidateSha -or
    $evidence.checks -isnot [System.Collections.IDictionary]) {
    throw 'Expected S20 schema 1 evidence for the requested candidate SHA.'
}

$archiveHash = if ($WindowsArchive) {
    (Get-FileHash -LiteralPath $WindowsArchive -Algorithm SHA256).Hash.ToLowerInvariant()
}
$archiveChecks = @('checkpoint-save-load', 'offline-muted-route', 'english-rendered',
    'no-spoken-audio', 'source-rights', 'windows-smoke', 'distribution-privacy', 'w20-visual')
$requiredChecks = @('edit-mode', 'play-mode', 'o03-tooling') + $archiveChecks
$summary = [ordered]@{}

foreach ($name in $requiredChecks) {
    $entry = $evidence.checks[$name]
    if ($null -eq $entry) {
        $summary[$name] = 'not-run'
        continue
    }
    if ($entry.status -cnotin @('pass', 'fail', 'blocked', 'not-run')) {
        throw "Invalid status for $name."
    }
    if ($entry.status -cne 'pass') {
        $summary[$name] = $entry.status
        continue
    }
    if ($entry.candidateSha -cne $CandidateSha) {
        $summary[$name] = 'candidate-mismatch'
        continue
    }
    if ($name -in $archiveChecks -and (!$archiveHash -or $entry.windowsArchiveSha256 -cne $archiveHash)) {
        $summary[$name] = 'archive-mismatch'
        continue
    }
    if ($name -in @('edit-mode', 'play-mode')) {
        $counts = @($entry.total, $entry.passed, $entry.failed, $entry.skipped, $entry.inconclusive)
        $invalid = $counts.Where({ $_ -isnot [long] -and $_ -isnot [int] })
        if ($invalid.Count -or $entry.total -lt 1 -or $entry.passed -ne $entry.total -or
            $entry.failed -ne 0 -or $entry.skipped -ne 0 -or $entry.inconclusive -ne 0) {
            $summary[$name] = 'invalid-test-counts'
            continue
        }
    }
    if ($name -eq 'o03-tooling' -and "$($entry.runId)" -notmatch '\A[1-9][0-9]{0,19}\z') {
        $summary[$name] = 'missing-run-id'
        continue
    }
    $summary[$name] = 'pass'
}

# Copy only fixed status values and computed or validated identifiers. Keep raw evidence private.
$record = [ordered]@{
    schemaVersion = 1
    candidateSha = $CandidateSha
    windowsArchiveSha256 = $archiveHash
    readyForReleaseReview = @($summary.Values | Where-Object { $_ -cne 'pass' }).Count -eq 0
    checks = $summary
}
$record | ConvertTo-Json -Depth 4
if (!$record.readyForReleaseReview) { exit 1 }
