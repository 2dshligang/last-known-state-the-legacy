# W09 archive reader

## Integration

Open `Assets/_Game/Scenes/Station.unity` in Play Mode. Walk to bay 03,
aim at the orange face below **ECHO ARCHIVE**, and press E. The saved
scene already binds `StationArchiveReader` to the existing
`quest.echo-evidence` anchor through the W06 raycast. To refresh the wiring,
save open scenes and run **LKS > World > Install W09 Archive Terminal**.

The reader uses S01 API 1.0 and the accepted S07 `StationArchive`
created by `SessionBootstrap.RestartWithPower`. The scene adapter passes
its `StationFirstPerson` and current-session delegate to `Open`. The reader
can be reused by another terminal that supplies those dependencies.

The list holds the W05 input focus lease. Each document also holds S07's
archive lease. Closing the reader or replacing its session releases both.
Acknowledgment calls `StationArchive.Acknowledge` while the document is
open. W11 owns the separate ECHO causal confirmation and its quest action.
The already-read state comes from S07's checkpoint. S16 supplies disk
persistence.

| Entry ID | Type and source |
| --- | --- |
| `archive.donki-sep` | Archived NASA observation, 2017-09-04 23:52 UTC, [DONKI record 13000](https://ccmc.gsfc.nasa.gov/DONKI/view/SEP/13000/-1) |
| `archive.power-model` | Simplified fictional model, [ASTERION engineering model v1](../../../Automation/samples/science/engineering-model.json) |
| `archive.dmitry-notes` | Fictional station record dated 2059 |
| `archive.arrival` | Fictional station record dated 2089 |

## Controls and checks

Use Up and Down to choose a record and Enter to open it. Use Tab or
Shift+Tab to move between actions and Enter to activate the outlined action.
Document text uses a measured scroll view. The wheel, arrow keys, Page Up,
Page Down, Home and End move through it. Back to list restores the exact
selection and scroll offset. Escape opens the standard pause menu without
closing the document.

Play Mode tests open all four real records at zero audio volume. They
verify keyboard selection and focus, exact list return, both pause sources,
explicit and idempotent acknowledgment, unchanged quest progress, restored
acknowledgment and release on disable or session replacement. They use the
saved scene wiring, walk the character capsule to the terminal and submit
through the real camera ray. The ray skips the player's own colliders,
including on re-entry after pause, and still refuses a blocked terminal.

The individual documents were captured at 800 × 600 with audio volume
at zero. The integrated view was captured in Windows at the installed
ECHO terminal at 1600 × 900 and 800 × 600, also with audio volume at zero.
The integrated build passed 12 Play Mode and 69 Edit Mode tests in Unity
6000.6.9f1. A repeat install left the scene unchanged.

- [Archive opened at the installed terminal](W09-station-terminal-wide.png)
- [Archive entry acknowledged in the installed scene](W09-station-acknowledged.png)
- [Record list and visible keyboard selection](W09-record-list.png)
- [Archived NASA observation](archive.donki-sep.png)
- [Simplified power model](archive.power-model.png)
- [Fictional research notes](archive.dmitry-notes.png)
- [Fictional arrival record](archive.arrival.png)

The scene binding builds on accepted
[W08 PR #25](https://github.com/2dshligang/last-known-state-the-legacy/pull/25).
Developer 1 owns the W10 reconstruction entry. Developer 2 owns additional
imported archive records and their validated content.
