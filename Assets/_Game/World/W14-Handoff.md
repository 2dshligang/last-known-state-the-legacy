# W14 opening handoff

Developer 1 owns the opening presentation. Developer 2 owns the narrative text, opening progress, and save schema.

## Integration

[W13 PR #34](https://github.com/2dshligang/last-known-state-the-legacy/pull/34) provides the accepted Station scene. [S13 PR #21](https://github.com/2dshligang/last-known-state-the-legacy/pull/21) provides the dates, passages and commands. W14 uses S01 INarrativeView API 1.0 and StationNarrative checkpoint version 1. [S16 PR #29](https://github.com/2dshligang/last-known-state-the-legacy/pull/29) owns disk persistence.

| S13 passage | Portrait and camera IDs | Exact in-game date |
| --- | --- | --- |
| narrative.farewell | portrait.dmitry / camera.farewell | January 20, 2059 |
| narrative.arrival | portrait.alexei / camera.station-arrival | November 14, 2089 |

Open Station.unity and enter Play Mode. StationInteraction adds StationOpening and opens the current S13 cue when a fresh session starts. Continue with Enter or the visible button. Skip with Space or the visible button. Restart station from the pause menu to run the opening again. Both paths release reading focus to the station player and leave quest.arrival available.

The illustrated opening uses original procedural silhouette images and an original photograph with no external portraits. Keep the photographs in the two shots and at the quest.ending anchor sourced from the same W14/DmitryPhoto Resources texture. The photograph prop is visual only and does not grant ending access.

Run **LKS > World > Build Station for Windows** to build the playable scene. Automation can use Unity with -batchmode -quit -executeMethod LKS.Editor.World.W14StationBuild.BuildWindows and set LKS_STATION_BUILD_PATH to an absolute executable path. The build command supplies Station explicitly. The shared Bootstrap build profile can be updated separately with the automation owner for release.

## Acceptance

Play StationOpeningTests in the interaction Play Mode suite. Check a continuous opening, a skipped opening and a restart. The arrival state should be identical, with the original camera and cursor ownership returned to first person. Text stays on screen until a player confirms or skips. The two passages and the short photograph movements should fit comfortably into the two-minute reading budget without an imposed delay.

At 800x600 and 1280x720, verify the date, passage, photograph and both controls with audio muted. Press Escape once, use Return to reading, and then continue. Verify first-person control returns in the arrival bay after the last confirmation and after skip. The photograph should remain visible at the later station photo prop.

Developer 1 continues with W15 and can reuse W14/DmitryPhoto for portrait.dmitry-photograph. The ending interaction and authority remain in S14. The required final text is Dad... I did it.
