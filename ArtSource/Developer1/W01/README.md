# W01 original blockout preparation

Status: **implemented and verified in Unity, ready for review in PR #2**.

## Base and ownership

O01, the automation foundation, and S01 are accepted in main at
`5d324edab7c305bb6d803923617dc6226c4b67b4`. The W01 branch includes that base.
W01 owns the original models and metadata in `Assets/_Game/Art/W01`, plus
the editable sources, provenance and verification evidence in this folder.
The S01 contract API is version 1.0. The identifiers in the W01 inventory
describe art assets. World bindings and production service IDs are later work.

## Asset inventory and provenance

`inventory.json` records SHA-256 hashes, dimensions, Blender version, export
settings, and asset IDs. IDs here are art inventory names, **not** an implementation
of Developer 2's StableId/save contract. `sources.json` records external discovery
and the blocked Kenney request. No third-party asset was imported.

All eight meshes are original procedural placeholders for fictional station art.
Rock silhouettes are visual distinctions only; they are not measured geological
specimens or evidence of life. There are no audio, dialogue, NASA measurements,
textures, shaders, or player-facing scientific claims in this packet.

| Mesh | Unity dimensions X/Y/Z, meters | Pivot and handoff intent |
|---|---|---|
| Straight wall | 4 / 3 / 0.2 | Bottom-left edge, thickness centered on snap plane |
| Floor | 4 / 0.2 / 4 | Top corner at zero height; extends downward |
| Equipment rack | 0.8 / 2 / 0.6 | Bottom center; open shelf silhouette |
| Lab bench | 2 / 0.9 / 0.8 | Bottom center; separate legs and top in one mesh |
| Solar panel | 2 / 0.08 / 1 | Bottom center; simple slab, no functional simulation |
| Layered rock | 0.8 / 0.24 / 0.54 | Bottom center; wide stacked profile |
| Angular rock | 0.64 / 0.56 / 0.5 | Bottom center; tall angular profile |
| Rounded rock | 0.68 / 0.42 / 0.54 | Bottom center; low-poly rounded profile |

Exact generated bounds are authoritative in the inventory. Use primitive or
compound colliders after import; none are supplied or tested here. The wall
source contains one named mesh with outward normals and a UVMap. UVs occupy
0..1 per face and overlap intentionally for a generic tiled blockout material;
they are not a unique lightmap unwrap. W02 must generate or inspect secondary UVs.

## Blender source and FBX settings

Authored with Blender **5.3.0**, build `02f0899d78e3` (2026-11-10).
Source: `ArtSource/Developer1/W01/wall-straight-4m.blend`.
Export: `Assets/_Game/Art/W01/wall-straight-4m.fbx`.

Blender uses meters, scale length 1, X length / Y depth / Z height. The wall spans
X [0,4], Y [-0.1,0.1], Z [0,3] at an identity object transform. FBX settings:
selected mesh only, scale 1, apply units, `FBX_SCALE_UNITS`, forward -Z, up Y,
space transform enabled, experimental bake-space-transform disabled, face
smoothing, triangulation enabled, no animation, bones, embedded textures, or
external file copies. The FBX has 12 triangles. OBJ coordinates are explicitly
rotated to Y-up, with no handedness reflection. The imported Unity orientation was measured below.

The `.meta` files reserve nine unique asset GUIDs using minimal importer records.
Unity imported the assets and retained these records. W02 should keep asset GUIDs
and paths when configuring import and building wrapper prefabs.

## Reproduction and verification

From repository root, with the installed Blender executable:

```powershell
$blender = 'C:\Program Files\Blender Foundation\Blender 5.3\blender.exe'
& $blender --background --factory-startup --python-exit-code 1 --python ArtSource/Developer1/W01/build_assets.py
& $blender --background --factory-startup --python-exit-code 1 --python ArtSource/Developer1/W01/build_assets.py -- --verify
node ArtSource/Developer1/W01/verify_assets.mjs
./ArtSource/Developer1/W01/check_repeatability.ps1 -BlenderPath $blender -ScratchRoot 'C:\tmp\lks-w01-new-test-directory'
& $blender --background --factory-startup --python-exit-code 1 --python ArtSource/Developer1/W01/build_assets.py -- --preview 'C:\tmp\lks-w01-new-preview.png'
```

