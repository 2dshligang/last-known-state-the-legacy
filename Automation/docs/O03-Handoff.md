# O03 local validation handoff

Base: [O01 #3](https://github.com/2dshligang/last-known-state-the-legacy/pull/3)
and [O02 #36](https://github.com/2dshligang/last-known-state-the-legacy/pull/36).
The project pins Unity `6000.6.9f1` revision `8c45277fb9c5`, URP `17.6.0`,
Input System `1.20.1`, and Unity Test Framework `1.8.0`.

## Acceptance

GitHub runs **Tooling validation only** on pull requests, pushes to main,
and merge groups. It verifies the Unity asset inventory, pinned sources,
repeatable generation, and failure handling with synthetic fixtures.
The team runs Unity compilation, C# quality checks, EditMode, PlayMode,
and Win64 builds locally for gameplay and release changes. A hosted Unity
runner is optional and outside the current O03 acceptance criteria.

## Run Unity before reviewing a build

Use Windows x64 with PowerShell 7, Git LFS, and the pinned Editor with
Windows player support. Activate Unity through its supported workflow
on the development machine. Close the Editor and begin from a clean,
separate checkout of the exact commit being reviewed. Unity imports can
modify tracked metadata and settings. Use the separate checkout to keep
these outputs away from ongoing edits.

From its repository root, run:

```powershell
$unity = 'C:/Program Files/Unity/Hub/Editor/6000.6.9f1/Editor/Unity.exe'
./Automation/tools/Invoke-UnityChecks.ps1 -ProjectPath . -UnityPath $unity -Mode All
```

The command stops at the first failure. It writes separate logs and NUnit
reports under `TestResults/<run-id>/` and the player under
`Builds/<run-id>/`. Build mode calls the World team's existing station
builder and requires its success marker. Keep the entire player directory together. Start
`LastKnownState.exe` and record the commit SHA, Unity version, test totals,
and whether the player reached the opening. Attach the sanitized log or
relevant review evidence to the PR. To produce a log with only allowlisted
diagnostic codes and test counts, run `Export-UnityDiagnostics.ps1` with
`-ResultsPath TestResults` and an output path outside the raw test results.

The C# check verifies the SHA-256 hashes and compiler registration of the
bundled Unity Roslyn analyzers, and the bundled SDK and formatter versions
in `sources/csharp-quality.json` (schema 1). It rejects increases in each
file's whitespace count. A new finding can replace one removed within a
legacy file without raising its count. Reduce those limits as the owners
fix legacy files.

The coordinator maintains these scripts. Each developer runs the local
checks for changes they own. `Automation/templates/ci.yml.example` is
retained as a reference for a future licensed runner. Public delivery
and signing belong to O05.
