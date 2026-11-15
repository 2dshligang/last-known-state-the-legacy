# S21 evidence and publication handoff

**Status: working submission copy on accepted S20.** W21 is the direct missing dependency. It follows W20's Windows acceptance and supplies the final build identity, walkthrough, duration, performance and visual evidence. Keep the submission and any delivery recommendation under review until those results are available.

## Versioned references

| Area | Source of truth and owner |
| --- | --- |
| Contracts and saves | Developer 2: [S01 contracts API 1.0](../Contracts/S01-Handoff.md), [S16 save schema 2 and `campaign` slot](../Systems/S16-Handoff.md), [S17 pacing API 1](../Systems/S17-Handoff.md). |
| Scene and presentation | Developer 1: [W19 bindings and route](../World/W19-Handoff.md), integrated at main `b96fac4aa615c30f408e883273113c5bf013ce66`. Use `StationSceneEntry.Path` and `Resolve(Scene)`. Preserve `quest.safe-power`, `quest.echo-evidence`, `quest.report` and `quest.ending`. |
| Data and attribution | Developer 2: [official Resources manifest](../../../Automation/sources/challenge-resources.json), [approved download lock](../../../Automation/sources/downloads.lock.json), [runtime science catalog](../Data/StationScienceCatalog.asset), [S19 use and rights audit](S19-SourceAudit.md) and [W15A source receipts](../../../ArtSource/Developer1/W15A/sources.json). Keep `nasa-donki-sep-2017`, `archive.donki-sep`, the `donki-sep-v1` transform and `StationSourceCredits.All` API 1. Developer 1 owns the visible credits. |
| Automated completion | Developer 2: [S18 route](../Tests/Route/S18-Handoff.md) and [S20 integration review](https://github.com/2dshligang/last-known-state-the-legacy/pull/49). S20 tested head `654b4c37af40770302ac1bf8f25195d76fc8b4b0`, merged to main as `c9a5ac6c14d6be13c46259055027e88b2f26593d` with an identical tree. The report includes 89 Edit Mode and 24 muted Play Mode tests, an automated route to the ending with checkpoint restores, S19 Windows audit and an interactive opening smoke. [Exact-head tooling](https://github.com/2dshligang/last-known-state-the-legacy/actions/runs/43752478399) |
| Performance and release | Developer 1: [W18 benchmark and raw evidence](../../../ArtSource/Developer1/W18/README.md), with final human-route performance pending W21. Developer 2 and coordinator: [S20 exact-candidate matrix](../Tests/Release/S20-ReleaseRegression.md) and [O05 archive requirements](../../../Automation/prompts/O05.md). The S20 internal ZIP was a development-machine smoke artifact. Final archive rights, clean-machine startup and distribution review belong to the release artifact. |

## Completion steps

1. Developer 1 supplies W21's accepted handoff with the full commit, Windows archive SHA-256, hardware and graphics settings, unmodified human-route duration, performance evidence, clean profile and muted/offline conditions. Supply capture timestamps for the five judge beats. Record blocking defects and the previous working build.
2. Developer 2 checks that the same build identities cover the [judge route](S21-JudgeRoute.md), [submission copy](S21-Submission.md), actual credits and the S20/O05 evidence. Recheck the science and asset manifests for additions. Insert W21's observed duration and performance with scope, and keep the earlier W18 benchmark labeled as such.
3. The coordinator updates the shared root README's **Project status** paragraph, which currently describes the Bootstrap setup. Use the verified playable Station entry, link to the submission and judge route, and quote only the measured duration and performance supported by W21. Keep the playtime target distinct from the measured run.
4. The coordinator binds the reviewed copy, captioned demo and instructions to the O05 artifact and notices. The team reviews the package before any separately authorized submission or publication.
