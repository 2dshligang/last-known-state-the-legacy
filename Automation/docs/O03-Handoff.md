# O03 CI handoff

Base: [O01 #3](https://github.com/2dshligang/last-known-state-the-legacy/pull/3)
and [O02 #36](https://github.com/2dshligang/last-known-state-the-legacy/pull/36).
Unity `6000.6.9f1`, revision `8c45277fb9c5`, URP `17.6.0`,
Input System `1.20.1`, and Unity Test Framework `1.8.0` remain pinned
in the project settings and package lock.

The active **Tooling validation only** job runs on pull requests, pushes
to main and merge groups. It checks the Unity asset inventory, pinned sources,
generation stability, and the Unity result validator with synthetic fixtures.
For local reproduction, run `node tools/verify.mjs` and
`node --test tests/*.test.mjs` from Automation, then run
`./Automation/tests/Test-UnityGate.ps1` and
`node Automation/tools/unity-policy.mjs .` from the repository root.

The coordinator and repository administrator own the runner handoff.

1. Provision a disposable, isolated Windows x64 runner with label
   `lks-ephemeral`, exact Unity revision, Windows player support, Git LFS,
   and a legal Unity entitlement. Restrict runner access to this repository
   and the trusted workflow. Review the source of merge groups admitted to
   the runner. Keep licensing credentials out of workflow inputs and published files.
2. Configure the `unity-ci` environment and set `UNITY_EDITOR_PATH` on the
   runner. The artifact contains an allowlisted diagnostic log with diagnostic
   codes and test totals. Raw logs, XML, and formatter reports stay on the
   runner for authorized inspection.
   Set repository variable `LKS_CI_READY=true` after provisioning and copy
   `Automation/templates/ci.yml.example` into `.github/workflows`.
3. Run the full workflow on a passing revision and deliberate compile,
   test, missing results, and license failures in an isolated validation
   branch and runner. Check Win64 player smoke separately. Record the
   actual GitHub check context before changing branch protection.
   The intended job label is **Unity validation gate**.
4. After compilation, run `./Automation/tools/Invoke-CSharpQuality.ps1
   -ProjectPath . -UnityPath <Unity.exe>`. It verifies the SHA-256 hashes
   and compiler registration of the bundled Unity Roslyn analyzers plus
   the exact bundled SDK and formatter versions in
   `sources/csharp-quality.json` (schema 1). It rejects increases in the
   per-file whitespace count. A different finding can replace one removed
   within a legacy file without raising its count. As the owners fix legacy
   files, reduce those counts through review until the baseline is empty.

The full workflow template compiles scripts, runs both suites, and builds
Win64. Each invocation has a unique output directory. Missing logs, missing
or skipped tests, compilation or license errors, and incomplete builds fail
the Unity job. The aggregate gate requires both jobs to succeed. Outputs are
CI candidates. Public delivery and signing belong to O05.

Local verification on base `fc786bf` passed compilation, 86 EditMode tests,
24 PlayMode tests and a Win64 build. The quality check verified the analyzer
registration in 23 compiler responses. A formatting regression fixture was
rejected and the diagnostic export fixture retained the compiler code while
omitting the raw log message. The GitHub API reported zero registered
self-hosted runners for this repository during O03 verification.
