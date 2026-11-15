# S20 release regression

Status: **hold for release**. S19 was accepted in [PR #41](https://github.com/2dshligang/last-known-state-the-legacy/pull/41) at main commit `8e9b24f025ced832cf072cc5857be136f06847ad`. The W19 scene integration, binding inventory and uninterrupted checkpoint route are still required before selecting a release candidate. Results below describe the required evidence for that candidate. The W18 and S19 results are earlier baselines.

## Run on one candidate

Use a clean checkout of the agreed full candidate SHA, hydrate Git LFS, and read its `ProjectSettings/ProjectVersion.txt`. Keep development logs and test XML outside the Windows distribution. Keep the Windows archive together and record its SHA-256 with `Get-FileHash -Algorithm SHA256`. If any player file, notice or packaging file changes, record the final archive hash and repeat the affected checks on that archive before release.

Run [O03 local validation](../../../../Automation/docs/O03-Handoff.md) for compilation, C# quality, Edit Mode and Play Mode. The current O03 generic build can build Bootstrap. Use W20's approved scene list for the release player. For the current Station-only configuration, run the [S19 detailed build and source audit](../../Science/S19-SourceAudit.md), then give W20 and O05 its exact player output for visual review and packaging. If the W20 scene list or player settings differ, audit the detailed BuildReport from that configuration before packaging. A second build is independent evidence until its identity is established.

| Check ID | Evidence needed for a pass |
| --- | --- |
| `edit-mode`, `play-mode` | Complete Unity test XML per platform from the exact SHA. Include S18 route and S19 source fixtures. Count every result, including skips and inconclusive tests. |
| `o03-tooling` | Link to the successful GitHub LKS Tooling run and confirm its head SHA. Record the numeric run ID. Hosted tooling covers repository checks. Unity results come from the local runner. |
| `checkpoint-save-load` | On a disposable Windows profile save at arrival, ECHO/sample progress and before report transmission. Close the executable, relaunch and Continue each checkpoint. Verify objectives, reserves, evidence and pending delivery. Try the corrupt-primary recovery path with a backed-up test profile. Confirm early ending is blocked and the completed ending reopens without applying progress twice. |
| `offline-muted-route` | Disconnect networking and mute the game at OS level. On the extracted Windows player follow New Game through the final photograph using ordinary keyboard/mouse and UI. Confirm the exact text `Dad... I did it.` and record actual route duration. Offline source cards and decisions must work. External links are optional. |
| `english-rendered` | Run the S15 content audit and attach W17-style visual evidence from the player for menus, prompts, archive, terminals, report, subtitles, credits and the ending. Sample on-screen text and artwork at normal and small window sizes. |
| `no-spoken-audio` | Compare the final package audio/media inventory with the S19 manifest and listen to the complete route with audio enabled in a separate check. Confirm every cue needed for progression is also understandable during the muted run. |
| `source-rights` | S19 detailed audit for the packaged player, approved source hashes and science provenance, visible credits and the coordinator's review of included engine and dependency notices. Preserve observation, model and fiction labels. |
| `windows-smoke` | Extract and launch the full archive on a clean Windows x64 PC or VM without Unity. Verify startup, input, settings, New Game, Continue and normal exit. Record actual OS and GPU/API separately from any other machine's results. |
| `distribution-privacy` | Inspect the complete staged ZIP listing and its shipped text, notices, metadata and evidence. Check for credentials, user names, absolute local and network paths, raw test XML, raw Unity logs, saves and debug output. Include only reviewed evidence. O03's `Export-UnityDiagnostics.ps1` generates a limited summary from local logs. If a credential is found, remove the file, rotate it and rebuild the archive. |
| `w20-visual` | Attach W20 screenshots and launch results tied to the SHA and archive. Cover startup, resolution, shader and material appearance, terminal focus, exterior objective banner and final photo/credits. Report any first ECHO entrance hitch against the W18 baseline. |

## Record the acceptance evidence

Keep a private JSON file with schema version 1 and fields `candidateSha` and `checks`. Each check entry has `status` (`pass`, `fail`, `blocked`, or `not-run`) and the full `candidateSha` of its evidence. Attach original evidence privately so another reviewer can confirm the observations. Archive checks also carry `windowsArchiveSha256`. Edit Mode and Play Mode require integer `total`, `passed`, `failed`, `skipped` and `inconclusive` counts. The O03 entry requires `runId`.

```powershell
pwsh -NoProfile -File ./Assets/_Game/Tests/Release/Test-S20ReleaseEvidence.ps1 `
  -CandidateSha $candidateSha -EvidencePath $evidencePath -WindowsArchive $candidateZip
pwsh -NoProfile -File ./Assets/_Game/Tests/Release/Test-S20ReleaseEvidence.Tests.ps1
```

The script prints a limited summary of check statuses and identifiers. Missing checks are `not-run`. Exit 0 means the evidence record is consistent and all required checks are marked pass. Exit 1 means hold or invalid input. Observed gameplay, rights and privacy outcomes still require review of the underlying evidence.

## Severity and handoff

- Block release for a failed or unverified required check, crash/hang, progression or save loss, misleading science, missing rights or sensitive data in the archive.
- Document cosmetic findings with reproduction, screenshot, candidate ID and owner. W18 measured a first ECHO entrance frame up to 39.836 ms. Recheck it on this candidate and record whether interaction or readability is affected. [W18 measurement](https://github.com/2dshligang/last-known-state-the-legacy/blob/48bf3f5ed06f4ba781899914a792501a31eba65b/ArtSource/Developer1/W18/README.md)
- Developer 1 supplies accepted W19 scene and checkpoint bindings, then W20 visual evidence for the exact archive. Maintain `Station.unity`, `quest.report`, `quest.ending` and the documented save slot unless the accepted integration records a replacement.
- Developer 2 owns this matrix, evidence schema 1, save schema 2 (`campaign`) and source credits API 1. W21 owns visual/packaging discrepancies in its scope. O05 supplies the ZIP/hash, reviewed notices, clean-machine and offline download/extract/launch evidence and rollback link. Send both owners the evidence summary and exact missing checks. Until that evidence is collected, the release recommendation remains hold.
