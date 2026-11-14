# W06 station interaction handoff

## Integration

Open `Assets/_Game/Scenes/Station.unity` and enter Play. The committed scene
already contains the W06 bindings. If W04 layout dimensions or W05 player
bindings change, save the scene and run **LKS > World > Install W06 Station
Interaction**. The installer validates required IDs and updates the existing
bindings. The exterior door remains a separate prefab instance outside the W04
generated hierarchy, so regeneration preserves its identity and manual scene
objects.

W06 consumes S01 contracts 1.0, S03 `StationQuests`, S04 `StationPower`, and
the accepted S05–S11 services through `SessionBootstrap.RestartWithPower`.
It validates the S10 catalog record `donki.2017-09-04t23-52-00-sep-001` at
startup. The archived record is an observation. Recovery and the physical
airlock are fictional station presentation driven by the station's engineering
power model.

| Stable ID | Station binding |
| --- | --- |
| `quest.arrival` | Arrival check |
| `quest.safe-power` | Power control |
| `target.power-recovery` | Recovery control across the corridor from power control |
| `quest.echo-evidence` | ECHO archive |
| `quest.sampling` | Exterior sample pickup |
| `quest.analysis` | Laboratory |
| `quest.report` | Research report |
| `quest.ending` | Photograph |
| `door.exterior-airlock` | Physical airlock at the exterior exit |

World input comes from W05's `StationFirstPerson.InteractPressed`. The same
2.4 m aim and collider check serves the prompt and the command. W06 uses
`IInteractable.Read` for the action ID or reason, then calls
`IInteractable.Request` as `actor.alexei`. Objectives use the current
quest snapshot. The exterior airlock follows
`StationPower.EssentialSupportSupplied` and retains mechanical return when
approached from outside. Its leaf uses W03 `AirlockPresentation`; door
movement sends no gameplay commands.

## Verification and next owner

- Unity 6000.6.9f1 Play Mode runs `LKS.World.Interaction.Tests` and
  `LKS.World.FirstPerson.Tests` against the actual scene. Checks cover
  occlusion, distance, repeat input, modal focus, successful recovery after a
  nonessential power allocation, the blocked exit, safe exterior return,
  checkpoint restoration and scene reload. The checkpoint test captures the
  accepted services' snapshots and restores them through W06.
- Local results were 6/6 in Play Mode, 50/50 in Edit Mode and a successful
  Windows x64 station preview build. The W04 geometry and generator
  verification passed, including regeneration with manual scene content.
  A second W06 install reported no scene changes.
- To review the URP stills, use
  `LKS.Editor.World.W06StationSetup.RenderPreview` in graphics-enabled batch
  mode and set `LKS_W06_PREVIEWS` to an existing absolute output directory.
  The method produces `W06-airlock-closed.png` and `W06-airlock-powered.png`
  with the real scene camera and reused airlock mesh.
- For an interactive check in Play, aim at the orange face of Arrival Check,
  press E, follow the objective to Restore Safe Power, and press E at that
  control. Confirm safe life support at Power Control, then walk through
  the exterior airlock. Escape opens pause. **Restart station** creates a
  fresh gameplay session and returns the player to arrival.
- Developer 1 continues in W07 with the allocation UI through
  `StationInteraction.Session.Power`. W09–W13 provide the archive and sample
  evidence actions. Their quest confirmations already use the accepted
  evidence gates. Report and photograph confirmations wait for the later
  authoritative report and ending integration.
- Developer 2 owns S16 disk persistence. The tested integration point is
  `StationInteraction.Restore` with the accepted power, quest, life support,
  logistics, archive, investigation and sample checkpoint types. Restore
  places the player at arrival. Disk serialization, UI and position restore
  remain outside this verified checkpoint path.
