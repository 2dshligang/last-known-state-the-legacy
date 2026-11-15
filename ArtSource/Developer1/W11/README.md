# W11 ECHO comparison and causality

## Integration

The view uses S01 contracts 1.0, the accepted S07 archive, the S08 timeline
and S09 `GameplaySession.Investigation`. It reads recorded times, evidence
quality, and captions from `StationEcho.Timeline` and the existing English
catalog. `EchoInvestigation.ControlReadingFreshness` supplies the timing-rule
result. The historical NASA training record remains a separate archive item.

After [W10 PR #27](https://github.com/2dshligang/last-known-state-the-legacy/pull/27)
is accepted, attach `StationEchoInvestigation` to the station player or
terminal controller. Close the active replay view before invoking
`Open(player, currentSession)` from its investigation action. This transfers
the W05 interaction focus to the comparison. Active panels release focus
on exit, disable, or replacement of the game session.

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
The test temporarily mounts the panel in the accepted station scene. It
checks the unread archive gate, missing clues, both incorrect orderings,
retained selection across source cards, accepted order, repeat submission,
and the exact transition to sample collection. A second test checks pause,
focus loss, replacement, and restoration of the confirmed order.

The local Windows Player used a temporary mount and actual S07/S09
commands with audio volume at zero. The following frames were inspected.

- [Rejected sequence at 1600 × 900](W11-order-rejected.png)
- [Sensor source card at 800 × 600](W11-source-card.png)
- [Confirmed explanation at 800 × 600](W11-order-confirmed.png)
