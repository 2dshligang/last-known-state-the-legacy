# Installation and verification runbook

## One-time inputs

| Input | Why it is needed | Secret? |
|---|---|---|
| Repository URL or real local Unity project path | Inspect code, history, packages, contracts and scene ownership | No |
| Developer 1 / Developer 2 GitHub accounts and coordinator | Enforce CODEOWNERS and root ownership | No |
| Exact editor version and agreed package lock | Same compilation/import behavior on both PCs | No |
| GitHub admin access for this repository | Configure server protections and CI permissions | Credential stays outside files/chat |
| Unity seats/license and CI hosting choice | Legal activation and available Windows modules | License material stays in secret storage |
| Optional API/client access for Codex and DeepSeek | Run local development/review sessions | Never commit keys |
| Distribution visibility and signing policy | Distinguish internal candidates from public releases | Certificate, if used, is a secret |

Do not ask users to paste tokens, private keys, passwords or Unity license files into prompts. Authentication screens, organization consent, license acceptance, MFA, billing and any CAPTCHA remain human-controlled when required.

## Start sequence

1. Run O01 to inspect the real repository and preserve existing work. Record the baseline build/tests before modifying it. If absent, create a new Unity 6.6 URP project in an explicitly chosen directory; do not pretend this toolkit is one.
2. Assign root/shared ownership. Adopt compatible parts of AGENTS.md, `.editorconfig` and `.gitignore`. Pin Unity and packages. Commit a clean seed with folder metadata before parallel work.
3. Clone on both PCs, authenticate with individual accounts, install matching Editor and Windows support. Use Git LFS for selected binary formats; include `.meta` files in Git. Do not place text scenes, prefabs or JSON under LFS. Confirm LFS objects can be fetched from a clean clone.
4. Developer 2 runs S01; Developer 1 can run W01 art discovery/blockout preparation independently. Only integrate world logic after the contracts merge.
5. Coordinator runs O02-O03; deploy a dedicated runner and protected environment. Install workflow template and bind required checks only after observing their actual names. Test a deliberately failing PR and a skipped Unity job: neither may merge.
6. Run W02/S02 onward in the dependency order from `prompts/INDEX.md`. Prioritize a short complete first pass through the game before art polish.
7. Run O04 to implement trusted merge policy and native auto-merge. First exercise it on a test branch with benign changes; verify denial cases. Do not enable unattended merging by just adding a label.
8. Run O05/O06 for immutable build delivery and bounded repair diagnostics.

## Unity wrapper usage after integration

```powershell
$editor = 'C:\Program Files\Unity\Hub\Editor\6000.6.9f1\Editor\Unity.exe'
./Automation/tools/Invoke-UnityChecks.ps1 -ProjectPath . -UnityPath $editor -Mode EditMode
./Automation/tools/Invoke-UnityChecks.ps1 -ProjectPath . -UnityPath $editor -Mode PlayMode
./Automation/tools/Invoke-UnityChecks.ps1 -ProjectPath . -UnityPath $editor -Mode Build
```

Use the path actually found on that machine and the patch pinned by the project. The wrapper verifies the executable's product version. It refuses an open project's lock file, uses unique output folders, avoids `-quit` for test runs, rejects zero/skipped/failed tests, and checks the player EXE exists. Its process timeout terminates only the launched process tree. It does not activate Unity or configure build scenes. [Unity command-line reference](https://docs.unity.com/en-us/engine/6000.6/manual/unity-editor/command-line-arguments/editor) and [test-runner flags](https://docs.unity3d.com/Packages/com.unity.test-framework@1.4/manual/reference-command-line.html) describe the relevant invocation behavior; reconcile Test Framework version with the actual lockfile.

After a successful scene-generation run, Developer 1 opens the generated scene, checks camera, collisions, scale, URP materials, lighting, UI and all interaction bindings. A headless run cannot establish those results. Execute generation twice, confirm stable IDs/GUIDs and preserve a manually added prop. Verify no manually edited scene objects disappear.

## Asset acquisition

This kit tried a real official Kenney Space Kit download; the response was HTML, not ZIP. It was rejected and not imported. `sources/asset-attempts.json` records the attempt. Five original OBJ blockout pieces in `samples/art` are the implemented fallback. They do not include Unity materials, colliders, prefabs or `.meta` files; Developer 1's import task must create those through Unity.

Allowed discovery candidates from the prior context are Kenney, Poly Haven and NASA 3D resources. Approve files, not entire internet domains as a substitute for licensing. [Kenney's specific Space Kit](https://kenney.nl/assets/space-kit) lists CC0. [Poly Haven assets](https://polyhaven.com/license) are CC0, while its [API access](https://polyhaven.com/our-api) has identification/credit conditions. Discover URLs through actual current pages/API responses, choose 1K-2K textures, retain terms and author credits, and do not execute downloaded scripts or install an unreviewed importer.

Archive extraction is intentionally absent from the current downloader. W01/O02 must add a restricted extraction/import stage if needed: reject traversal, absolute paths, symbolic links, device names, drive letters, excessive expanded size/count, executable scripts and unexpected nested archives. Extract only explicitly selected model/texture/license entries to quarantine. Validate individual hashes, then import the approved subset through Unity. Never auto-extract the entire internet archive into `Assets`.

## Final manual acceptance

- Complete the game on a clean Windows machine, offline and muted. Record actual duration and places where a player needed outside help.
- Test keyboard/mouse, rebinding policy, pause, ALT-TAB, exit/restart, resolution changes and terminal focus.
- Read every objective, archive, scientific card, prompt, report, credit and ending. Include spawned/inactive TMP text, serialized defaults, generated strings and text baked into images. Confirm no Russian or other untranslated content is shipped.
- Listen to or inspect every optional audio asset to confirm no speech; mute it and repeat every puzzle.
- Load each chapter checkpoint; confirm pending commands, battery, evidence, samples and ending status remain consistent.
- Review science claims against specific sources; confirm units, context, uncertainty, archived labels and links. Fail the release if a fabricated value is represented as an observation.
- Measure performance in a player build at 1920x1080 on the target hardware. Capture CPU/GPU percentiles and memory, not only an average FPS screenshot. Record deviations instead of claiming the target was met.

These actions are acceptance work, not an argument against automation. Automated checks reduce repetition; they cannot alone establish readability, visual quality, licensing judgment or how a first-time child understands the game.
