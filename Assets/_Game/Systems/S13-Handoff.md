# S13 narrative presentation

Uses S01 INarrativeView and the [accepted S12 report](https://github.com/2dshligang/last-known-state-the-legacy/pull/19). Developer 1 owns staging in W14 and W15.

1. Bind Current.Narrative after RestartWithPower. Read TryGetOpening to show the current passage. Call cue.Present(view, Current.Archive.Content) to pass English text, portrait ID and camera ID to INarrativeView. Keep text visible until the player explicitly continues or skips. Use the W05 reading focus lease while the passage is open.
2. Bind portrait.dmitry and camera.farewell to the departure cue, and portrait.alexei and camera.station-arrival to the return cue. The fictional dates are March 15, 2050 for Alexei's birth, January 20, 2059 for farewell, and November 14, 2089 for return. Alexei is eight and 39 respectively.
3. Continue calls AdvanceOpening. Skip calls SkipOpening and returns control to the same arrival gameplay as normal completion. ReplayOpening starts a temporary playback after completion. Read the cue again after each command, and close the presentation when no cue remains. Gameplay still requires the existing station interactions and quest conditions.
4. CaptureOpening provides version-1 opening state. Restore via openingCheckpoint in RestartWithPower. Replay never replaces saved progress. Close the view and release its input lease on session replacement. S16 owns durable storage.
5. The archive includes archive.dmitry-notes and archive.arrival with concise summaries and optional details. Keep their Fiction labels. After S12 accepts the report, TryGetResearchContext supplies camera.research-desk and portrait.dmitry-photograph. Stage this cue as a quiet report context. S14 owns permission to show the final ending through ending.final.

Verify both opening paths, re-entry and skip in the W14 scene. Verify the research cue at the desk and preserve the exact ending text in W15. Use on-screen reading throughout.
