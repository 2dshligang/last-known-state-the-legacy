# Verified challenge, provenance and scientific mechanics

## Official selection

The selected challenge inferred from the referenced conversation is [Build a Junior Astronaut Mission Trainer, 2026](https://www.spaceappschallenge.org/2026/challenges/junior-astronaut-mission-trainer/). The official page was retrieved directly on 2026-11-14 after the web reader returned HTTP 502. Its server-delivered content includes the complete objectives and Resources. The link inventory and page checksum are in `sources/challenge-resources.json`. The page metadata reports last publication on 2026-11-13.

Its audience is ages 7-12. It asks for a working outpost-management learning experience with engineering trade-offs, including life support, shielding, power, food and communications. Archived embedded data is acceptable when clearly identified. Our narrative design is a proposed interpretation, not an official endorsement or an award guarantee.

The similarly named [nasaspaceappschallenge.org](https://www.nasaspaceappschallenge.org/) is the Houston event site. Use the global challenge page as the authoritative source of the task and Resources.

## Actual Resources inventory

| Official resource | Planned role | Boundaries |
|---|---|---|
| [NASA Open APIs](https://api.nasa.gov/) | Discover official public interfaces | A portal entry does not guarantee every historical API remains available; verify endpoint response and terms. |
| [DONKI](https://ccmc.gsfc.nasa.gov/tools/DONKI/) | Archived event timeline; compare event time, observation and model output | Do not convert an event class, particle energy channel or event count into Mars surface dose. |
| [OSDR](https://osdr.nasa.gov/bio/repo/) | Select one relevant published study and its metadata for an evidence card | Preserve organism, instrument and experimental context; no human-health inference from an unrelated experiment. |
| [RadLab](https://visualization.osdr.nasa.gov/radlab/gui/data-api/) | Select a documented radiation series and explain exposure units | Preserve detector, location, units and calibration; ISS readings are not Mars surface measurements. |
| [Mars Trek](https://trek.nasa.gov/mars/) | A small terrain/image subset with layer metadata | Record projection, resolution, coordinates and layer source. Do not bulk-download the portal. |
| [CSA STEM resources](https://www.asc-csa.gc.ca/eng/youth-educators/resources-stem-and-youth-organizations.asp) | Age-appropriate learning structure | Reference resource; review rights before copying images or educational material. |
| [CSA food-production toolkit](https://www.asc-csa.gc.ca/eng/youth-educators/toolkits/food-production/) | Explain food/water/power trade-offs | No instant crop growth. Use original English explanations and review individual media rights. |

The official page also links the OSDR search example `https://osdr.nasa.gov/osdr/data/search?term=KEYWORD`. These are seven top-level resources, not seven downloaded datasets. This kit downloads one specific DONKI snapshot; the other entries are references pending dataset selection and rights review.

## Additional primary sources

For Mars geology, follow Mars Trek layer metadata to the original mission/PDS product, and use specific peer-reviewed papers or NASA mission explanations. [NASA's explanation of Martian organic material](https://www.nasa.gov/news-release/nasa-finds-ancient-organic-material-mysterious-methane-on-mars/) supports the distinction between organics and proof of life. This is an additional primary source, **not asserted to be a listed challenge resource**. Older papers can establish historical findings; they must not be presented as the latest result without a fresh search.

Every proposed additional source gets `relationship: supplementary-primary`. An asset library is `art-only`, never scientific evidence. A source in the Resources list still requires inspecting its own data-use conditions. [NASA media guidelines](https://www.nasa.gov/nasa-brand-center/images-and-media/) distinguish NASA content, third-party works, marks and identifiable people. Do not label everything hosted by NASA CC0 or treat a citation as redistribution permission.

## Three independent kinds of evidence

1. `Observation`: an instrument observation or documented published finding. Keep original units, uncertainty, location, instrument, quality flags and date.
2. `EngineeringModel`: explicit assumptions and equations used to simplify station operation. Cite supporting engineering sources where used; separately label fictional capacity values, time compression and tuning.
3. `Fiction`: ASTERION, Alexei, Dmitry, the 2059 accident, the 2089 expedition, fictional sample locations and reconstructed dialogue. Never attach a NASA source as if NASA measured these events.

The DONKI transform uses `ObservedEvent` and `ModelOutput` within its archived catalog. A model output must not be promoted to an observation during import. `dose: null` is intentional. Null is not zero and must never be silently filled with a plausible number.

## Implemented snapshot

`sources/raw/donki-sep-2017.json` contains the bytes returned by NASA for a fixed query from 2017-09-01 to 2017-09-15. Its hash, actual retrieval timestamp, URL and conditions are in `sources/downloads.lock.json`. `samples/science/donki-events.json` preserves all nine records, sorted deterministically. One instrument label begins with `MODEL:` and remains `ModelOutput`. Multiple instruments can report related events; record count is not a count of independent storms.

This snapshot can drive a station **training archive**: a player orders events and compares the recorded timestamp with a simulated stale display. The scenario uses a selected archived sequence as an educational example. It is not evidence that a 2017 Earth-near event struck ASTERION in 2059. Any compression from historical time to game time is a separate, versioned model transform. [DONKI's own documentation](https://ccmc.gsfc.nasa.gov/tools/DONKI/) describes research/prototyping limitations and updated URLs.

## Data pipeline

`official Resources -> selected source + terms -> raw snapshot -> SHA-256 lock -> documented transform -> reviewed runtime dataset -> Unity importer -> scientific card -> gameplay decision -> report citation`

Use immutable snapshots; network refreshes produce candidate diffs. Build jobs never fetch changing science. The shipped game works offline, requires no NASA key, and displays the archived date. Cache failures preserve the previous verified snapshot; missing required science blocks a scientific release. A procedural rock may replace art, but fabricated measurements must never replace missing data.

Each record should include:

```text
id, provider, title, sourceUrl, finalDownloadUrl, challengeUrl,
relationship, datasetId/DOI/productVersion, retrievedAtUtc,
observationStartUtc, observationEndUtc, location, instrument,
units, uncertainty, qualityFlags, licenseId/licenseUrl,
licenseEvidenceHash, attribution, approvalScope, rawSha256,
transformVersion, transformParameters, derivedSha256,
runtimeAssetGuid, playerFacingUse, limitations, reviewStatus
```

Unknown values stay null with an explanation. A retrieved hash is an integrity record, not proof of rights. A first-time download is quarantined until the source/terms/format are reviewed; the repeat downloader only accepts an already reviewed hash. Store raw scientific provenance outside `Assets`, and import only a small reviewed English runtime subset.

## Mechanics that use evidence

| Decision | Input | Actual effect | Scientific limit |
|---|---|---|---|
| Restore air before archive analysis | Explicit model: finite generation, battery and loads | Selected loads alter charge and available lab operation | Station watts are labeled game assumptions unless supported by a cited design. |
| Check an old controller reading | Observation timestamp, modeled forecast flag, fictional telemetry age | Player rejects stale input and selects a safe fallback | ECHO cannot reconstruct information that was never logged. |
| Respond to a training radiation event | Archived DONKI record and separately authored scenario timing | Shelter preparation interrupts analysis and changes power priorities | No medical dose prediction; passive shielding consumes no continuous power. |
| Preserve seedlings or run analysis immediately | Cited food-system explanation plus explicit model | Future supply plan/seedling viability state changes; a recoverable cost delays analysis | Food does not grow in minutes. The 30-year abandoned greenhouse is dead; viable starter samples arrive with Alexei. |
| Classify three Mars analog samples | Source-backed mineral/context evidence, blank/control, fictional specimen IDs | Sample confidence and report wording depend on actual evidence choices | No invented instrument spectrum or concentration labeled NASA. |
| Queue a message while finishing locally | Cited or explicitly modeled communication delay | Player continues research; later status arrives as text | Do not present a fixed universal Earth-Mars delay. |

Maximum scientific value comes from evidence affecting choices and conclusions, not from maximizing file volume. Target three substantive evidence chains for the first complete game: archived event, geological interpretation, and one life-support/food engineering reference. Add a real RadLab series only when units and context are understood.

## Research conclusion

The successful report should separate observations, interpretation, alternatives and limits. A defensible result is an ancient environment compatible with preservation of organics and a justified follow-up sampling plan. It does not establish life. A wrong claim should produce a concise explanation and a chance to revise the report, not a dead end. Completion honors Dmitry's work through careful science rather than an invented discovery.
