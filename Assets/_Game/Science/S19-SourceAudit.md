# S19 source-use inventory on the W15 baseline

Scope: accepted `aec279f`. This inventory covers the current science references and known source-use boundaries. Release asset coverage and credits integration await the accepted S18 route and W18 Windows build. The current [S18 route PR](https://github.com/2dshligang/last-known-state-the-legacy/pull/40) has no skipped gameplay tests.

## English bibliography and visible use

| Source ID | Citation and attribution | Player-visible use and limits |
| --- | --- | --- |
| `nasa-donki-sep-2017` | NASA CCMC DONKI and contributing instruments, [DONKI Rules of the Road](https://ccmc.gsfc.nasa.gov/tools/DONKI/#donki-rules-of-the-road) | `archive.donki-sep` and the shelter console use the 4 September 2017 23:52 UTC SEP record, NASA record 13000. It provides training context and the archive acknowledgment required for ECHO. The fictional drill and its timing are separate. |
| `ntrs.20210013268` | Eigenbrode et al., 2018. [Organic Matter Preserved in 3-Billion-Year-Old Mudstones at Gale Crater, Mars](https://ntrs.nasa.gov/citations/20210013268). *Science*. | Reference card for `sample.layered-mudstone` in the lab and report. Curiosity's real findings provide context. The ASTERION rock and instrument response are fictional, and organics alone do not establish life. |
| `ntrs.20205001823` | Rampe et al., 2020. [Mineralogy of Vera Rubin Ridge from the Mars Science Laboratory CheMin Instrument](https://ntrs.nasa.gov/citations/20205001823). *Journal of Geophysical Research: Planets*. | Reference card for `sample.iron-rich-rock`. Real mineral analyses provide context for comparison. The fictional station value is not a Curiosity measurement. |
| `ntrs.20130011130` | Eigenbrode et al., 2013. [Detection of Organic Constituents Including Chloromethylpropene in the Analyses of the ROCKNEST Drift by Sample Analysis at Mars](https://ntrs.nasa.gov/citations/20130011130). Lunar and Planetary Science Conference. | Reference card for `sample.procedural-blank`. The mission ran an empty cup to examine instrument background. Passing and failing controls at ASTERION are fictional. |
| `csa-food` | Canadian Space Agency, [Food production on the Moon and in remote areas](https://www.asc-csa.gc.ca/eng/youth-educators/toolkits/food-production/) and [Interplanetary Farming](https://www.asc-csa.gc.ca/eng/youth-educators/activities/fitness-and-nutrition/interplanetary-farming.asp). | Educational context for seedling preservation. The trade-off with lab power is a prototype model, and the plan yields no crop during play. |
| `asterion-prototype-v1` | Last Known State team, `archive.power-model` and the original station implementation. | Simplified engineering assumptions for power and reserves. ECHO's station logs and report transmission are part of the fictional story. |

The NTRS and CSA references supply citations and original explanatory text. Their papers, figures and media are outside the current runtime credits scope. Production credit text for the pinned dataset: **NASA CCMC DONKI. Contributing instruments and linked event records are retained. Educational use. No NASA endorsement.** Use the provider names and author citations above when showing the science credits.

## Imported snapshot

`node tools/verify.mjs` in `Automation` verified one locked download, nine records and five generated assets. The raw snapshot, derived JSON and transform SHA-256 all match `Assets/_Game/Data/StationScienceCatalog.asset`. Transform version: `donki-sep-v1`. The 2017 UTC timestamps are historical; instrument energy thresholds describe channels. Mars surface dose is absent. The normalized snapshot contains eight observed events and one labeled model output.

The current station references one specific event for archive/shelter gameplay. The other eight entries, including the model output, are present in the source snapshot without an identified player-facing card or decision on this baseline. Exclude them from the count of sources actively used in gameplay. The remaining challenge Resources are inventoried in `Automation/sources/challenge-resources.json`. Listing a resource does not establish that the game shipped or used it.

## Release handoff

Developer 1 supplies the accepted W18 build commit and content changes, including the final scene list, Resources, fonts and any replacement assets. Developer 2 compares the actual Windows player dependency set against manifests and rights evidence, verifies pinned package licenses, runs the unmanifested file, missing license, stale transform and false-measurement fixtures, and gives Developer 1 the final credits data. Unknown rights block use in the release inventory. The present baseline does not establish full asset coverage or release approval.
