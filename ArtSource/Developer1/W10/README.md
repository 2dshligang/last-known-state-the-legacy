# W10 ECHO reconstruction

## Integration

The replay view uses S01 contracts 1.0 and the accepted S08 playback API
on `GameplaySession.Echo` (`StationEcho`). The older `IEchoService` signature
is for causal submission. W10 uses the accepted playback instance for
`Snapshot`, `Timeline`, `Subscribe`, `Restart`, `Seek`, and `Stop`.
The subscription filters subject `echo.asterion` and is disposed on exit,
disable, destruction, or session replacement.

After [W09 PR #26](https://github.com/2dshligang/last-known-state-the-legacy/pull/26)
is accepted, attach `StationEchoReview` alongside the archive view and call
`Open(player, currentSession)` from the ECHO terminal's reconstruction action.
The W05 reading lease freezes world input and station power simulation.
The default session clock allows playback during reading and freezes in
the normal pause menu. The stage uses one render texture and a bounded set
of collider-free silhouettes below the station. Closing the view hides the
set and stops playback. Destroying the component releases the projection
material and texture. The Resource shader keeps the projection available
in the Windows Player.

| Stable event ID | Display |
| --- | --- |
| `echo.stale-input` | Recorded station log, power monitor cue |
| `echo.controller-decision` | Recorded controller log, shown at 4 s alongside the estimate |
| `echo.backup-estimate` | Simplified model estimate, outlined backup pack |
| `echo.missing-interval` | Gap slate, original timestamp unknown |
| `echo.backup-exhausted` | Recorded station log, outage cue |

The right-hand list retains both simultaneous records. At a tie S08 picks
the highest sequence for the main frame. The captions and recorded times come
from S08 and the English catalog. Historical NASA evidence remains in W09's
archive. W11 owns evidence collection, causal submission and quest progress.
S16 owns file saves. Replay starts fresh after restoring the station session.

## Controls and verification

Use **Replay from start** for timed playback, **Hold frame** to stop on the
current frame, or scrub by slider, Left and Right, Home and End, or by choosing
a timeline row. Tab cycles through actions and Enter activates the outline.
Escape opens the station pause menu. The replay stays visible at the final
frame until the player exits.

Run `StationEchoReviewTests` in the world interaction Play Mode assembly and
the standard Edit Mode suite. The tests mount the view temporarily in
`Station.unity` and verify five repeated entries, reverse seek across the gap,
global pause, cleanup, unchanged colliders and quest state, and fresh replay
after checkpoint restore. The production terminal connection follows W09.

The recorded log, modeled estimate and missing interval were rendered in a
Windows Player with audio volume at zero. The capture fixture temporarily
mounted the view in the accepted scene and is outside this branch.

- [Recorded station log at 1600 × 900](W10-recorded-log.png)
- [Model estimate at 800 × 600](W10-model-estimate.png)
- [Missing interval at 800 × 600](W10-missing-interval.png)
