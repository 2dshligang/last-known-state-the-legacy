#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ResultsPath,
    [Parameter(Mandatory)][string]$OutputPath
)
$ErrorActionPreference = 'Stop'
$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('Unity CI diagnostics')
if (Test-Path -LiteralPath $ResultsPath -PathType Container) {
    foreach ($run in Get-ChildItem -LiteralPath $ResultsPath -Directory | Where-Object Name -Match '^[a-f0-9]{32}$') {
        foreach ($mode in 'Compile','CSharpFormat','EditMode','PlayMode','Build') {
            $log = Join-Path $run.FullName "$mode.log"
            if (!(Test-Path -LiteralPath $log -PathType Leaf)) { continue }
            $lines.Add("$mode run $($run.Name)")
            $codes = Select-String -LiteralPath $log -Pattern '\b(?:error|warning) ((?:CS|AD|UNT)\d{4})\b' -AllMatches |
                ForEach-Object { $_.Matches | ForEach-Object { $_.Groups[1].Value } } | Sort-Object -Unique
            if ($codes) { $lines.Add('Compiler and analyzer diagnostics: ' + ($codes -join ', ')) }
            if (Select-String -Quiet -LiteralPath $log -Pattern 'No valid Unity Editor license', 'No valid license found', 'Licensing initialization failed') {
                $lines.Add('Unity reported a licensing failure.')
            }
            if ($mode -notin @('EditMode','PlayMode')) { continue }
            $xml = Join-Path $run.FullName "$mode.xml"
            if (!(Test-Path -LiteralPath $xml -PathType Leaf)) {
                $lines.Add('Test results are missing.')
                continue
            }
            try {
                [xml]$result = Get-Content -Raw -LiteralPath $xml
                foreach ($key in 'total','passed','failed','skipped','inconclusive') {
                    $count = $result.DocumentElement.GetAttribute($key)
                    if ($count -match '^\d{1,9}$') { $lines.Add("$key=$count") }
                }
            } catch {
                $lines.Add('Test results could not be parsed.')
            }
        }
    }
}
if ($lines.Count -eq 1) { $lines.Add('No Unity log outputs were available.') }
$lines | Set-Content -LiteralPath $OutputPath -Encoding utf8
