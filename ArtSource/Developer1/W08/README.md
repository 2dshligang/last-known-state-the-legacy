# W08 shelter and seedling cabinet

## Station route

The installed setup is in the shelter bay of `Assets/_Game/Scenes/Station.unity`.
In Play Mode, approach the orange shelter control and press E. Read the
training briefing and the separately dated historical source. Power the
actuator, request shutter closure, and close the panel to resume simulation.
The marked side access remains walkable with the shutter open, interrupted
or latched. The fixed walls and roof depict passive shielding. After closure,
the player can release the actuator load while the shutter stays latched.

Across the corridor, press E at the seedling cabinet. **Preserve seedlings**
uses the returning expedition's starter material for a future growing plan.
**Run the laboratory** powers analysis and defers seedling care. If a choice is
rejected, shed optional loads in the power bay or use **Restore Safe Power**
before retrying. The cabinet represents preservation during the mission.
The educational explanation comes from S06 and is grounded in the
[CSA food-production materials](https://www.asc-csa.gc.ca/eng/youth-educators/toolkits/food-production/).

To reinstall after changing the layout, save all scenes and run **LKS >
World > Install W08 Shelter and Seedlings**. The existing W04 generator
retains object references and adds the W08 geometry by stable ID.

## Service bindings

| Stable ID | Role |
| --- | --- |
| `target.shelter-controls` | Local presentation anchor. Reads S05 and submits `RequestShelter` with a fresh `ShelterReading`. |
| `target.seedling-plan` | Local presentation anchor. Uses S06 `StationLogistics.Choose` and displays the resulting schedule and supply. |
| `load.shelter-actuator` | Existing S05/S04 load. The console calls `IPowerService.RequestLoad` on the active station power service. |
| `load.seedling-preservation`, `load.lab` | Existing S06 alternatives. S06 owns the allocation change. |
| `shelter.shutter`, `shelter.shutter-stop` | W04 generated parked shutter and closed pivot. Travel comes from S05 actuator seconds. |
| `training.donki-sep-2017` | Fictional practice alert, using S05's simplified timing. |
| `nasa.donki-sep-13000` | [NASA DONKI record 13000](https://ccmc.gsfc.nasa.gov/DONKI/view/SEP/13000/-1), a historical observation at 2017-09-04 23:52 UTC. |

Uses S01 API 1.0, S05 life support checkpoint v1 and S06 logistics
checkpoint v1 through the W06 session. The shutter travel uses S05's
five model seconds to map the authoritative checkpoint to the two poses.
The reading lease pauses simulation and suspends world input. Controls
reject actions while paused, unfocused, closed or bound to an old session.
Developer 2's S16 owns on-disk persistence. Developer 1 continues with
the archive presentation when its dependencies are accepted.

## Verification

Run `StationShelterSetTests` in the W06 interaction Play Mode assembly and
the standard Edit Mode suite. The scene tests follow both plans, recover
from rejected allocation, pause a requested closure, interrupt and resume
the shutter, release actuator power after latching, walk the physical side
route in both directions, and verify restoring checkpoints and starting a
fresh scene. The W04 verification tests repeatable generation, manual
object preservation and interaction-ray clearance.

On the accepted S15 base, Unity 6000.6.9f1 passed 10 Play Mode and 69 Edit
Mode tests. A repeat install left the scene unchanged, and the Windows
x64 station preview built. The visual captures used the actual Windows
Player with controlled S05/S06 checkpoints.

- [Shelter control with an interrupted actuator](W08-shelter-panel.png)
- [Seedling choice at 800 × 600](W08-seedlings.png)
- [Latched shutter and marked side access](W08-shelter-world.png)
