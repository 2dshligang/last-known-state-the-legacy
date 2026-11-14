# Actual validation and integration status

Prepared 2026-11-14. This report distinguishes tooling evidence from game acceptance.

## Files created or modified

The starter kit contains architecture/science/automation/runbook documents; 48 complete task prompts and an index; source manifests; the real DONKI raw/normalized data; an explicitly fictional engineering model; five generated OBJ placeholders; Node tools/tests; a Unity PowerShell wrapper; and an inactive full CI template. The repository integration adds `Automation/`, `.github/workflows/lks-tooling.yml` and `docs/AUTOMATION_SETUP.md` on a dedicated feature branch. No developer-owned scene, prefab, gameplay contract or system file is changed.

## Implementation details

The selected official Resources were verified from the directly downloaded challenge page. Nine historical DONKI records were fetched and transformed with source hashes and instrument/model distinctions intact. A Kenney download returned HTML instead of ZIP and was rejected; procedural assets were generated as the permitted fallback. The downloader validates hosts/redirects, file type, size and pinned hash; repeated generation refuses to replace modified files. The active workflow checks this tooling only. Full Unity CI remains an explicit template pending a usable editor and isolated licensed runner.

## Tests actually executed

- Node's test runner on Windows/Node 24.16.0: downloader rejection/caching/integrity cases, path handling, deterministic normalization, original geometry and scientific provenance tamper cases.
- Source manifest and derived-output verification; repeated scientific transformation and procedural generation.
- Real end-to-end replay through the finished downloader against NASA into a fresh workspace directory; the pinned SHA-256 matched.
- PowerShell parser on the Unity wrapper. This checks syntax, not successful Unity execution.
- Official GitHub API verification of checkout v4.2.2, upload-artifact v4.6.2 and setup-node v4.4.0 commit pins.
- Git repository/history/ownership inspection; current authenticated account capability check.
- Two scratch Unity project creation attempts, including one with access outside the initial execution restriction.

## Test results

The first 15-test run passed 11 and failed 4 because temporary test directories were under an inaccessible AppData ancestor. Test scratch storage was moved into the workspace, preserving path/link checks; the next run passed 15/15. Two additional real-data/tamper tests were then added; the final run passed **17/17**, with zero failed or skipped tests. Source checks passed with 1 locked dataset, 5 original models and 9 scientific records. Repeated generation preserved bytes. The locked fetcher verified both the cached snapshot and a fresh real NASA download. Action tag pins matched official repositories. No Unity visual/gameplay result is claimed.

The foundation was pushed in commit `c8ecb45` and [draft PR #1](https://github.com/2dshligang/last-known-state-the-legacy/pull/1) was created. Its first hosted Linux [PR check run](https://github.com/2dshligang/last-known-state-the-legacy/actions/runs/43585970408) and [branch push check run](https://github.com/2dshligang/last-known-state-the-legacy/actions/runs/43585957905) both completed successfully. These are tooling checks, not Unity game acceptance. Subsequent documentation-only commits require their own current-head CI result; verify the PR before merging.

Unity exited **199** twice. The installation lacks the expected `Unity.Licensing.Client.exe` and `UnityPackageManager.exe`; both paths were checked and absent. The log reports licensing initialization failure and inability to connect to a licensing client. No Unity compilation, EditMode/PlayMode suite, Windows build or performance test was completed.

Repository baseline: `5f11e94d6c2653ba09e018d65358859704bc4af3`. A README line-ending-only difference was observed immediately after clone and is deliberately excluded from the feature commit. The existing account `chitkid` has push permission but no admin or maintain permission. Auto-merge is disabled. A main-protection GET returned 404; this does not establish absence of protection with these permissions.

## Unity Editor actions required

Repair/reinstall Unity 6000.6.9f1 through Unity Hub or install another explicitly agreed 6.6 patch on both PCs and CI. Restore the licensing client, Package Manager and Windows build support; authenticate/activate through the permitted Unity flow. Then create/open the URP project, pin actual packages, integrate ownership-specific components, generate/inspect scenes and run the full test/build/player route. No license acceptance or activation has been simulated.

## Integration risks

The toolkit is not a ready game. There are no production Unity contracts/scenes/tests yet. Owner account mapping, licensed runner, C# semantic lint, full runtime source inventory, trusted merge-policy service, branch protections and player release delivery remain unconfigured. A regex scan cannot prove all text is English or all audio lacks speech. Code and source hashes are integrity evidence, not legal certification or visual evidence.

## Remaining issues

Complete the repository administrator configuration and owner mapping; repair the Editor; execute the W/S integration backlog; verify selected additional science datasets and asset licenses; measure 20-25 minute pacing and target-hardware performance. Only then enable full game merge/release gates. Keep full-game auto-merge disabled until those gates exist and their failure cases are demonstrated.

## Suggested Git commit

`chore: add verified science provenance and automation foundation`

The final chat report should state any actual pushed branch, draft PR and CI outcome separately, using their verified URLs. This document does not imply main was merged or a release was published.
