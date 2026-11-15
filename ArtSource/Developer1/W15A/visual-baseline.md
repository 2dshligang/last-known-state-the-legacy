# W15A visual baseline

Baseline is accepted main `aec279ff6cc47f10925ed0908b2b56d9cfd21aa2` (W15, PR #39). The Windows player contains `Assets/_Game/Scenes/Station.unity` and was built with Unity 6000.6.9f1, URP 17.6.0. Baseline inventory consists of six interior bays, 192 registered W04 nodes, 13 W04 interaction anchors and 833 serialized scene documents. Existing W01–W03 station modules and the accepted opening, airlock, ECHO, shelter, sample and ending objects were reviewed in place. The W04 material assets and W01–W03 authored prefabs were kept as scene dependencies.

[Baseline player contact sheet](baseline.png) · [800×600 ECHO view](baseline-echo-800x600.png)

The nine views originate from the first-person player camera in the built station. The same camera fixture and quality preset `PC` are used for both builds. The fixture captures at 1920×1080 with FOV 65, near plane 0.055 and far plane 150 under Direct3D 11. ECHO has a paired 800×600 readability check. The capture fixture sets audio volume to zero, closes the existing opening and locks first-person motion while taking stills. It is outside `Assets` in this branch. There is no claim that the screenshots constitute gameplay or a frame-rate benchmark.

## Gaps in the accepted station

| Area / visible surfaces | Treatment | Impact | Integration risk | Art / performance cost |
| --- | --- | --- | --- | --- |
| Arrival corridor, ceiling, baseboards, door pockets | Add three panel variants, service trim, guides and restrained diffuser housings. Keep English room signage and airlock motion. | P0 first impression, navigation | Door clearance and sign overlap | Low, reused modules |
| Power shell and recovery station | Build distinguishable control housings around the accepted interaction faces. Surface relief on the floor. | P0 goal clarity | Control prompts and interactable ray hits | Low, shared URP palette and one reviewed 1K normal map |
| ECHO terminal, signage, projection and cards | Custom terminal shell. Keep the original projection, content and focus behavior. | P0 goal clarity and evidence comprehension | Screen focus and occlusion | Low, original geometry |
| Shelter and seedlings | Keep shutter and safety clearance, add bracket/track housing and cabinet trays. | P1 distinguish systems and choices | Side access and status label | Low, original geometry |
| Analysis, clean blank, report and photo | Add equipment, an empty tray, report station and photo backing. Keep existing scientific context, labels and photograph. | P1 distinguish contamination control and support ending | Report unlock, photo visibility and save | Low, original geometry |
| Exterior ground, island perimeter, routes and sample props | Add layered meshes and rough ground, preserve the two marked legs and sample targets. | P1 route reading and field landmarks | Visible boundary versus inherited colliders | Low to moderate, repeated meshes plus one reviewed 1K normal map |
| Actual sky, light placement, settlement and water history, projection contents, opening and ending UI | Keep accepted content. W16 owns lighting and atmosphere. | Scientific and narrative continuity | High for changing these during this pass | No additions |

W15's accepted handoff reports 22 Play Mode and 85 Edit Mode tests. Those are historical baseline suite totals. W15A's own test runs are recorded separately.
