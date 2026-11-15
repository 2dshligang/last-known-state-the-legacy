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
   runner. Confirm logs and XML contain no secrets before enabling uploads.
   Set repository variable `LKS_CI_READY=true` after provisioning and copy
   `Automation/templates/ci.yml.example` into `.github/workflows`.
3. Run the full workflow on a passing revision and deliberate compile,
   test, missing results, and license failures in an isolated validation
   branch and runner. Check Win64 player smoke separately. Record the
   actual GitHub check context before changing branch protection.
   The intended job label is **Unity validation gate**.
4. Audit C# formatting with Unity's bundled SDK `8.0.318` and its
   `dotnet format` version `8.3.631206`. From the repository root run
   `<Unity Editor directory>/Data/DotNetSdk/dotnet.exe format whitespace Assets/_Game --folder --verify-no-changes`.
   The current baseline returns exit 2 with existing whitespace violations.
   Coordinate baseline fixes and a pinned semantic analyzer with both code
   owners before installing these as required checks.

The full workflow template compiles scripts, runs both suites, and builds
Win64. Each invocation has a unique output directory. Missing logs, missing
or skipped tests, compilation or license errors, and incomplete builds fail
the Unity job. The aggregate gate requires both jobs to succeed. Outputs are
CI candidates. Public delivery and signing belong to O05.
