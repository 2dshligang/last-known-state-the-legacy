# W15A station art

[Before](baseline.png) · [After](after.png) · [ECHO 800×600 before](baseline-echo-800x600.png) · [ECHO 800×600 after](after-echo-800x600.png) · [Baseline and gap audit](visual-baseline.md) · [Asset provenance](sources.json)

W15A is installed in the accepted station scene across the playable route. The pass adds three corridor panel variants, bulkhead frames, utility and diffuser details, dedicated housings for power, ECHO, analysis and research, a separate clean control tray, planted cabinet trays and a shelter track cover, a photograph backing and layered exterior terrain. Cue colors are consistent with the accepted English labels and interaction feedback. The mirrored depth detected during screenshot review was corrected at the Blender export source and rechecked in the player.

The project contains 20 authored meshes, 20 visual-only prefab wrappers, 13 shared URP Lit materials, 50 prefab placements and two CC0 normal maps. Five accepted W04 materials retain their GUIDs and update in place. The scene audit compared Unity documents by serialized file ID. All 833 baseline documents remain. Four of them now disable the old exterior box renderers and one attaches the generated visual group under `station.spawn`. Its new documents total 103. The registered W04 roots (192), 13 interaction anchors, scene GUID, interactions and original exterior collision shapes remain in place. The panel patterns and exterior rocks are illustrative station art.

## Visual QA

The before/after sheets represent nine paired 1920×1080 player captures. Arrival, power and ECHO show the corridor framing, distinct faceplates and readable room signs. Shelter and seedlings show the shutter track and tray geometry. Analysis, blank control and the report desk have separate silhouettes. The field loop has layered banks and an island profile that keeps both path markers visible. The overlook retains the photograph, with a small backing around it. The paired 800×600 ECHO views were inspected specifically for the archive and adjacent shelter signs.

The close-up shelter highlight remains stronger than neighboring bays and the shutter and airlock still use their accepted flat shells. The exterior sky stays gray, the outcrops are visibly stylized, and the rock banks repeat if viewed from the edge. Those are the remaining visual limitations. The intro, report and ending interfaces retain accepted W15 presentation.

In a muted static arrival view on a Ryzen 5 5600 and RTX 2060 SUPER, the player fixture sampled 160 frames per build at 1920×1080 on Direct3D 11 with vSync disabled. The baseline p50/p95 were 2.606/15.420 ms and the W15A p50/p95 were 2.997/11.868 ms. Unity total allocated memory snapshots were 321,716,454 and 328,892,646 bytes. The single static sample cannot establish route performance or isolate CPU and GPU time. Sustained 60 FPS and color-vision simulations remain unmeasured.

## Reproduction

Use Blender 5.3.0 and Unity 6000.6.9f1. The active project must retain URP 17.6.0. The editable `AsterionVisualKit.blend` and FBX models are LFS objects. The generator overwrites the listed project-authored FBX outputs and the generated Blender source. Use a disposable tree or back up hand edits to the Blender file before regenerating. Successful geometry generation is repeatable, while exported binary timestamps can differ.

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.3\blender.exe' --background --python-exit-code 4 --python ArtSource/Developer1/W15A/build_visual_kit.py
node ArtSource/Developer1/W15A/verify_assets.mjs
```

The scene is already assembled. For a future art revision, **LKS > World > W15A > Preview Changes**, **Apply Visuals** and **Verify Visuals** preview mapped placements, update owned scene geometry and validate the resulting references and collision separation. The batch methods are `LKS.Editor.World.W15AVisualSetup.{Preview,Apply,Verify}`. W04 `Generate` also refreshes an already installed set. The preview and apply commands should leave an up-to-date scene unchanged.

To reproduce the stills, copy the review-only `W15AViewCapture.cs` into `Assets/_Game/World/W06/` on disposable baseline and revision worktrees. Build each using `LKS.Editor.World.W14StationBuild.BuildWindows` and launch the resulting executable as follows. The regular branch and the clean Windows build exclude the fixture from `Assets`.

```powershell
& $player -lksVisualCapture $shots -force-d3d11 -screen-fullscreen 0 -popupwindow -screen-width 1920 -screen-height 1080 -logFile $playerLog
```

Repeat with `-screen-width 800 -screen-height 600` for the ECHO spot check. The game uses bundled art and archived science. An optional external DONKI source link opens the system browser on request. No runtime download was added.

## W16 handoff

Use the accepted W15A commit in `Assets/_Game/Scenes/Station.unity`, `Assets/_Game/Art/W15A/Materials/`, `Assets/_Game/Art/W04/` and `Assets/_Game/Prefabs/W15A/` as the lighting base. Unity quality preset for the paired inspection is `PC`. Porcelain and matte alloy are the main interior surfaces, graphite outlines the structure, ochre marks controls and trim, muted teal marks informational features and the exterior stays warm and dusty. Preserve the authored `Diffuser.mat` low emission and place or bake light separately. Check the shelter highlight, ECHO focus, the sky and the photograph exposure using the saved camera positions in the fixture. W15A leaves shadow casting disabled on detail wrappers. Before baking, validate UV2 on receivers and place light probes for dynamic doors/player objects if W16 enables baked lighting. Keep the route signs and science cards readable at 800×600.

Coordinator handoff after acceptance: add W15A to `Automation/prompts/INDEX.md` between W15 and W16 and add the accepted W15A dependency to `Automation/prompts/W16.md`. Shared files require the coordinator's ownership.