Scratch and preview paths must not exist. Tests preserve their temporary evidence
and never delete directories. The preview is a Blender contact sheet using its
own camera/lighting, not a Unity screenshot or a final material target.

On an existing packet, generation verifies all recorded hashes and writes nothing.
Changed/missing output or changed generator code stops the operation. Manual files
outside the manifest remain untouched. To reproduce geometry from scratch, copy
the generator into the same directory layout under a **new empty root**, then run
it. Blender/FBX binary timestamps need not be byte-identical across fresh builds;
each inventory hashes the actual produced files. Artist revisions require a
reviewed regeneration/version update, retaining accepted Unity GUIDs. Never delete
the inventory or edited files to bypass a failed check.

## Verification, 2026-11-14

- Blender 5.3.0 generated the editable wall source and initial binary FBX.
  Source dimensions, normals, UVs, the 12-triangle Blender FBX round trip and
  deterministic OBJ geometry passed. Fresh-root generation produced the same
  OBJ and metadata bytes. The repeated generation fixture preserved an
  unrelated manual file and rejected edited output before writing.
- Node verified 18 tracked file hashes, nine unique Unity GUIDs, mesh winding,
  positive volume, UVs and dimensions of seven OBJ files, and the binary FBX
  header. Git LFS fsck passed. All 17 automation tooling tests passed.
- Unity 6000.6.9f1 compiled and imported all eight models from a fresh Library.
  An Editor probe instantiated each model and measured mesh vertices in scene
  coordinates. All dimensions matched the recorded inventory within 1 mm.
  All meshes had normals and UV0 and geometry had 356 triangles total.
- The wall imported at 4 x 3 x 0.2 m, with bounds from (-4, 0, -0.1) to
  (0, 3, 0.1) relative to its snap pivot. This negative-X span is the measured
  Unity result. Keep the imported root rotation when placing it beneath a
  wrapper. In the first render probe, explicitly replacing root rotation with
  identity laid the wall flat. Preserving the imported transform fixed it.
- Visually checked the second Unity URP render with Direct3D 11. The image
  shows all eight silhouettes, an upright wall and an orange 1 m reference
  cube. It is saved as [unity-preview.png](unity-preview.png). Materials and
  layout in this temporary preview were for import inspection.
- Unity EditMode ran the four accepted S01 contract tests. All four passed,
  with zero failures or skips. W01 does not supply an inspection scene,
  wrapper prefabs, collision geometry, or a player build.

## Handoff to W02

Use the eight original geometry files and dimensions in `inventory.json`.
The first Unity import confirms the meter scale at global scale 1 and an
upright FBX with the imported root rotation intact. Preserve the art GUIDs
and build stable wrapper roots for snapping, collision and interactions.
Configure URP test materials and inspect secondary UV needs. Color, normal,
roughness and metallic textures are currently absent. The Kenney request
returned a redirect to its HTML page and contributed no files.

After W01 is accepted, W02 adds persistent inspection content and verifies
same-path reimport, prefab and collider stability. W03 can use that accepted
result to build the wall opening, airlock and ECHO terminal.

## Eight-part task report

1. FILES CREATED OR MODIFIED. Eight model assets and metadata, Blender source,
   generator, inventory, provenance, verification scripts and preview images.
2. IMPLEMENTATION DETAILS. Original meter-scale placeholders with stable art
   GUIDs, documented FBX export and hash-protected repeatable generation.
3. TESTS ACTUALLY EXECUTED. Blender and Node geometry checks, repeatability
   fixtures, tooling tests, Unity model import and URP render, S01 EditMode.
4. TEST RESULTS. All listed final checks passed. The edited-output fixture
   correctly refused to write. The Unity render was visually inspected.
5. UNITY EDITOR ACTIONS REQUIRED. W02 should configure persistent materials,
   wrappers, colliders and the inspection scene.
6. INTEGRATION RISKS. The wall requires its imported rotation. UV0 is tiled
   per face. W02 must account for these properties when generating prefabs.
7. REMAINING ISSUES. W01 is ready for review. W02 acceptance requires a
   published W01 base and its own Unity checks.
8. SUGGESTED GIT COMMIT. `test(world): verify W01 blockout in Unity`.
