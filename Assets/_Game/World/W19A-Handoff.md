# W19A station interface

The candidate starts at accepted W19 on main `c9a5ac6c14d6be13c46259055027e88b2f26593d`. It uses Unity 6000.6.9f1, URP 17.6.0, S01 API 1.0, S16 save schema 2 and S19 credits API 1. The explicit Windows entry is `LKS.Editor.World.W14StationBuild.BuildWindows`. W20 needs fresh evidence from the reviewed W19A revision. S20's release hold and the coordinator's build settings and index changes remain separate.

## Player route and input

The existing panels share IMGUI chrome, a stable visual focus outline and a common set of control and status colors. An integrated ECHO prototype used the installed W18 projection and the same focus lease as the player. The station uses one live renderer per screen. Keeping IMGUI avoids a second focus stack across uGUI, UI Toolkit and the first-person input. English source attribution still comes from the station catalog and credits. The HUD puts the objective in the upper left, the launch menu uses the existing W14 Mars arrival art, and the shared palette follows the ASTERION brief. The controls use text and shapes; no new icons, fonts, voice, external media or software packages were added.

| Screen and entry | Main action | Error or blocked state | Close and return |
| --- | --- | --- | --- |
| Launch | New Game opens the farewell. Continue attempts the campaign slot. | S16 supplies empty, incompatible, invalid and recovery messages when a load is attempted. | Settings and Quit are available from launch. |
| Farewell and arrival | Continue, then Enter station. Space keeps the existing skip route. | Input is suspended while paused or out of application focus. | Game menu is available on M. The final slide returns to first person. |
| Campaign menu and settings | Continue or Return to station. Settings saves text size and mouse sensitivity using the existing preferences. | New Game confirms loss of unsaved session progress. Save and Load use S16 results. | Settings returns to the menu. Return resumes the existing scene or opening passage. |
| Pause and HUD | Resume or return to the reading panel. HUD shows the current target, direction and feedback. | A blocked interaction shows its current reason. | The original input lease and cursor policy hand control back to the player. |
| Power and recovery | Choose loads, apply the selection and confirm safe air and water. | Demand, battery charge, possible interruptions and the safe power recovery route come from the current service state. | Close panel returns to the corridor. |
| Archive and ECHO playback | Read archive entries or choose an event on the replay. The replay distinguishes logs, model and gap with explicit labels. | A missing interval has no original time or reconstructed projection. | Exit replay returns to the station. Investigate opens the separate comparison. |
| ECHO comparison and source | Inspect clues, arrange them, check and confirm the sequence. | Inspected/uninspected text and available actions follow the sequence checkpoint. | Closing a source returns to the same comparison, sequence and source control. |
| Shelter and seedlings | Follow the training briefing and operate the powered mechanisms. | Passive shielding, motor power and training status use distinct descriptions. | Player can leave the panel while waiting. External NASA reference is optional. |
| Exterior and laboratory | Follow the A/B loop and airlock signs. Review collected samples and run the empty control separately. | Bring the required sample and supply laboratory power before analysis. | Readings and citations are optional detail panels. Return to station resumes the route. |
| Report | Review station evidence, choose observations and conclusions, submit and queue. | The confirmation or rejection and recovery guidance come from the existing report result. | Evidence details return to the existing report draft. |
| Photograph and credits | Inspect the photograph after the required milestones. The last line is `Dad... I did it.` | The existing ending readiness owns access and replay. | Credits and attribution scroll, then return to the menu. |

The modified scene plates retain their existing serialized identities and attach to the same station models. Preserve `quest.echo-evidence`, `echo.asterion`, `door.exterior-airlock`, the 13 interaction anchors, and the S16 `campaign` slot. The comparison still uses the authoritative evidence order and timestamps. W18 continues to draw its projection per changed cue.

## Windows evidence

Paired frames use 800×600 at the same installed interaction points. The ECHO sign uses the same approach transform, look target and lighting. The dot-prefixed evidence files are excluded from Unity import.

| Screen | Accepted W19 | W19A |
| --- | --- | --- |
| Campaign menu | [Before](.w19a-evidence/.before-menu.png) | [After](.w19a-evidence/.after-menu.png) |
| HUD | [Before](.w19a-evidence/.before-hud.png) | [After](.w19a-evidence/.after-hud.png) |
| ECHO sign | [Before](.w19a-evidence/.before-echo-sign.png) | [After](.w19a-evidence/.after-echo-sign.png) |
| Station power | [Before](.w19a-evidence/.before-power.png) | [After](.w19a-evidence/.after-power.png) |
| ECHO playback | [Before](.w19a-evidence/.before-echo-playback.png) | [After](.w19a-evidence/.after-echo-playback.png) |
| ECHO comparison | [Before](.w19a-evidence/.before-echo-comparison.png) | [After](.w19a-evidence/.after-echo-comparison.png) |
| Shelter | [Before](.w19a-evidence/.before-shelter.png) | [After](.w19a-evidence/.after-shelter.png) |
| Lab and empty control | [Before](.w19a-evidence/.before-laboratory.png) | [After](.w19a-evidence/.after-laboratory.png) |
| Rejected report | [Before](.w19a-evidence/.before-report.png) | [After](.w19a-evidence/.after-report.png) |
| Final photograph | [Before](.w19a-evidence/.before-finale.png) | [After](.w19a-evidence/.after-finale.png) |

The first review set is [launch](.w19a-evidence/.after-launch.png), [HUD at ECHO](.w19a-evidence/.after-echo-sign.png), [ECHO projection](.w19a-evidence/.after-echo-model.png) and [report](.w19a-evidence/.after-report.png). Additional frames cover [an ECHO source card](.w19a-evidence/.after-echo-source.png), [standard ECHO text](.w19a-evidence/.after-echo-standard.png) and the report source with [standard](.w19a-evidence/.after-report-source-standard.png) and [large text](.w19a-evidence/.after-report-source.png).

