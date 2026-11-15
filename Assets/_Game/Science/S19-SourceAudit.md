# S19 source-use and license inventory

Scope: Windows x64 player built explicitly from `Station.unity` on Unity 6000.6.9f1 and accepted W18 (`1650abb`), with [S18 route coverage](https://github.com/2dshligang/last-known-state-the-legacy/pull/40). The regular Unity scene list and the Station release builder serve different entry points. This inventory follows the Station player.

Accepted [W18 measurements and build evidence](https://github.com/2dshligang/last-known-state-the-legacy/pull/47) identify the player scene and render baseline.

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

The current station references one specific event for archive/shelter gameplay. The other eight entries, including the model output, remain in the catalog for provenance and have no player-facing card or decision. Count only the selected event as gameplay evidence. The unused challenge links are inventoried in `Automation/sources/challenge-resources.json`. No challenge videos, paper figures, logos or external audio are used.

## Windows media and software

- The detailed Unity BuildReport lists 27 file-backed project media assets. Two are the W15A Poly Haven normal maps by Rob Tuytel and Dimitrios Savva, with [provider CC0 terms](https://polyhaven.com/license), approved raw and imported SHA-256, asset URLs and attribution in `ArtSource/Developer1/W15A/sources.json`. All three W14 images and 22 meshes are team originals. The checked-in media manifest points to the authorship evidence.
- Twelve additional imported originals were outside the Station package: the eight W01 meshes, three W02 inspection textures and W03 `EchoHousing.fbx`. Kenney was never downloaded, the larger Poly Haven variants were rejected and NASA 3D Resources was reference-only. W18 screenshots and raw performance captures are repository QA evidence.
- The player uses Unity's built-in primitive geometry, UI sprite, splash resource and `LegacyRuntime.ttf`; W17 counted 40 authored TextMesh objects and zero TMP text objects in the Station scene. No project font, audio clip or video was packed. An AudioListener is present.
- Detailed packed package paths identify Collections 6.6.0, Input System 1.20.1, Profiling Core 1.0.3, Render Pipelines Core, Universal and Universal Config 17.6.0, Shader Graph 17.6.0, and uGUI 2.6.0. Package license bytes, versions and provider evidence are pinned in `Data/Editor/StationMediaInventory.json`. Unity credits include the [Unity Software Terms](https://unity.com/legal/terms-of-service/software) and the [Unity Companion License](https://unity.com/legal/licenses/unity-companion-license). Burst 2.0.0 is [built into Unity 6.6](https://unity.com/releases/editor/whats-new/6000.6.0f1). The player also carries Unity-supplied engine and Mono libraries; the report preserves the shipped binary names for notices review. The coordinator must verify applicable third-party notices for the final distributed artifact.

## Verification

Run the executable check for the exact candidate with hydrated Git LFS assets. Set `LKS_S19_OUTPUT` to a clean directory outside Assets, then run Unity with the following arguments:

```text
-batchmode -force-d3d11 -noaudio -buildTarget Win64 -projectPath <project> -quit -executeMethod LKS.StationMediaAudit.BuildAndVerify -logFile <log>
```

A successful command emits `S19_MEDIA_AUDIT_PASS` and writes `packed-assets.tsv`, `build-files.tsv` and `media-use.tsv`. The gate checks imported media coverage and the actual packed media, imported external SHA-256 and rights, packed package licenses and the serialized DONKI catalog against the approved raw, transform and normalized files. Run Edit Mode `LKS.Tests.StationSourceAuditTests` for the unmanifested image, missing rights, stale transform and relabeled observation. Run `node tools/verify.mjs` from the Automation directory as well. Keep a candidate with unresolved source rights out of release.

On the audited baseline, the clean Windows build emitted `S19_MEDIA_AUDIT_PASS`. Unity Edit Mode passed 89 tests with zero failures or skips. The Automation verifier checked 89 files, one locked download, five generated assets and nine science records. All 20 Automation Node tests passed.

## Release handoff

Developer 1 owns the credits presentation. In the finale's credits view, enumerate `LKS.StationSourceCredits.All` (API version 1), using each entry's stable `Id`, English `Attribution` and optional HTTPS `SourceUrl`. Resolve links only when the player chooses to open them, and keep all attribution readable offline. Scroll or paginate the list and preserve the existing ending screen. Run the W17 small-window and rendered-text checks with the expanded credits. The coordinator attaches applicable Unity and dependency notices and reruns the inventory for the exact release archive, including any content added after W18.
