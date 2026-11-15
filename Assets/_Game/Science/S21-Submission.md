# LAST KNOWN STATE: THE LEGACY

LAST KNOWN STATE: THE LEGACY is a first-person Mars outpost trainer for ages 7–12, built for the 2026 NASA Space Apps challenge **Build a Junior Astronaut Mission Trainer**. In 2059, eight-year-old Alexei says goodbye to his father Dmitry, who leaves to study Mars. Thirty years later, Alexei returns to the abandoned ASTERION station, restores its systems and finishes his father's research. [Official challenge and Resources](https://www.spaceappschallenge.org/2026/challenges/junior-astronaut-mission-trainer/)

## What players learn by doing

- **Power and life support.** Keep air and water running while the archive, laboratory and transmitter compete for a limited supply. Preview an allocation, apply it and see which loads the station can supply. Battery storage, load priority and reserves are simplified engineering models.
- **Radiation and food.** Use a historical space-weather event to discuss why a crew plans for shelter. The station's warning and shutter timing belong to a fictional drill. A separate decision weighs preserving carried seedlings against powering the lab. Seedling care changes a future supply plan. No crop grows during the game.
- **Evidence and uncertainty.** In ECHO, arrange the fictional station logs and distinguish recorded events in the story from model estimates and a missing interval. In the lab, run an empty control, resolve instrument background and compare two fictional rocks. Finish a report that explains what the evidence supports, offers another explanation and asks for another test. Finding organic molecules alone cannot establish that life existed.

## Space data and educational resources

| Where players use it | What the source supports |
| --- | --- |
| Space-weather archive and shelter training | The game selects the historical NASA CCMC DONKI event `2017-09-04T23:52:00-SEP-001` (record 13000). The record identifies the SOHO COSTEP instrument channel, 15.8–39.8 MeV. MeV here describes the channel's particle-energy range. Mars surface dose is absent. [DONKI](https://ccmc.gsfc.nasa.gov/tools/DONKI/) · [Event record](https://ccmc.gsfc.nasa.gov/DONKI/view/SEP/13000/-1) |
| Seedling planning | The Canadian Space Agency toolkit connects food production in space to astronaut nutrition and invites students to plan how crops could be grown. The game adapts the planning idea using original text and a fictional power trade-off. [CSA toolkit](https://www.asc-csa.gc.ca/eng/youth-educators/toolkits/food-production/) |
| Mudstone context card | Curiosity's SAM instrument detected preserved organic matter in ancient lake mudstones in Gale crater. The in-game layered rock and its result are fictional. [Eigenbrode et al., 2018](https://ntrs.nasa.gov/citations/20210013268) |
| Iron-rich comparison card | Curiosity's CheMin team compared mineralogy in drill samples on and below Vera Rubin Ridge. This motivates comparing rocks in the game. The in-game comparison result belongs to ASTERION. [Rampe et al., 2020](https://ntrs.nasa.gov/citations/20205001823) |
| Empty-control card | Curiosity's SAM team ran an empty cup before analyzing Rocknest material to check instrument background. The game's failed blank and clean rerun teach the control idea through fictional results. [Eigenbrode et al., 2013](https://ntrs.nasa.gov/citations/20130011130) |

DONKI and the CSA food toolkit appear in the challenge's official Resources. The three Curiosity studies are supplementary primary references. The game uses original summaries and links for these references. NASA Open APIs, OSDR, RadLab, Mars Trek and the CSA general STEM hub are listed as references in the project inventory. The only downloaded science dataset used by the Windows gameplay is the pinned DONKI snapshot below. There is no live science query or external service required to finish the campaign.

### Exact dataset

NASA CCMC DONKI, solar energetic particle events queried for September 1–15, 2017. Retrieved **2026-11-14 10:09:24 UTC**. The packaged catalog uses the reviewed `donki-sep-v1` transformation. Eight observations and one model record are retained for provenance. The single gameplay record is identified above. NASA CCMC DONKI and contributing instruments are credited for educational use, with no NASA endorsement. [Exact query](https://ccmc.gsfc.nasa.gov/DONKI-API/get/SEP?startDate=2017-09-01&endDate=2017-09-15) · [DONKI usage guidance](https://ccmc.gsfc.nasa.gov/tools/DONKI/#donki-rules-of-the-road)

| Input or output | SHA-256 |
| --- | --- |
| [Raw JSON](../../../Automation/sources/raw/donki-sep-2017.json) | `f36c8854e40b0429299450815fb74a7eaae1df3573c58c0d7efc269f6c92cb24` |
| [Normalized JSON](../../../Automation/samples/science/donki-events.json) | `64e6ebda0d1d0f1a30e45d8ef35ca7ead69de5891cad82954ce1d121d848fd36` |
| [Normalization script](../../../Automation/tools/normalize-donki.mjs) | `9eb949570da3cc664f86746fead9bfd179a23dfdaac7b7617e0a154d263204c1` |

## Tools, credits and measured limits

- The audited Windows x64 player uses Unity 6000.6.9f1, C# and URP 17.6.0. The team made station models and illustrated story assets, including Blender-authored art. Two imported Poly Haven normal maps are CC0: Metal Plate by Rob Tuytel and Rough Concrete by Dimitrios Savva. Engine and package terms are inventoried for the coordinator's delivery review. [Poly Haven license](https://polyhaven.com/license) · [Source and license inventory](S19-SourceAudit.md)
- The English interface communicates instructions, archive evidence and story through text and images. The integrated play-mode route passed with audio disabled. The Windows smoke observed the opening and entry to the station. [S20 result](https://github.com/2dshligang/last-known-state-the-legacy/pull/49)
- **Playtime target: 20–25 minutes.** The timed human playthrough on the final candidate is pending W21. The earlier W18 benchmark sampled a camera route at 1920×1080 Direct3D 11 on a Ryzen 5 5600, RTX 2060 SUPER and 32 GiB RAM. Its two warmed optimized runs had p95 frame times of 3.970 and 4.161 ms. The first ECHO entry peaked at 36.980 and 39.836 ms. These samples describe the W18 benchmark route. Whole-campaign performance on the final candidate is pending. [W18 measurements and method](../../../ArtSource/Developer1/W18/README.md)

See the [judge route](S21-JudgeRoute.md) for the core choices, and the [evidence handoff](S21-Handoff.md) for build identity and acceptance status.
