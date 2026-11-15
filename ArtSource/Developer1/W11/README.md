# W11 ECHO comparison and causality

## Integration

The view uses S01 contracts 1.0, the accepted S07 archive, the S08 timeline
and S09 `GameplaySession.Investigation`. It reads recorded times, evidence
quality, and captions from `StationEcho.Timeline` and the existing English
catalog. `EchoInvestigation.ControlReadingFreshness` supplies the timing-rule
result. The historical NASA training record remains a separate archive item.

The saved Station scene connects [W10 PR #32](https://github.com/2dshligang/last-known-state-the-legacy/pull/32)
to [S09 PR #16](https://github.com/2dshligang/last-known-state-the-legacy/pull/16).
After confirming arrival and life support, walk to **ECHO ARCHIVE** in bay
03 and press E. Open **Solar particle observation**, select **Mark as read**,
and return to the archive list. Select **Open ECHO replay**, then
**Investigate incident**. The replay closes its projection and transfers
reading focus to the comparison. **Exit investigation** restores first-person
control. To refresh the scene wiring, save open scenes and run
**LKS > World > Install W11 ECHO Investigation**.

| Stable ID or key | Presentation use |
| --- | --- |
| `archive.donki-sep` | Authoritative S07 prerequisite for inspecting and submitting clues |
| `echo.stale-input` | Sensor reading and its recorded timestamp |
| `echo.controller-decision` | Controller action and decision timestamp |
| `echo.backup-exhausted` | Later outage entry |
| `echo.backup-estimate`, `echo.missing-interval` | Model and gap source card |
| `echo.freshness.rule` | Accepted timing and uncertainty explanation |
| `quest.echo-evidence` | Deliberate S03 confirmation after S09 accepts the sequence |

The three **Inspect** actions call `EchoInvestigation.Collect`. The ordered
selection stays intact across source cards and rejected submissions.
**Check sequence** calls `SubmitSequence` and displays the returned
message. **Confirm ECHO step** uses the existing quest target, then the
quest snapshot controls its completed label. The view keeps the draft in
memory while open. S09 checkpoint v1 restores collected clues and the
confirmed order. S16 owns disk persistence. Developer 1 continues with
W12 after the integrated W11 route is accepted.

## Verification

Run `StationEchoInvestigationTests` in the interaction Play Mode suite.
The tests load the saved Station scene, confirm arrival and power through
the station controls, and open the archive and replay from the terminal.
They cover the unread archive gate, missing clues, both incorrect orderings,
source-card return, accepted order, repeated submission, and the transition
to sample collection. The S16 disk round trip restores the confirmed order,
collected clues and completed ECHO step. Pause and focus loss block input.

Unity 6000.6.9f1 passed all 16 Play Mode and 81 Edit Mode tests. A second
installer run left the scene unchanged, and all existing object IDs were
preserved. The standard Windows x64 station build and all 17 Automation
tests passed.

The muted Windows x64 capture opened the installed archive, replay and
investigation after completing arrival and life support. Both runs unlocked
sampling and restored first-person control. A temporary capture script drove
the installed controls and is outside this branch.

- [Investigation action in the replay at 1600 × 900](W11-replay-action.png)
- [Rejected sequence at 1600 × 900](W11-order-rejected.png)
- [Sensor source card at 800 × 600](W11-source-card.png)
- [Confirmed explanation at 800 × 600](W11-order-confirmed.png)
