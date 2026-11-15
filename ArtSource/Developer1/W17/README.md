# W17 visual accessibility and content review

W17 builds on W16 (PR #43), the accepted S15 audit (PR #23) and the S18 integrated route (PR #40). The active Unity and URP pins are 6000.6.9f1 and 17.6.0. The presentation changes use the existing S01 API 1.0 and S16 save schema 2. They use the accepted English catalog, archive and research records, and leave scene binding identities in place.

## Player controls

The main menu and pause have **Text size: Standard / Large**. During the opening, press **T** or select the text-size button. The preference is saved separately from the campaign under `LKS.Station.LargeText`. Large mode enlarges the HUD and pause as space permits, and increases paragraph text in the opening, archive, shelter, seedling cabinet, lab and the report reference. It also enlarges text in the menu, credits and final page. Power, ECHO replay/investigation and the editable report retain their fitted control layouts.

In pause, shelter and laboratory, Tab and Shift+Tab move between available controls, the amber outline identifies focus and Enter activates the focused control. Left and Right adjust the focused mouse-sensitivity slider. Page Up and Page Down scroll the laboratory reading. Archive document scrolling uses its existing Page Up/Page Down and arrow controls. Opening, main menu, archive, ECHO, power and research keep their existing keyboard paths.

Objective and result messages draw above the walking view. Action prompts are above the movement legend, leaving the specimen board available for reading when the same target is aimed at. Text, marks and status phrases distinguish warnings, fiction, missing evidence, simulated data and recorded observations.

## Renderer audit

The accepted S15 gate uses `LKS.StationReleaseAudit.VerifyContent` and `VerifyRelease`. W17 records the displayed English content at Repaint through the accepted `LKS.PlayerTextAudit.Record` API in Editor and development builds. It includes `menu.main`, `quest.objective`, `interaction.prompt`, `interaction.error`, `archive.entry`, `science.label`, `credits.team`, `narrative.farewell` and `ending.final`. The same surface's unchanged value is emitted once. Reference context, archive expansion, report feedback and the arrival passage also have explicit surfaces.

`LKS.Editor.World.W17FontInventory.Verify` traverses scenes including inactive children and prefab contents. Its inventory includes 40 serialized TextMesh components and zero serialized TMP components. It checked all 63 distinct non-whitespace catalog glyphs in the built-in LegacyRuntime.ttf. Authored world labels use that font. The IMGUI screens use the built-in GUI skin and were reviewed at both resolutions. No TMP objects are spawned by the scene UI code.

The raster sheet shows the eight imported images. W14's farewell, arrival and photograph have decorative strokes and silhouettes. The three W02 panel maps and two W15A normal maps are surface imagery. Manual inspection found no words built into these textures. The existing S15 gate guards voice/TTS imports and the development fixture sets the player volume to zero.

## Repeatable shot checklist

| Phase | Screens to capture at 800×600 and 1920×1080 |
| --- | --- |
| Opening | Farewell and arrival captions, keyboard instructions, photograph cropping |
| World HUD and pause | Early laboratory block, objective direction, arrival prompt, Standard versus Large, focus frame and text-size selection |
| Power and ECHO | Power status, all four archive records, expanded longest document after Page Down, missing ECHO interval and evidence comparison |
| Shelter and samples | Shelter warning and historical source, two seedling choices, exterior field prompt with the source note visible, blank and layered-rock lab panes |
| Report and finale | Rejected unsupported claim, reference explanation, exact ending, English credits, main menu and Continue restoring the final photograph |

The diagnostic fixture starts a campaign, uses the installed controls and accepted service commands, checks keyboard focus and produces actual player log and screenshot evidence. It teleports the camera between authored approaches and uses a fixed test clock for experiments. This is an automated muted route, with human playtesting still separate. The normal Windows build includes the accessibility presentation and has the diagnostic helpers outside Assets.

## Handoff

Developer 1 owns these UI screens and the review sheets. Developer 2 owns English catalog and scientific content, including any future word changes that need a new key. W18 can use the same saved scene and public routes, but should profile the normal build and report 60 FPS across traversal. Final UI polish can address the denser power/ECHO/report layouts and physical signage when scheduled.
