# W15 report, transmission and ending handoff

## Station controls

Use the research terminal in bay 05 after reviewing the laboratory results.
Select the station observations and a statement in each of the four remaining
sections. **Submit report** asks S12 to evaluate the draft. Rejected
submissions keep the selected statements. The evidence and feedback cards
show fictional ASTERION results and the separate scientific references. The
report identifies organics as evidence to investigate. It leaves the question
of life open.

After the report is accepted, **Queue accepted report** sends the local S14
command to S06. The status distinguishes an unqueued report, transmitter
power loss, a powered countdown and simulated delivery. Those seconds
belong to the station model. **Return to station** lets the player keep
exploring or supply the transmitter from the power console while it is
queued. **Save progress** writes the S16 campaign slot.

Use the surface overlook to the left of the airlock. Its photograph action
reads the S14 readiness result and requests the existing quest target.
The committed ending is saved before its presentation opens. **View
credits** is deliberate. **Skip to menu** goes directly to the game menu.
The menu can continue the saved campaign, replay a completed ending,
return to the station or start a fresh run. While exploring, open the
game menu from the pause screen.

## Accepted interfaces

| Stable ID | Binding |
| --- | --- |
| `quest.report` | Research console and deliberate S03 confirmation |
| `research.dmitry-report` | S12 accepted report, checkpoint version 1 |
| `message.research-report` | S06 queue through S14 |
| `quest.ending` | S14 photograph action and durable completion |
| `portrait.dmitry-photograph` | W14/DmitryPhoto shared image |
| `camera.final-photograph` | S14 close photograph framing |
| `ending.final` | Dad... I did it. |
| `campaign` | S16 disk slot, schema 2 |

S01 contracts remain at 1.0. S12 owns the report draft and validation.
S14 owns the final cue and completion predicate. S16 owns the atomic save.
Only the committed S14 cue drives the final screen. Replay uses the same
cue and leaves the completion and transmission records intact.

## Integration status

The report, queue, save and completion flow uses accepted S12/S14/S16 on the
W13 station base. The accepted W14 opening is required for verifying the
shared portrait, the full opening-to-ending playthrough, and Continue into
an unfinished opening. The W15 implementation binds the W14 portrait at
`Resources/W14/DmitryPhoto`. The overlook reuses the existing photograph
fixture and anchor IDs. To regenerate its position, use **LKS > World >
Generate W04 Station** after saving open scenes.

## Verification

Run `StationResearchTerminalTests` in the interaction Play Mode suite.
The tests exercise rejected revision, reference-card return, intentional
report acceptance, duplicate queue requests, queued-save restoration,
premature photograph access, the actual route through the airlock, saved
completion, manual credits and replay. The fixture uses a reviewed sample
checkpoint to exercise the W15 scene without repeating the entire campaign.

Unity 6000.6.9f1 passed 20 Play Mode and 85 Edit Mode tests. The editor
route verification checked 13 interaction rays and preserved all 833
existing scene IDs. A muted Windows x64 capture used the installed report
terminal and returned to exploration with the report still queued.

- [Rejected claim at 1600 × 900](W15-report-rejected.png)
- [Fictional result and science reference at 800 × 600](W15-report-source.png)
- [Queued transmission at 1600 × 900](W15-report-queued.png)
- [Queued transmission at 800 × 600](W15-report-queued-small.png)

W16 owns the lighting and silent atmosphere pass after W15 is accepted.
