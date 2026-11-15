# W12 Mars field loop

Developer 1 owns the scene, its generator and the first-person presentation. Developer 2 owns sample definitions, quest gates and save state.

- Accepted [W11 #28](https://github.com/2dshligang/last-known-state-the-legacy/pull/28) (merge 2c4781d) supplies the ECHO unlock.
- Accepted [S10 #17](https://github.com/2dshligang/last-known-state-the-legacy/pull/17) (merge 411b8dd) defines the specimens.
- Accepted [S11 #18](https://github.com/2dshligang/last-known-state-the-legacy/pull/18) (merge 1445a99) supplies StationSamples and SampleCheckpoint schema 1. [S16 #29](https://github.com/2dshligang/last-known-state-the-legacy/pull/29) supplies disk persistence. Contract API remains S01 1.0.

| Site | Binding | What to do |
| --- | --- | --- |
| A, layered outcrop | sample.layered-mudstone | Aim at the labeled panel. Press E to record context, then E again to collect. |
| B, comparison rock | sample.iron-rich-rock | Same controls. Collect before or after A. |
| C, dust check | quest.sampling | Read the fictional boot-dust clue. This point confirms the sampling quest once both rocks and the lab blank are ready. |
| Lab empty control | sample.procedural-blank | Record its context, then prepare the empty control. W13 will run it in the analyzer. |

Use the saved Station.unity directly. To reproduce placement, save open scenes and run **LKS > World > Generate W04 Station**. Complete ECHO, exit through the powered airlock, and follow either side of the loop. Follow the floor markings back to **AIRLOCK / LAB**. The current quest gate requires all three S11 entries, so prepare the lab blank and return to C to confirm. After restoring a save, the sample prompts read the restored S11 state directly. No pickup animation controls a state transition.

All terrain and field props are original illustrative geometry assembled from the existing station palette. The [verified challenge Resources](../../../Automation/docs/SCIENCE.md) include [Mars Trek](https://trek.nasa.gov/mars/) for labeled source terrain and [CSA youth educator activities](https://www.asc-csa.gc.ca/eng/youth-educators/resources-stem-and-youth-organizations.asp) for age-appropriate activity structure. No Mars Trek layer, measurements, coordinates, textures or copied activity content are embedded. Bedding, alteration and boot dust are fictional scene observations. The S10 links provide [lakebed mudstone](https://ntrs.nasa.gov/citations/20210013268) and [iron mineral](https://ntrs.nasa.gov/citations/20205001823) context. The shelter still shows the historical record offline and the external button opens the [NASA DONKI catalog](https://ccmc.gsfc.nasa.gov/DONKI/events/).

## Validation and next owner

- Unity 6000.6.9f1 W04Verification.Verify checks regeneration, both route directions, 13 aimed anchors, five exterior barriers and preservation of manual content.
- Automation/tools/Invoke-UnityChecks.ps1 passed 81 Edit Mode and 17 Play Mode tests. The sampling test uses the real Station interaction rays, checks all six orders including the lab blank, rejects duplicate pickups and restores an incomplete run through S16. The first-person route test moves the real motor both directions.
- The standard Unity Windows x64 build succeeded. Run a muted walk in that build to validate field-board readability, return signage and the offline shelter panel. With sampling active, turn 180 degrees away from C and check the entire NEXT Exterior sample pickup / Behind you text at 1280x720 and 800x600. The route and Play Mode logs do not establish those visual checks.
- Developer 1 continues with W13. Reuse the existing blank binding and connect the S11 analysis/clean/review actions. Developer 2 coordination is needed if a third collectible exterior specimen is desired.
