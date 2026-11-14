#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using LKS.World;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LKS.Editor.World
{
    public sealed class StationInteractionTests
    {
        private const string ScenePath = "Assets/_Game/Scenes/Station.unity";

        [UnityTest]
        public IEnumerator OnlyVisibleInRangeControlsCanChangeQuestProgress()
        {
            yield return LoadStation();
            var interaction = Object.FindAnyObjectByType<StationInteraction>();
            var player = interaction.GetComponent<StationFirstPerson>();
            var id = StationQuests.RequiredStages[0];
            var anchor = Anchor(id);
            Ray aim = AimAt(anchor);
            var outsideRange = new Ray(anchor.transform.position - aim.direction * (StationInteraction.Range + .1f), aim.direction);
            Assert.That(interaction.FindAimedTarget(outsideRange), Is.Null);
            Assert.That(interaction.Submit(outsideRange), Is.False);
            Assert.That(interaction.FindAimedTarget(aim), Is.SameAs(anchor));
            Assert.That(interaction.Describe(anchor), Is.EqualTo("E  Confirm arrival\nArrival check"));

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = Vector3.Lerp(aim.origin, anchor.transform.position, .5f);
            wall.transform.localScale = Vector3.one * .4f;
            Physics.SyncTransforms();
            Assert.That(interaction.FindAimedTarget(aim), Is.Null);
            Assert.That(interaction.Submit(aim), Is.False);
            Object.Destroy(wall);
            yield return null;

            player.SetPaused(true);
            Assert.That(interaction.Submit(aim), Is.False);
            player.SetPaused(false);
            using (player.AcquireReadingFocus()) Assert.That(interaction.Submit(aim), Is.False);
            Assert.That(interaction.Session.CaptureQuestCheckpoint(), Is.Empty);
            Assert.That(interaction.Submit(aim), Is.True);
            Assert.That(interaction.Feedback, Is.EqualTo("Step complete."));
            Assert.That(interaction.Submit(aim), Is.False);
            Assert.That(interaction.Submit(AimAt(Anchor(StationQuests.RecoveryTarget))), Is.False);
            Assert.That(interaction.Session.CaptureQuestCheckpoint(), Is.EqualTo(new[] { id }));
            Assert.That(interaction.HasDoorPower, Is.False);
        }

        [UnityTest]
        public IEnumerator IncorrectPowerCanBeRecoveredAndReturnNeverTrapsPlayer()
        {
            yield return LoadStation();
            var interaction = Object.FindAnyObjectByType<StationInteraction>();
            var gate = Object.FindAnyObjectByType<StationExteriorGate>();
            var motor = interaction.GetComponent<FirstPersonMotor>();
            var end = Object.FindAnyObjectByType<StationGenerationManifest>().Layout.InteriorEnd;
            var power = interaction.Session.Power;
            Assert.That(interaction.Submit(AimAt(Anchor(StationQuests.RequiredStages[0]))), Is.True);
            Assert.That(power.Commit(new[] { StationPower.Archive }).Succeeded);
            Assert.That(interaction.Describe(Anchor(StationQuests.RequiredStages[1])),
                Does.Contain("Restore air and water"));
            Assert.That(gate.IsOpen, Is.False);
            Place(motor, end - 2);
            Assert.That(Advance(motor, 1), Is.LessThan(end - .9f), "Unpowered leaf must block the corridor.");

            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(interaction.Submit(AimAt(Anchor(StationQuests.RecoveryTarget))), Is.True,
                interaction.Feedback);
            Assert.That(interaction.HasDoorPower, Is.True);
            yield return null;
            Physics.SyncTransforms();
            Assert.That(gate.IsOpen, Is.True);
            Assert.That(Advance(motor, 1), Is.GreaterThan(end + 1));
            Assert.That(power.Commit(new[] { StationPower.Archive }).Succeeded);
            yield return null;
            Physics.SyncTransforms();
            Assert.That(gate.IsOpen, Is.True, "The exterior return must remain usable without power.");
            Assert.That(Advance(motor, -1, 24), Is.LessThan(end - 2));
            yield return null;
            Assert.That(gate.IsOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator CheckpointsRestoreAuthorityAndSceneReloadStartsAFreshRun()
        {
            yield return LoadStation();
            var interaction = Object.FindAnyObjectByType<StationInteraction>();
            Assert.That(interaction.Submit(AimAt(Anchor(StationQuests.RequiredStages[0]))), Is.True);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(interaction.Submit(AimAt(Anchor(StationQuests.RecoveryTarget))), Is.True);
            var saved = interaction.Session;
            var power = saved.Power.Capture();
            var quests = saved.CaptureQuestCheckpoint();
            var life = saved.LifeSupport.Capture();
            var logistics = saved.Logistics.Capture();
            var archive = saved.Archive.Capture();
            var investigation = saved.Investigation.Capture();
            var samples = saved.Samples.Capture();
            interaction.NewRun();
            Assert.That(interaction.HasDoorPower, Is.False);
            Assert.That(interaction.Session.CaptureQuestCheckpoint(), Is.Empty);

            Assert.That(interaction.Restore(power, quests, life, logistics, archive, investigation, samples), Is.True);
            Assert.That(interaction.Session, Is.Not.SameAs(saved));
            Assert.That(interaction.Session.CaptureQuestCheckpoint(), Is.EqualTo(quests));
            Assert.That(interaction.Session.Power.Capture().Requested, Is.EquivalentTo(power.Requested));
            Assert.That(Object.FindAnyObjectByType<StationExteriorGate>().IsOpen);
            yield return LoadStation();
            var reloaded = Object.FindAnyObjectByType<StationInteraction>();
            Assert.That(reloaded.Session.CaptureQuestCheckpoint(), Is.Empty);
            Assert.That(reloaded.HasDoorPower, Is.False);
            Assert.That(Object.FindAnyObjectByType<StationExteriorGate>().IsOpen, Is.False);
        }

        private static IEnumerator LoadStation()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var interaction = Object.FindAnyObjectByType<StationInteraction>();
            Assert.That(interaction.StartError, Is.Null);
            Assert.That(interaction.Session, Is.Not.Null);
            Physics.SyncTransforms();
        }

        private static StationInteractionAnchor Anchor(StableId id) =>
            Object.FindObjectsByType<StationInteractionAnchor>()
                .Single(anchor => anchor.TargetId == id);

        private static Ray AimAt(StationInteractionAnchor anchor)
        {
            Vector3 eye = anchor.Approach.position + Vector3.up * 1.62f;
            return new Ray(eye, (anchor.transform.position - eye).normalized);
        }

        private static void Place(FirstPersonMotor motor, float z)
        {
            var controller = motor.GetComponent<CharacterController>();
            controller.enabled = false;
            motor.transform.position = new Vector3(0, 0, z);
            controller.enabled = true;
            motor.ResetMotion();
            Physics.SyncTransforms();
        }

        private static float Advance(FirstPersonMotor motor, float direction, int steps = 18)
        {
            for (int i = 0; i < steps; i++) motor.Move(new Vector2(0, direction), .08f);
            return motor.transform.position.z;
        }
    }
}
#endif
