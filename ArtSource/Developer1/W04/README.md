# W04 station layout

Base `a14e366` includes W03, S01 through S04 and the W02 import pipeline.
This scene is an original graybox for Unity 6000.6.9f1 and URP 17.6.0. The
material and font adapter from W03 depth-test station signs.

Open `Assets/_Game/Scenes/Station.unity`. The Game view shows the preview camera
at the arrival spawn. Use Scene view to inspect the whole station. The
[interior](station-interior.png) and [exterior](station-exterior.png) captures
come from the saved scene through URP Direct3D 11.

## Route and scene binding

Six bays run along local +Z, with a 4 m bay length, 4 m clear interior width
and 3 m ceiling. The 1.6 m openings and floor guide align on the corridor
center. From arrival, the player can reach the power controls and recovery
station, ECHO archive, shelter and seedling cabinet, laboratory and exterior.
The exterior island gives two connected paths to the sample pickup. Both
paths return through the same interior entrance. The default exterior
boundary measures 11 x 12 m and has continuous walls on all sides except
the connecting doorway. The analysis bench, report desk and photograph
are in the laboratory for the return leg.

`Assets/_Game/World/W04/StationLayout.asset` defines dimensions and spawn.
Use `station.spawn` from the scene manifest for the W05 player start.
`station.preview-camera` gives the editor a saved view. W05 should switch
to its own game camera when the player controller is installed.

| Target ID | Physical location |
| --- | --- |
| `quest.arrival` | Arrival check console |
| `quest.safe-power` | Power control console |
| `target.power-recovery` | Separate recovery control on the return route |
| `quest.echo-evidence` | ECHO archive console |
| `quest.sampling` | Far side of the exterior sample island |
| `quest.analysis` | Laboratory analysis bench |
| `quest.report` | Laboratory report desk |
| `quest.ending` | Photograph stand |

The IDs use the authoritative S01 API 1.0 `StableId` and match the accepted
S03 handoff. `StationInteractionAnchor.TargetId` identifies the physical
target, with `Approach` pointing to a visible standing position. W05 and W06
bind the real controller and S02/S03 session to these anchors. Authoritative
availability, commands and progression stay with their existing services.
The graybox openings are physically open, so layout checks can visit the
complete route before door and life-support rules are connected.

## Safe regeneration

Use **LKS > World > Preview W04 Station** to see the planned counts in Console.
**Generate W04 Station** reads the configuration, then edits existing tracked
nodes in place and adds any missing planned IDs. Existing materials and the
scene GUID are retained. The scene-serialized `StationGenerationManifest`
on `LKS_Generated` owns 135 ordered ID-to-object references. Hand-authored
scene roots and children absent from the manifest are preserved. Unsaved
scene edits, duplicate IDs, missing manifest references and removal of a
plan ID block generation. Review and migrate the old reference before
removing a generated ID. Use a manual child for hand-authored content.

For an isolated batch run, call
`LKS.Editor.World.W04StationGenerator.Generate`, then
`LKS.Editor.World.W04Verification.Verify`. The verifier temporarily tests a
manual prop and a changed bay length, then restores the original scene and
configuration bytes. For a render, set `LKS_W04_INTERIOR` and
`LKS_W04_EXTERIOR` to new PNG paths and call
`LKS.Editor.World.W04Verification.RenderPreview` with Direct3D 11 enabled.

## Evidence

- Unity compiled and imported the scene from accepted W03. Two no-op
  generations left all 135 IDs, Unity object identities and scene bytes
  identical.
- A saved manual root, a child inside the generated subtree and a manual
  reference into ECHO survived a dimension change and a scene reopen.
  Unsaved edits blocked the generator before saving anything.
- Unity physics checked the complete route via 910 clearance and floor
  samples on the default configuration and 932 after a 0.25 m bay extension,
  with eight interaction sightlines and five exterior boundary checks per
  configuration.
- Inspected both scene views. The final render uses W03's depth-tested signs.
  Reloading the layout asset after opening the target scene fixed a preview
  failure caused by Unity unloading the earlier asset instance.
- All 17 project EditMode tests and 17 automation tests passed. The asset
  policy and whitespace checks passed. First-person walking, actual terminal
  interactions, Windows build and framerate measurement are later checks.
