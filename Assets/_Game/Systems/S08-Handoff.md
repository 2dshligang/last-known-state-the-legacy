# S08 ECHO replay

Uses S01 contracts 1.0, [S05](https://github.com/2dshligang/last-known-state-the-legacy/pull/10) and [S07](https://github.com/2dshligang/last-known-state-the-legacy/pull/14). The fictional 2059 timeline follows the controller and backup sequence in Automation/docs/ARCHITECTURE.md. Historical DONKI data stays in the archive.

1. W10 binds Current.Echo after RestartWithPower. Read Snapshot, then subscribe to changes at subject echo.asterion. Dispose on panel close, disable or session change. One pending notification per run coalesces rapid seeks. Read the latest snapshot when binding. Use Timeline to draw every entry, including simultaneous records. At a tie the highest sequence supplies the current frame.
2. Use Restart to play from zero, Seek for scrubbing and Stop to reset and stop. The replay runs on session elapsed game seconds and can play during the station's reading pause. Global time-scale pause freezes the default session clock. Commands should be issued after the current session tick when exact frame alignment matters.
3. Show each entry's caption through the English catalog. VisualCue identifies only the presentation. Display Missing as a gap and Estimated as modeled. Recorded describes a log inside the fictional story. The original timestamp is unknown for echo.missing-interval, represented across playback seconds 8 through 14 (exclusive).
4. S09 owns evidence collection, causal submission and quest completion. Playback and scrubbing have no authority over those states.

Run LKS.Systems.Tests. Developer 1 verifies scrub, reverse seek, repeated open and close, and visible category and gap labels in W10. Any rendered character dialogue remains fictional and text-only.
