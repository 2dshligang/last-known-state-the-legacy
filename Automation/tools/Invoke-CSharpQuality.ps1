#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ProjectPath,
    [Parameter(Mandatory)][string]$UnityPath
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectPath).Path
$editor = Split-Path -Parent (Resolve-Path -LiteralPath $UnityPath).Path
$policy = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot '../sources/csharp-quality.json') | ConvertFrom-Json -AsHashtable
if ($policy.schemaVersion -ne 1 -or $policy.analyzerSha256.Count -ne 2) { throw 'Invalid C# quality policy.' }
$dotnet = Join-Path $editor 'Data/DotNetSdk/dotnet.exe'
$analyzers = Join-Path $editor 'Data/Tools/BuildPipeline/Unity.Analyzers'
$output = Join-Path $root ('TestResults/' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $output | Out-Null

foreach ($name in $policy.analyzerSha256.Keys) {
    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $analyzers $name)).Hash
    if ($hash -ne $policy.analyzerSha256[$name]) { throw "Unexpected Unity analyzer version: $name" }
}

# Check the compiler invocation recorded by the pinned editor. A fresh CI
# checkout compiles immediately before this step, so there is no Bee cache.
$bee = Join-Path $root 'Library/Bee/artifacts'
$responses = @(Get-ChildItem -LiteralPath $bee -Recurse -Filter 'LKS.*.rsp' -File |
    Where-Object Name -NotMatch '\.dll\.')
if (!$responses.Count) { throw 'No LKS compiler response files found after Unity compilation.' }
foreach ($response in $responses) {
    $arguments = Get-Content -Raw -LiteralPath $response.FullName
    foreach ($name in $policy.analyzerSha256.Keys) {
        if ($arguments -notmatch ('(?m)^-analyzer:"[^"\r\n]*[/\\]' + [regex]::Escape($name) + '"\r?$')) {
            throw "Unity assembly $($response.Name) was compiled without $name."
        }
    }
}

$env:DOTNET_CLI_HOME = $output
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
Push-Location $root
try {
    $sdk = & $dotnet --version
    if ($LASTEXITCODE -ne 0 -or $sdk -ne $policy.sdk) { throw "Unexpected bundled .NET SDK: $sdk" }
    $version = & $dotnet format --version | Select-Object -Last 1
    if ($LASTEXITCODE -ne 0 -or $version -ne $policy.formatter) { throw "Unexpected dotnet formatter: $version" }
    $report = Join-Path $output 'format'
    & $dotnet format whitespace Assets/_Game --folder --verify-no-changes --verbosity quiet --report $report *> (Join-Path $output 'CSharpFormat.log')
    if ($LASTEXITCODE -notin @(0, 2)) { throw "Formatter execution failed. Diagnostics: $output" }
    $entries = Get-Content -Raw -LiteralPath (Join-Path $report 'format-report.json') | ConvertFrom-Json
    $violations = 0
    $regression = $false
    foreach ($entry in $entries) {
        $relative = [IO.Path]::GetRelativePath($root, $entry.FilePath).Replace('\','/')
        $count = @($entry.FileChanges).Count
        $violations += $count
        if ($count -gt [int]$policy.whitespaceBaseline[$relative]) {
            Write-Error -ErrorAction Continue "$relative has $count whitespace findings above its recorded limit."
            $regression = $true
        }
    }
    if ($regression) { throw "C# formatting regressed. Diagnostics: $output" }
    Write-Output "Checked pinned Unity analyzers on $($responses.Count) compiler responses and $violations known whitespace findings."
} finally {
    Pop-Location
}
