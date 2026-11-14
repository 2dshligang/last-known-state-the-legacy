#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using LKS.World;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace LKS.Editor.World
{
    public sealed class StationShelterSetTests
    {
        private const string ScenePath = "Assets/_Game/Scenes/Station.unity";
        private static readonly StableId Arrival = StationQuests.RequiredStages[0];

        [UnityTest]
        public IEnumerator ShutterStopsResumesAndLatchesWhileSideAccessRemainsPassable()
        {
            yield return LoadStation();
            var set = Object.FindAnyObjectByType<StationShelterSet>();
            var interaction = set.GetComponent<StationInteraction>();
            var player = set.GetComponent<StationFirstPerson>();
            var clock = new Clock();
            var session = Start(set, clock, new[] { StationPower.Air, StationPower.Water });
            var support = session.LifeSupport;
            var power = session.Power;
            Vector3 parked = Node("shelter.shutter").position;
            Vector3 closed = Node("shelter.shutter-stop").position;
            Assert.That(interaction.Submit(AimAt(StationShelterSet.ShelterTarget)), Is.True);
            Assert.That(player.HasReadingFocus);
            Assert.That(set.RequestShelter(), Is.False);
            Assert.That(set.Feedback, Does.Contain("actuator"));
            Assert.That(set.SetActuatorPower(true), Is.True);
            Assert.That(set.RequestShelter(), Is.True);
            player.SetPaused(true);
            clock.Advance(session, 1);
            Assert.That(support.Capture().ActuatorSeconds, Is.Zero);
            Assert.That(set.RequestShelter(), Is.False);
            player.SetPaused(false);
            clock.Advance(session, .3);
            Assert.That(support.Capture().ActuatorSeconds, Is.Zero, "Returning to the panel keeps reading pause.");

            set.Close();
            player.ResumePointer();
            yield return null;
            clock.Advance(session, .2);
            yield return null;
            var partial = Node("shelter.shutter").position;
            Assert.That(partial.z, Is.GreaterThan(parked.z).And.LessThan(closed.z));
            Assert.That(power.RequestLoad(StationPower.ShelterActuator, false).Succeeded);
            clock.Advance(session, .5);
            yield return null;
            Assert.That(support.ReadShelter().Phase, Is.EqualTo(ShelterPhase.Interrupted));
            Assert.That(Node("shelter.shutter").position, Is.EqualTo(partial));
            Assert.That(interaction.Submit(AimAt(StationShelterSet.ShelterTarget)), Is.True);
            Assert.That(set.SetActuatorPower(true), Is.True);
            set.Close();
            player.ResumePointer();
            yield return null;
            clock.Advance(session, .6);
            yield return null;
            Assert.That(support.ReadShelter().Phase, Is.EqualTo(ShelterPhase.Sheltered));
            Assert.That(interaction.Submit(AimAt(StationShelterSet.ShelterTarget)), Is.True);
            Assert.That(set.SetActuatorPower(false), Is.True);
            set.Close();
            player.ResumePointer();
            yield return null;
            clock.Advance(session, .5);
            yield return null;
            Assert.That(Node("shelter.shutter").position, Is.EqualTo(closed));
            Assert.That(support.ReadShelter().ActuatorPowered, Is.False);
            Assert.That(support.ReadShelter().Phase, Is.EqualTo(ShelterPhase.Sheltered));

            var motor = set.GetComponent<FirstPersonMotor>();
            Vector3 side = Node("shelter.side-approach").position;
            Vector3 inside = Node("shelter.interior").position;
            Walk(motor, side);
            Walk(motor, inside);
            Walk(motor, side);
            Physics.SyncTransforms();
            Assert.That(Physics.Raycast(new Vector3(0, 1.1f, closed.z), Vector3.right, out var obstruction, 2));
            Assert.That(obstruction.collider.name, Is.EqualTo("shelter.shutter"));

            var checkpoint = support.Capture();
            Assert.That(Restore(interaction, session), Is.True);
            yield return null;
            Assert.That(interaction.Session.LifeSupport.Capture().ActuatorSeconds, Is.EqualTo(checkpoint.ActuatorSeconds));
            Assert.That(Node("shelter.shutter").position, Is.EqualTo(closed));
            yield return LoadStation();
            var reloaded = Object.FindAnyObjectByType<StationShelterSet>();
            Assert.That(reloaded.GetComponent<StationInteraction>().Session.LifeSupport.Capture().ShelterRequested, Is.False);
            Assert.That(Node("shelter.shutter").position, Is.EqualTo(parked));
        }

        [UnityTest]
        public IEnumerator LogisticsChoicesRecoverFromOverdrawAndRestoreFromCheckpoint()
        {
            yield return LoadStation();
            var set = Object.FindAnyObjectByType<StationShelterSet>();
            var interaction = set.GetComponent<StationInteraction>();
            var player = set.GetComponent<StationFirstPerson>();
            var session = Start(set, new Clock(), new[] { StationPower.Air, StationPower.Water, StationPower.Archive });
            Assert.That(interaction.Submit(AimAt(StationShelterSet.SeedlingTarget)), Is.True);
            Assert.That(set.Choose(FoodSchedule.PreserveSeedlings), Is.True);
            Assert.That(session.Logistics.SeedlingPlanPowered);
            Assert.That(set.FoodStatus, Does.Contain("preservation is powered"));
            Assert.That(set.Choose(FoodSchedule.RunLab), Is.False);
            Assert.That(set.Feedback, Does.Contain("not enough power"));
            Assert.That(session.Logistics.Capture().Schedule, Is.EqualTo(FoodSchedule.PreserveSeedlings));
            player.SetPaused(true);
            Assert.That(set.Choose(FoodSchedule.RunLab), Is.False);
            player.SetPaused(false);
            set.Close();
            Assert.That(set.Choose(FoodSchedule.RunLab), Is.False);
            Assert.That(interaction.Submit(AimAt(StationQuests.RecoveryTarget)), Is.True);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(interaction.Submit(AimAt(StationShelterSet.SeedlingTarget)), Is.True);
            Assert.That(set.Choose(FoodSchedule.RunLab), Is.True);
            Assert.That(set.FoodStatus, Does.Contain("lab is powered"));
            Assert.That(session.Power.Snapshot.Loads.Single(load => load.Id == StationPower.Seedlings).Requested, Is.False);
            Assert.That(session.Power.EssentialSupportSupplied);
            Assert.That(set.Choose(FoodSchedule.PreserveSeedlings), Is.True);
            Assert.That(session.Power.Snapshot.Loads.Single(load => load.Id == StationPower.Lab).Requested, Is.False);
            Assert.That(Restore(interaction, session), Is.True);
            Assert.That(set.IsOpen || player.HasReadingFocus, Is.False);
            Assert.That(interaction.Session.Logistics.SeedlingPlanPowered);
        }

        private static IEnumerator LoadStation()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            Object.FindAnyObjectByType<StationFirstPerson>().SendMessage("OnApplicationFocus", true);
            Physics.SyncTransforms();
        }

        private static GameplaySession Start(StationShelterSet set, Clock clock, StableId[] loads)
        {
            var bootstrap = set.GetComponent<SessionBootstrap>();
            var errors = bootstrap.RestartWithPower(bootstrap.Current.Science, Array.Empty<StableId>(),
                () => Array.Empty<IInteractable>(), _ => ResultCode.EvidenceInsufficient,
                new PowerCheckpoint(0m, 0m, loads), new[] { Arrival }, clock,
                lifeSupportCheckpoint: new LifeSupportCheckpoint(1m, 10m, 0m, false));
            Assert.That(errors, Is.Empty);
            return bootstrap.Current;
        }

        private static bool Restore(StationInteraction interaction, GameplaySession saved) =>
            interaction.Restore(saved.Power.Capture(), saved.CaptureQuestCheckpoint(), saved.LifeSupport.Capture(),
                saved.Logistics.Capture(), saved.Archive.Capture(), saved.Investigation.Capture(),
                saved.Samples.Capture(), saved.Research.CaptureAccepted());

        private static Transform Node(string id) => Object.FindAnyObjectByType<StationGenerationManifest>().Entries
            .Single(item => item.id.value == id).node.transform;

        private static Ray AimAt(StableId id)
        {
            var anchor = Object.FindObjectsByType<StationInteractionAnchor>().Single(item => item.TargetId == id);
            Vector3 eye = anchor.Approach.position + Vector3.up * 1.62f;
            return new Ray(eye, (anchor.transform.position - eye).normalized);
        }

        private static void Walk(FirstPersonMotor motor, Vector3 goal)
        {
            for (int i = 0; i < 190; i++)
            {
                var delta = goal - motor.transform.position;
                delta.y = 0;
                if (delta.magnitude < .035f) return;
                var local = motor.transform.InverseTransformDirection(delta);
                motor.Move(Vector2.ClampMagnitude(new Vector2(local.x, local.z) / .20f, 1), .06f);
            }
            Assert.Fail($"Could not reach shelter waypoint {goal:F2} from {motor.transform.position:F2}.");
        }

        private sealed class Clock : ISessionClock
        {
            public double Seconds { get; private set; }

            public void Advance(GameplaySession session, double duration)
            {
                Seconds += duration;
                session.Tick();
                session.Dispatch();
            }
        }
    }
}
#endif
