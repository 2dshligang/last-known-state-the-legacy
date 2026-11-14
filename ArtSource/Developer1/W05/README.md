# W05 first-person walkthrough

Open `Assets/_Game/Scenes/Station.unity` in Unity 6000.6.9f1 and press Play.
Click **Click to resume** if the Game view started without input focus.
Use WASD or the arrow keys to walk, the mouse to look, E to request an
interaction, and Escape to pause. Sensitivity and vertical inversion are in
the pause menu and are stored locally.

## Scene binding

The `First Person` instance sits outside W04's generated subtree. It takes
its position and rotation from the existing `station.spawn` reference.
The old preview camera is disabled so that the player camera renders the
scene. **LKS > World > Install W05 First Person** can install or refresh
these bindings. The installer protects unsaved scenes and refuses to
replace an unrelated object at its destination.

World movement and look are separate from the edge-triggered
`StationFirstPerson.InteractPressed` event. Its listener should resolve a
reachable W04 anchor and submit an explicit request through the S01/S03
contracts. The existing eight target IDs are preserved. W05's overlay counts
requests so the two modal previews can verify that blocked input does not
escape into world interactions.

Terminal and archive views should acquire a lease with
`StationFirstPerson.AcquireReadingFocus` and dispose it on close or disable.
Escape can open pause above a reader; resuming leaves that reader in charge
until it closes. Restart revokes pending leases. After loss of application
focus, walk and look stay blocked until the player clicks **Click to resume**.
The first frames after capture discard interaction and look input.

The prefab carries the accepted S02 `SessionBootstrap` as the integration
point. W05 does not supply a catalog or start a gameplay run. For a running
session the controller uses `GameplaySession.SetPowerReadingPaused` so
focused reading, pause and loss of focus suspend power and life-support
advance. W06 and later can bind the accepted session services without
changing this input boundary. Remove the temporary preview overlay as
production views replace it.

## Verification

- The PlayMode suite `LKS.World.FirstPerson.Tests` loads the actual station
  and uses its character capsule to walk both branches of the exterior loop,
  visit the interior approaches and reach the return laboratory. An isolated
  platform tests a 0.20 m step, a 19-degree slope and a closed barrier with
  250 ms movement ticks.
- The focus scenario checks overlapping readers, pause above a reader,
  suppressed interaction delivery, focus loss and deliberate reacquisition,
  restart at spawn, and actual scene reload. The test drives the input
  callback boundary. The Windows preview launched, rendered and switched
  from the resume overlay to the captured state on click. Direct keyboard,
  mouse-look and Alt+Tab verification remains a manual check because
  another application took the desktop during the windowed session.
- The Windows preview contains one scene. Use
  `LKS.Editor.World.W05StationPlayerSetup.BuildStationPreview` from Unity
  batch mode with `LKS_W05_PLAYER` set to a new absolute executable path.
  The main build scene list is unchanged.

For manual review, check mouse look after focus returns, both preview
windows, resume while reading, Restart walk and Reload station. Full game
save restore is owned by S16. Gameplay focus of the actual console and archive
can be checked once those views are connected.
