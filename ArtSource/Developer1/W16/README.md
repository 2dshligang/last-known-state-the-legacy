# W16 station lighting and silent atmosphere

Unity QA base: `5e12edafc013f9ca7ce20b5d2e2380064ceff775`. W15 (#39), W15A (#42) and the S18 integrated route (#40) are in that base. The branch was rebased onto O02 (#36, e3ed231) before publication. The Unity content is byte-identical to the tested game. The active Unity and URP pins are 6000.6.9f1 and 17.6.0.

## Station setup

W16 configures `Assets/_Game/Scenes/Station.unity` through the existing W04 manifest and generator. The six `bay.*.light` managed objects use wide shadowless spotlights with distinct warm and cool settings. The stable IDs are `bay.arrival.light`, `bay.power.light`, `bay.archive.light`, `bay.shelter.light`, `bay.laboratory.light` and `bay.exit.light`. The lower-intensity shelter light is placed away from the roof panel that clipped to white in W15A. Existing camera and light scene references are used for the sky and lighting. The directional `exterior.sun` provides one shared set of soft shadows. The island, regolith and ridge prefabs cast shadows in the sunlight, retaining their mesh and prefab IDs. Trilight ambient supplies fill in dark areas. The sky is an original procedural URP gradient with a soft halo. It depicts an illustrative Mars atmosphere. The scene uses subtle exponential-squared fog (density 0.013 in Unity units), fixed room colors and a restrained emissive material on the ceiling and seedling diffusers.

S01 API 1.0 and S16 save schema 2 apply. The existing W04 node IDs and 13 interaction anchors, including `quest.echo-evidence`, `quest.report` and `quest.ending`, bind the same objects. Lighting uses scene-authored settings, independent of the simulated power service. The opening, science cards and final cue use the accepted systems and views. Current lighting uses six shadowless practicals and one shadow-casting sun. This pass uses no generated lightmaps, as the W15A models do not yet have verified lightmap UVs.

No external art, audio or other media was added. The shader and its color settings are project originals. The accepted quiet route remains legible with audio disabled.

## Reapplying and inspecting

The scene is assembled in the commit. **LKS > World > W16 > Apply Station Lighting** routes through the existing W04 generator. **Verify Station Lighting** checks the seven lights, the sun direction, the assigned shader and the player camera's sky settings. It can be run in a batch editor with Direct3D 11 and with the normal graphics path enabled. The added rendering remains scene-local through the active station settings and the camera instance override.

- The [W15A player baseline](../W15A/after.png) and [W16 Windows player views](player.png) use the same nine camera poses and the `PC` quality preset. [W16 ECHO at 800×600](echo-800x600.png).
- [The nine Unity Editor views](editor.png) use the same poses at 1600×1000. The photograph texture is assigned at runtime, so its player view is the evidence for the finished picture.
- Detailed steps, tests and exact local log names are in [verification.md](verification.md). The [scene identity audit](scene-integrity.json) records changes by serialized object ID.

## Handoff

Next owner is Developer 1 for W17. Its S15 dependency is accepted at `fcdd786` (PR #23). Developer 1 and Developer 2 follow with the W18 performance pass. Check text and warning contrast during live focus/pause flows in the darker bays, and profile the complete traversal and exterior directional shadows at 1920×1080. Review the `PC` preset, six stable practical IDs and `exterior.sun` before adding lights. Terminal labels and the outdoor rock repetition are described in the visual review; the existing terminal layout remains the reference for the final polish.
