# W03 station kit

Base `450a565` includes accepted W02 and S01–S03. This original graybox kit
uses Unity 6000.6.9f1 and URP 17.6.0. No external content was used.

Open `Assets/_Game/Scenes/W03CorridorKit.unity` and select the Game view for
the inspection camera. The sample contains two corridor bays and one copy
each of the airlock, utility cabinet, lab workstation, ECHO archive terminal,
and shelter. Open and closed airlock views and the far bay are shown in the
[open preview](corridor-open.png), [closed preview](corridor-closed.png) and
[far bay preview](corridor-far-bay.png).

## Authoring

- Use `module-inventory.json` for prefab paths, nominal dimensions, usage and
  attachment points. The six prefab IDs use the S01 API 1.0 `StableId` type.
  IDs identify module types. For W04 scene placement, assign separate unique
  instance IDs and bind production interactions to the accepted S03 target IDs.
- Align each bay's `SnapStart` to the previous bay's `SnapEnd` without scaling.
  Each bay advances 4 m along local +Z. Floor level is Y=0, inner width is 4 m,
  and ceiling height is 3 m. Line up the airlock's zero-depth snap plane with
  a seam. Its clear opening is X=-1.05 to +0.35 m, Y=0 to 2.2 m, so the walk
  line through the opening is X=-0.35 m. The other four modules are placeable
  wall-side units with a zero-depth origin anchor and independent InspectPoint.
- The collision layer is Unity's existing Default, index 0. Collision is
  non-trigger BoxCollider geometry parented outside the visual variants.
  The leaf's collider follows its sliding root. The scene's end wall belongs
  to the sample. W04 can place a continuation at the final bay's SnapEnd.
- The airlock leaf moves 1.55 m along local +X on a track in front of the
  frame. Its pivot is centered horizontally on the leaf at floor level.
  `AirlockPresentation.ApplyOpen` positions the leaf and refreshes the label
  from a caller-supplied state. Gameplay ownership, prompts and animated
  traversal remain with the accepted systems and the later W tasks.
- Both the airlock and ECHO prefab have paired authored and primitive visual
  variants. `StationModule.TrySetFallbackVisual` toggles them without moving
  the module, collider or anchors. `InspectPoint` is a visual attachment,
  with no interaction logic in W03. For ECHO, the separate screen mesh faces
  local -Z and has a matching surface in the fallback visual.
- The palette uses dusty stone, warm shell panels, matte metal and ochre
  guides. The technical panels have a muted blue accent. English text gives
  open/closed state on the airlock and identifies the utility, archive, lab
  and shelter. The text shader depth-tests labels against station geometry.

## Sources and reproduction

The three editable Blender files are in this directory and their FBX
exports are in `Assets/_Game/Art/W03/Models`. W03 uses Blender 5.3.0
(`02f0899d78e3`) and reuses the accepted W01 export settings verified by W02:
one unit per meter, unit conversion through FBX units, Y up and -Z forward,
triangulated output, no animation. `mesh-spec.json` supplies named parts and
material slots in Unity coordinates to the Blender generator and Unity's
fallback builder. Imported normals, UV0 and generated secondary UVs are
checked against the part dimensions. See `mesh-hashes.json` for all export
settings and file digests.

```powershell
$blender = 'C:\Program Files\Blender Foundation\Blender 5.3\blender.exe'
& $blender --background --factory-startup --python-exit-code 1 --python ArtSource/Developer1/W03/build_station_meshes.py
```

In a clean output tree the Blender tool generates all six binary files.
On the existing kit it verifies the recorded source and output hashes.
Use a new export location for revised geometry, review the result and
preserve Unity metadata when adopting the revision. The Blender extension
cache write warning in the sandbox did not affect model generation.

Use **LKS > World > Create W03 Station Kit** to fill missing materials,
prefabs, the sample scene and module inventory. Existing outputs and
manually authored content are kept. Interactive generation needs a
saved scene if one is open. Run `LKS.Editor.World.W03Verification.Verify`
using Unity batch mode on a clean checkout. The verifier does two repeated
generations, checks file hashes and prefab identities, compares authored
and fallback bounds and checks collider clearances. To reproduce the URP
capture, run `LKS.Editor.World.W03Verification.RenderPreview` with
`-batchmode -force-d3d11 -quit`. Set `LKS_W03_OPEN_PREVIEW`,
`LKS_W03_CLOSED_PREVIEW` and optionally `LKS_W03_FAR_PREVIEW` to new PNG paths.

## Verification

- Imported and compiled in Unity 6000.6.9f1. All 13 existing project EditMode
  tests and 17 tooling tests passed.
- The W03 verification passed for all six IDs, two visual swaps with stable
  anchors and colliders, per-part FBX bounds within 1 mm, and a 1.4 x 2.2 m
  opening. It checked 141 forward-route capsule positions and downward floor
  rays, along with both sides of the corridor seam. A forward ray hit the
  closed leaf. Two generations left the serialized outputs byte-identical.
- Inspected three final D3D11 URP renders. Iteration moved the status panel
  clear of the parked leaf, reduced excessive highlights and fixed depth
  occlusion of world text before the final capture.
- This is a modular inspection scene. The W04 level assembly and W05 player
  controller will provide the in-game traversal check. Player build and
  framerate profiling were outside this verification.
