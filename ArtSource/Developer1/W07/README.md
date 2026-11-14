# W07 power console

## Scene and controls

Open `Assets/_Game/Scenes/Station.unity` in Play Mode. Confirm Arrival Check,
then aim at the Power Control cabinet and press E. Use the **Proposed status**
buttons to change the selection. **Apply allocation** submits the whole set.
**Confirm safe life support** advances the route once the active quest
recognizes a safe air reserve. If the reserve is low, close the panel and let
air and water run. **Restore Safe Power** across the corridor still provides
the S03 recovery action. The console stays available after confirmation.

With the panel open, use Tab and Shift+Tab to move between available buttons,
Enter to choose, or use the mouse. Escape opens the normal pause menu.
The W05 reading lease pauses battery and life-support simulation and prevents
movement and world actions. Closing, disabling, or restarting the station
discards unsubmitted choices and releases the lease and subscription.

The scene is wired. To restore the bindings after altering the scene, save
all scenes and run **LKS > World > Install W06 Station Interaction**. The
installer preserves the existing W04 targets and manual scene content.

## Authoritative integration

- S01 contract version 1.0, accepted S03/S04 APIs, stable cabinet ID
  `quest.safe-power`, recovery ID `target.power-recovery`, player
  `actor.alexei`. Confirmation uses the existing `IInteractable` action.
- The display reads and subscribes through `IPowerService`. S04 exposes
  atomic allocation `Preview` and `Commit` on the session's implementing
  `StationPower` class. Those calls use the same complete selection.
  All generation, demand, energy and supplied states come from its snapshots.
- Displayed loads use `load.air`, `load.water`, `load.seedling-preservation`,
  `load.archive`, `load.lab`, `load.shelter-actuator` and `load.transmit`.
  Values are identified as the simplified fictional station model described
  in [the reviewed engineering assumptions](../../../Automation/samples/science/engineering-model.json).
  The on, off and no-power labels and written recovery hint work when muted.
- S04 owns checkpoint v1. Opening the console after a scene or session restart
  binds to the active service. S16 owns persistent saves. Developer 1's W08
  shelter and seedling views should reuse the current session's power,
  life-support and logistics services.

## Verification

Run the W06 interaction Play Mode assembly, including
`StationPowerConsoleTests`, and the standard Edit Mode suite in Unity
6000.6.9f1. W07 verifies the actual scene, preview without mutation,
atomic apply, quest confirmation, guarded inputs, restart and scene reload.
A test advances an accepted nearly empty checkpoint to zero while the panel
is closed, verifies the lab was shed, checks the refusal and applies a
viable selection.

For a manual check at 1920 × 1080 and 800 × 600, preview air, water, archive
and lab together. Check the generation, proposed demand, battery units,
load statuses and the warning. Open pause over the panel, return to reading,
apply the choice and close the panel. The reserve drains while walking.
For a fast empty-battery check, use the fixture in
[`StationPowerConsoleTests.cs`](../../../Assets/_Game/Editor/World/W06Tests/StationPowerConsoleTests.cs).
