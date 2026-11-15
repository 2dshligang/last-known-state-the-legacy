function Assert-LksUnityResult {
    param([string]$Mode, [int]$ExitCode, [string]$LogPath, [string]$XmlPath, [string]$PlayerPath)

    if ($ExitCode -ne 0) { throw "Unity exited $ExitCode. Log: $LogPath" }
    if (!(Test-Path -LiteralPath $LogPath -PathType Leaf) -or
        (Get-Item -LiteralPath $LogPath).Length -eq 0) { throw 'Unity log is missing or empty.' }
    if (Select-String -Quiet -LiteralPath $LogPath -Pattern '\berror CS\d{4}\b', 'scripts have compiler errors',
        'No valid Unity Editor license', 'No valid license found', 'Licensing initialization failed') {
        throw "Unity reported a compilation or license failure. Log: $LogPath"
    }
    if ($Mode -eq 'Compile') { return }
    if ($Mode -eq 'Build') {
        $dataDir = Join-Path (Split-Path -Parent $PlayerPath) (([IO.Path]::GetFileNameWithoutExtension($PlayerPath)) + '_Data')
        if (!(Test-Path -LiteralPath $PlayerPath -PathType Leaf) -or
            (Get-Item -LiteralPath $PlayerPath).Length -eq 0 -or
            !(Test-Path -LiteralPath $dataDir -PathType Container)) {
            throw 'Unity exited without producing the player and data directory.'
        }
        return
    }

    if (!(Test-Path -LiteralPath $XmlPath -PathType Leaf)) { throw 'Test results are missing.' }
    [xml]$results = Get-Content -Raw -LiteralPath $XmlPath
    $run = $results.DocumentElement
    if ($run.Name -ne 'test-run' -or $run.GetAttribute('result') -ne 'Passed') { throw 'Unity test run did not pass.' }
    foreach ($attribute in @('total','passed','failed','skipped','inconclusive')) {
        if ($run.GetAttribute($attribute) -notmatch '^\d+$') { throw "Missing or invalid Unity test count: $attribute" }
    }
    $total = [int]$run.GetAttribute('total')
    if ($total -le 0 -or [int]$run.GetAttribute('passed') -ne $total -or
        [int]$run.GetAttribute('failed') -ne 0 -or [int]$run.GetAttribute('skipped') -ne 0 -or
        [int]$run.GetAttribute('inconclusive') -ne 0) { throw 'Unity tests failed, were skipped, or no tests ran.' }
}

function Assert-LksGate {
    param([string]$Policy, [string]$Unity)
    if ($Policy -ne 'success' -or $Unity -ne 'success') {
        throw "Required jobs must succeed. Policy: $Policy. Unity: $Unity."
    }
}