Run the following with the project root and an absolute log path, using the pinned Windows editor. Tests omit `-quit` and stop through the Unity test runner.

| Arguments after `-batchmode -projectPath <project> -buildTarget Win64` | Observed result and local file under `TestResults/W19A/` |
| --- | --- |
| `-nographics -runTests -testPlatform EditMode -testResults edit-final.xml` | 89 passed, 0 failed, 0 skipped. `edit-final.log`, `edit-final.xml`. |
| `-force-d3d11 -noaudio -runTests -testPlatform PlayMode -testResults play-final.xml` | 24 passed, 0 failed, 0 skipped on the final layout. S18 and W19 route and checkpoint coverage. `play-reviewed.log`, `play-reviewed.xml`. |
| `-nographics -quit -executeMethod LKS.Editor.World.W04Verification.Verify` | Two byte-identical no-op runs, 192 generated objects, 13 rays, 1890 original clearance/floor samples and manual content preservation. `w04-final.log`. |
| `-force-d3d11 -quit -executeMethod LKS.Editor.World.W17FontInventory.Verify` | 63 catalog glyphs, 40 serialized TextMesh components and 0 serialized TMP components. `font-final.log`. |
| `-nographics -quit -executeMethod LKS.StationReleaseAudit.VerifyContent` | Static S15 audit passed. `s15-final.log`. |
| `-nographics -quit -executeMethod LKS.StationReleaseAudit.VerifyRelease` | S15 ran with `LKS_RELEASE_EVIDENCE` pointing at the muted development-player capture record and passed. `s15-runtime.log` and the reviewed layout in `s15-reviewed.log`. |
| `-nographics -quit -executeMethod LKS.Editor.World.W14StationBuild.BuildWindows` | W19 binding validation passed and the Windows x64 Station build succeeded. `production-final.log`. |

The W17 fixture was installed only in temporary evidence builds. `-lksW17Evidence <folder> -force-d3d11 -noaudio -popupwindow -screen-fullscreen 0 -screen-width <x> -screen-height <y>` reached the photograph and reloaded its completed checkpoint. The final 800×600, 1280×720 and 1920×1080 player logs (`reviewed-*.log`) include the screen size, text setting and mute state for each frame. Launch Continue also restored a completed checkpoint generated before W19A; New Game subsequently returned to the opening in the isolated Windows profile. Tests used isolated save folders, drove installed interaction rays and real view handlers, and injected service calls for portions of the route. The normal Windows x64 player lives at `Builds/W19A-final/LastKnownState.exe`. It does not contain either fixture source.

With ordinary controls on the production player, Settings opened from a mouse click and Right changed sensitivity from 0.12 to 0.14. The text-size button changed to Large. Both values survived closing the player with Alt+F4 and reopening Settings after a new process launch. The full manual focus and offline walkthrough is pending.

## Frame timings

The controlled pair used accepted W19 with W18 optimizations and the W19A presentation before the final palette and launch update. Both ran on the same Ryzen 7 5800X, RTX 4060, 32694 MiB RAM, D3D11, PC quality, 1920×1080, muted, with VSync off and uncapped. A fixture recorded 240 end-of-frame wall times per region in each process. Its first ECHO frame includes entry and projection presentation. One run per revision and unreset driver caches limit the comparison. The published W18 2060 SUPER measurements used a different PC and route.

| Region | Accepted W19 p95 ms | W19A p95 ms | Accepted W19 / W19A first 16 frame peak ms |
| --- | ---: | ---: | ---: |
| HUD | 7.356 | 5.811 | 6.744 / 6.912 |
| ECHO first entry | 7.203 | 8.137 | 43.685 / 37.273 |
| ECHO reopen | 6.318 | 7.680 | 6.530 / 5.542 |
| ECHO comparison | 6.288 | 7.741 | 6.230 / 7.880 |
| Report with rejection | 6.465 | 6.404 | 5.568 / 5.734 |

First ECHO entry still exceeds 16.67 ms. The warmed ECHO panels had higher p95 frame times in this pair. See `paired-baseline-1920x1080.log` and `paired-candidate-1920x1080.log`. The reviewed Windows build, timed separately after loading the old checkpoint, peaked at 25.462 ms for first ECHO entry. HUD, warmed ECHO, comparison and rejected report measured p95 of 2.521, 2.480, 2.427 and 2.437 ms respectively. See `reviewed-1920x1080.log`. The checkpoint step changes warm-up, so use the controlled pair for the comparison. Repeat on the target PC before accepting the whole-route frame budget. No timed full manual run has been measured.

## Developer 2 and W20

Developer 2 owns the remaining pre-load Continue availability question. Please expose a read-only slot query on `StationSaves` for the `campaign` slot covering available, recoverable, empty, corrupt, incompatible and unavailable states. Use the existing save validation rules, carry the existing message keys and leave the running session and both save copies untouched. The current UI can show the authoritative empty state after a failed load. At launch it cannot tell whether the slot exists through the public Save and Load API.

W20 should build the reviewed commit, perform a full keyboard-and-mouse run from New Game to the photograph offline and muted, load a pre-W19A checkpoint, record time and review the final screenshots on the target PC. During ECHO, open a source, use Tab and Enter to return to its control, press Escape for pause, resume and close back to the station. Repeat Enter/Escape and switch away with Alt+Tab to check cursor and panel ownership. Try safe power recovery and a rejected report before submitting the supported one. Re-run the release audit on the approved release evidence and keep S20's release decision with the coordinator.
