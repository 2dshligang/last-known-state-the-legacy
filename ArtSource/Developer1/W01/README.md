# W01 original blockout preparation

Status: **partial W01; not accepted, not Unity-integrated, not a playable scene**.
This packet completes independent geometry preparation while O01 and the shared
Unity baseline are missing. It does not satisfy the W02 dependency gate.

## Base and ownership

- Branch: `feature/world-W01-original-blockout`, based on accepted `main`
  `5f11e94d6c2653ba09e018d65358859704bc4af3`.
- GitHub PR #1 was OPEN and `merged=false` when checked on 2026-11-14;
  its head was `bd83a706dcd01a593eb4c5cf5d24beae2defd848`. No merge attempted.
- The proposed Automation prompts were read as task guidance, not copied into
  main or treated as an accepted dependency. The explicit launch request also
  authorizes editable Developer 1 Blender sources outside Assets.
- Writes are limited to `Assets/_Game/Art/W01`, its folder metadata, and this
  `ArtSource/Developer1/W01` subtree. No packages, settings, CI, shared parent
  metadata, scenes, gameplay state, contracts, or another developer's work changed.
- The pre-existing checkout was left untouched. This separate clone has a
  pre-existing README line-ending normalization difference; README is excluded.

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
rotated to Y-up, with no handedness reflection. Unity orientation remains pending.

The `.meta` files reserve nine unique asset GUIDs using minimal importer records.
Unity has not normalized those records. Keep their GUIDs and model paths when
W02 establishes actual ModelImporter settings; never replace art wrapper identity
or gameplay bindings because a visual mesh changes. Shared parent folder metadata
must be assigned by the coordinator.

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

## Actual evidence, 2026-11-14

- Git LFS 3.7.1 and Node 24.16.0 are available. About 125 GiB free on C: at preflight.
- Both discovered Unity executables report `6000.6.9f1_8c45277fb9c5`.
- `C:\unity\6000.6.9f1` contains Licensing Client, Package Manager,
  WindowsStandaloneSupport, a bundled .NET runtime, and URP blank template 17.2.6.
  The Program Files copy lacks the two client executables at the inspected paths.
- A bounded batch startup of the C:\unity copy launched Licensing Client and
  connected Package Manager, but emitted cache/assertion errors and remained at
  `Licensing is not yet initialized`. The probe and its children were stopped.
  No project was created, no license was accepted/activated, and licensing readiness
  is **unverified**. A sandbox/account issue remains possible; this is not proof
  that the full installation is broken or that a paid license is required.
- `main` has no Packages, ProjectSettings, accepted O01 handoff, or gameplay API.
- Blender generation: exit 0; editable .blend and binary FBX produced.
- Blender verification: exit 0; dimensions, pivot, normals, UVs, 12 triangles,
  FBX export/reimport into Blender, and deterministic OBJ output passed.
- Node verification: exit 0; 18 hashes, nine unique GUIDs, seven closed consistently
  wound OBJ meshes with positive volume, finite meter bounds and valid UVs passed.
  An initial verifier header-length assertion was corrected and the test rerun.
- Two repeat generations in a fixture: zero changed files. A manual file survived.
  An intentional OBJ edit caused expected exit 1 before any writes; snapshot unchanged.
- Fresh-root regeneration also passed the Blender round trip and independent Node
  verifier; all seven OBJ files and model metadata matched the delivered hashes.
- Blender contact sheet rendered and visually inspected: all eight silhouettes
  present, wall/rack/bench readable, three rock shapes distinct. Neutral gray
  preview only; contact-sheet spacing/shadows do not establish in-game grounding.
  The inspected image is included as `preview.png` in this source folder.
- Blender reported inability to write its user extension cache in the sandbox;
  geometry/export/verification completed successfully despite that warning.
- Unity compilation, EditMode/PlayMode tests, URP import, visual Editor inspection,
  collisions, performance, save compatibility, and gameplay acceptance: **not run**.

## Precise handoff and remaining gates

**Coordinator / O01:** provide an accepted Unity project commit with pinned Unity,
URP/packages and lockfile, shared parent metadata, and evidence of a successful
open/compile under the developer account. Resolve PR #1 through normal review;
this work neither merges nor depends on its proposed tooling implementation.

**Developer 1 / W02 after acceptance:** import the eight inventory models; inspect
scale against a one-meter cube, wall snap pivot, facing/normals, and UVs. Establish
URP materials, missing-texture report, collider intent, inspection scene and
wrapper prefabs. Export a reviewed wall revision to the existing FBX path, keep
its `.meta`, reimport twice, and verify references and colliders in Unity. Record
screenshots. This is required before W03 door-wall/airlock/ECHO modeling.

**Developer 2 / S01:** publish the accepted authoritative contracts and composition
handoff before W04 binds any generated scene content. No contract change requested
or parallel API created in this art-only packet.

W01 is partial until dependency/asset acceptance; W02-W21 remain unexecuted.
No safe next integration task is ready on the currently accepted main.

## Eight-part task report

1. FILES CREATED OR MODIFIED: the Art/W01 models and metadata; this Developer 1
   source subtree with .blend, generator, inventory, sources, verifier and handoff.
2. IMPLEMENTATION DETAILS: eight original meter-scale placeholders, stable asset
   GUIDs, source FBX export settings, hash-protected repeatable generation.
3. TESTS ACTUALLY EXECUTED: Blender generation/verification/render; Node geometry,
   hash/GUID checks; repeatability/manual-file/changed-output fixtures; Git checks.
4. TEST RESULTS: geometry checks passed; rejection test failed as expected without
   writes. Unity and gameplay acceptance remain unverified. See evidence above.
5. UNITY EDITOR ACTIONS REQUIRED: accepted O01 project, W02 inspection/import,
   same-path revision/reimport, materials, colliders and prefab reference checks.
6. INTEGRATION RISKS: no accepted Unity/package baseline or parent metadata;
   importer defaults and licensing readiness unverified; FBX/.blend use Git LFS.
7. REMAINING ISSUES: blocked external archive, partial W01, W02-W21 dependency gates,
   no playable route or measured performance/playtime.
8. SUGGESTED GIT COMMIT: `feat(world): prepare original W01 blockout assets`.
