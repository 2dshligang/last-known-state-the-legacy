# LAST KNOWN STATE: THE LEGACY - automation starter kit

Prepared on 2026-11-14. This is a tested tooling and planning deliverable, **not a Unity game, configured GitHub repository, or verified player build**.

The initial working folder contained no Unity project or Git repository. The user then supplied `2dshligang/last-known-state-the-legacy`; its private repository was cloned at `5f11e94` and contains collaboration documentation and empty ownership folders, not a functioning Unity project. A Unity 6000.6.9f1 executable was found. Two scratch project creation attempts exited 199 during licensing initialization; the licensing client and Package Manager could not be launched. No Unity compilation, tests or player build passed. Git and Node 24.16.0 are available. GitHub CLI, Python and a global .NET SDK were not found; Unity has a bundled SDK whose project compatibility has not been exercised.

The existing GitHub account `chitkid` has push access, but no admin/maintainer permission. Auto-merge is disabled. A protection API response was 404; with these permissions this is not proof that main has no protection. Administrative server configuration remains outstanding.

## Read first

1. [Architecture and playable scope](docs/ARCHITECTURE.md)
2. [Verified challenge and science plan](docs/SCIENCE.md)
3. [Two-PC workflow, CI and merge safety](docs/AUTOMATION.md)
4. [Setup and validation runbook](docs/RUNBOOK.md)
5. [48 ready prompts and their dependency order](prompts/INDEX.md)
6. [Actual validation report](docs/VALIDATION.md)

## What is implemented here

- Official challenge Resources link inventory with source URL and downloaded page hash.
- A real NASA DONKI historical snapshot: 9 records from a fixed September 2017 interval, including a model-derived record.
- A deterministic normalizer preserving observation/model distinctions, instruments and individual source links. It does not invent radiation dose.
- Checksum-pinned, bounded HTTPS downloads with host checks at every redirect, no credential-bearing URLs, no archive execution/extraction, and no overwrites of changed files.
- Five original procedural OBJ placeholders with UV coordinates, dimensions, generation code and checksums. These are simple boxes for blockout, not finished station art.
- Unit tests, source integrity checks and a baseline Unity text/GUID linter.
- A PowerShell Unity test/build wrapper and an inactive GitHub Actions template.
- A plan and task prompts for the missing Unity integration, license audit, authoritative ownership check, merge policy service, release pipeline and visual verification.

## Run locally

From this directory, with Node 22 or newer:

```powershell
node --test tests/*.test.mjs
node tools/verify.mjs
node tools/normalize-donki.mjs
node tools/fallbacks.mjs
node tools/fetch-locked.mjs
```

There are no npm packages to install. Repeated generation verifies existing content; it refuses to replace changed output. To accept an intentional content update, review it in a new output path/version and update the manifest through a PR. Do not delete an existing snapshot to make a failed check disappear.

## Integration layout

Copy this toolkit into `Automation/` **only after the project coordinator identifies the repository and approves its shared-tooling placement**. This packet does not write into either developer's Unity ownership area. Do not replace an existing AGENTS.md, workflow, package manifest, .gitignore or .editorconfig blindly; merge the relevant rules after inspecting the repository.

The Unity CI template is `templates/ci.yml.example`. It is deliberately not an active workflow. It expects the actual Unity project at repository root and this kit at `Automation/`. Review and install it as `.github/workflows/lks-ci.yml` after runner and server protections are ready. Configure real CODEOWNERS accounts; placeholder owners do not enforce anything.

## Known boundaries

The English scan detects Cyrillic; it cannot prove all prose is English, inspect text baked into textures, or inspect runtime-generated TextMeshPro values. The baseline linter is not Roslyn/CSharpier. A manifest check verifies recorded license evidence, not legal ownership. The sample science data is not wired to gameplay. The CI template does not provision a runner, install Unity, activate a license, configure GitHub protections, or enable auto-merge.

Do not call these planned capabilities completed. Use the validation report to distinguish executed checks from required integration work.
