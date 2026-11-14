# W09 archive reader

## Integration

The reader uses S01 API 1.0 and the accepted S07 `StationArchive`
created by `SessionBootstrap.RestartWithPower`. Use the W06 raycast and
the existing `quest.echo-evidence` anchor to open the reader in Station
after W08 is accepted. Attach `StationArchiveReader` to the first-person
object and pass its `StationFirstPerson` and current-session delegate to
`Open`. The reader can also be mounted on another terminal that supplies
those same dependencies.

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
acknowledgment and release on disable or session replacement. The tests mount
the reader on a temporary GameObject component in the accepted Station scene.

The production scene binding depends on the acceptance of
[W08 PR #25](https://github.com/2dshligang/last-known-state-the-legacy/pull/25).
Developer 1 owns this binding and the W10 reconstruction view. Developer 2
owns additional imported archive records and their validated content.
