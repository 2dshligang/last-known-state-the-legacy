#requires -Version 7.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../tools/UnityGate.ps1')

function Invoke-Fixture {
    param([string]$Name, [scriptblock]$Check, [string]$ExpectedError)
    $result = 'success'
    try { & $Check } catch {
        if (!$ExpectedError -or $_.Exception.Message -notmatch $ExpectedError) { throw }
        $result = 'failure'
    }
    if ($ExpectedError) {
        if ($result -ne 'failure') { throw "$Name unexpectedly passed." }
        try { Assert-LksGate -Policy success -Unity $result }
        catch {
            if ($_.Exception.Message -notmatch 'Required jobs must succeed') { throw }
            Write-Output "$Name blocked the gate."
            return
        }
        throw "$Name did not block the gate."
    }
    Assert-LksGate -Policy success -Unity $result
    Write-Output "$Name passed."
}

$scratch = New-Item -ItemType Directory -Path ([IO.Path]::Combine([IO.Path]::GetTempPath(), 'lks-gate-' + [guid]::NewGuid()))
try {
    $log = Join-Path $scratch.FullName 'Unity.log'
    $xml = Join-Path $scratch.FullName 'results.xml'
    $missing = Join-Path $scratch.FullName 'missing.xml'
    Set-Content -LiteralPath $log -Value 'Unity fixture completed.'
    Set-Content -LiteralPath $xml -Value '<test-run result="Passed" total="1" passed="1" failed="0" skipped="0" inconclusive="0" />'
    Invoke-Fixture 'Passing test run' { Assert-LksUnityResult EditMode 0 $log $xml }
    Invoke-Fixture 'Missing results' { Assert-LksUnityResult EditMode 0 $log $missing } 'Test results are missing'
    Set-Content -LiteralPath $xml -Value '<test-run result="Failed" total="1" passed="0" failed="1" skipped="0" inconclusive="0" />'
    Invoke-Fixture 'Test failure' { Assert-LksUnityResult PlayMode 0 $log $xml } 'did not pass'
    Set-Content -LiteralPath $xml -Value '<test-run result="Passed" total="1" passed="0" failed="0" skipped="1" inconclusive="0" />'
    Invoke-Fixture 'Skipped test' { Assert-LksUnityResult PlayMode 0 $log $xml } 'were skipped'
    Set-Content -LiteralPath $log -Value 'Assets/Fixture.cs(1,1): error CS1002: ; expected'
    Invoke-Fixture 'Compilation failure' { Assert-LksUnityResult Compile 0 $log } 'compilation or license'
    Set-Content -LiteralPath $log -Value 'warning AD0001: Analyzer threw an exception'
    Invoke-Fixture 'Analyzer failure' { Assert-LksUnityResult Compile 0 $log } 'analyzer failed to run'
    Set-Content -LiteralPath $log -Value 'No valid Unity Editor license'
    Invoke-Fixture 'License failure' { Assert-LksUnityResult Compile 0 $log } 'compilation or license'
    Invoke-Fixture 'Editor process failure' { Assert-LksUnityResult Compile 199 $log } 'exited 199'
    try { Assert-LksGate -Policy success -Unity skipped }
    catch {
        if ($_.Exception.Message -notmatch 'Required jobs must succeed') { throw }
        Write-Output 'Skipped job blocked the gate.'
        return
    }
    throw 'Skipped job did not block the gate.'
} finally {
    Remove-Item -LiteralPath $log, $xml -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $scratch.FullName
}
