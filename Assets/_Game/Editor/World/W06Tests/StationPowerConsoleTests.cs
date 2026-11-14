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
    public sealed class StationPowerConsoleTests
    {
        private const string ScenePath = "Assets/_Game/Scenes/Station.unity";

        [UnityTest]
        public IEnumerator PreviewAndConfirmationUseTheActiveSession()
        {
            yield return LoadStation();
            var console = Object.FindAnyObjectByType<StationPowerConsole>();
            var interaction = console.GetComponent<StationInteraction>();
            var player = console.GetComponent<StationFirstPerson>();
            Ray powerCabinet = AimAt(StationPowerConsole.Target);
            Assert.That(interaction.Submit(powerCabinet), Is.False);

            Assert.That(interaction.Submit(AimAt(StationQuests.RequiredStages[0])), Is.True);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(interaction.Submit(powerCabinet), Is.True);
            Assert.That(player.HasReadingFocus);
            Assert.That(console.ConfirmLifeSupport(), Is.False);
            var power = interaction.Session.Power;
            Assert.That(console.Live, Is.SameAs(power.Snapshot));
            foreach (var load in new[] { StationPower.Air, StationPower.Water, StationPower.Archive, StationPower.Lab })
                Assert.That(console.Toggle(load), Is.True);
            Assert.That(power.Snapshot.Loads.All(load => !load.Requested));
            Assert.That(console.Proposed.DemandKilowatts, Is.EqualTo(8));
            Assert.That(console.Proposed.GenerationKilowatts, Is.EqualTo(6));
            Assert.That(console.Explanation, Does.Contain("battery").And.Contain("one at a time"));

            player.SetPaused(true);
            Assert.That(console.IsOpen && player.HasReadingFocus);
            Assert.That(console.Apply(), Is.False);
            Assert.That(console.Toggle(StationPower.Air), Is.False);
            player.SetPaused(false);
            player.SendMessage("OnApplicationFocus", false);
            Assert.That(console.Apply(), Is.False);
            player.SendMessage("OnApplicationFocus", true);
            Assert.That(console.Apply(), Is.True);
            Assert.That(console.Feedback, Is.EqualTo("Allocation applied."));
            Assert.That(console.Live, Is.SameAs(power.Snapshot));
            Assert.That(console.ConfirmLifeSupport(), Is.True);
            Assert.That(console.ConfirmLifeSupport(), Is.False);
            Assert.That(console.Apply(), Is.False);
            Assert.That(interaction.Session.Quests.Snapshot[StationPowerConsole.Target], Is.EqualTo(QuestStatus.Completed));

            Assert.That(console.Toggle(StationPower.Transmit), Is.True);
            console.enabled = false;
            Assert.That(console.IsOpen || player.HasReadingFocus, Is.False);
            Assert.That(console.Apply(), Is.False);
            Assert.That(power.Snapshot.Loads.Single(load => load.Id == StationPower.Transmit).Requested, Is.False);
            console.enabled = true;
            Assert.That(interaction.Submit(powerCabinet), Is.True, "Console must remain available after life support is confirmed.");
            Assert.That(console.Proposed.Loads.Single(load => load.Id == StationPower.Transmit).Requested, Is.False);

            interaction.NewRun();
            Assert.That(console.IsOpen || player.HasReadingFocus, Is.False);
            Assert.That(console.Apply(), Is.False);
            Assert.That(interaction.Session.Power, Is.Not.SameAs(power));
            yield return LoadStation();
            var reloaded = Object.FindAnyObjectByType<StationPowerConsole>();
            Assert.That(console == null);
            Assert.That(reloaded.IsOpen, Is.False);
            Assert.That(reloaded.GetComponent<StationInteraction>().Session.Power.Snapshot.DemandKilowatts, Is.Zero);
        }

        [UnityTest]
        public IEnumerator EmptyBatteryShowsShedLoadsAndRejectsTheSameOverdraw()
        {
            yield return LoadStation();
            var console = Object.FindAnyObjectByType<StationPowerConsole>();
            var interaction = console.GetComponent<StationInteraction>();
            var bootstrap = console.GetComponent<SessionBootstrap>();
            var clock = new ManualClock();
            var catalog = interaction.Session.Science;
            var essentials = new[] { StationPower.Air, StationPower.Water };
            Assert.That(bootstrap.RestartWithPower(catalog, Array.Empty<StableId>(),
                () => Array.Empty<IInteractable>(), _ => ResultCode.EvidenceInsufficient,
                new PowerCheckpoint(.05m, 0, essentials), new[] { StationQuests.RequiredStages[0] }, clock), Is.Empty);
            var active = bootstrap.Current;
            var power = active.Power;
            Assert.That(console.Open(), Is.True);
            Assert.That(console.Toggle(StationPower.Archive) && console.Toggle(StationPower.Lab));
            Assert.That(console.Apply(), Is.True);

            clock.Seconds = 8.1;
            active.Tick();
            active.Dispatch();
            Assert.That(power.Capture().BatteryKwh, Is.EqualTo(.05m), "Reading pauses battery consumption.");
            console.Close();
            console.GetComponent<StationFirstPerson>().ResumePointer();
            yield return null;
            clock.Seconds = 16.2;
            active.Tick();
            active.Dispatch();
            Assert.That(power.Capture().BatteryKwh, Is.Zero);
            Assert.That(console.Open(), Is.True);
            Assert.That(console.Live.BatteryKilowattHours, Is.Zero);
            var laboratory = console.Live.Loads.Single(load => load.Id == StationPower.Lab);
            Assert.That(laboratory.Requested && !laboratory.Supplied);
            Assert.That(console.RecoveryHint, Does.Contain("Restore Safe Power"));
            Assert.That(console.Toggle(StationPower.Seedlings));
            Assert.That(console.PreviewResult.Code, Is.EqualTo(ResultCode.InsufficientResources));
            Assert.That(console.Explanation, Does.Contain("not enough power"));
            Assert.That(console.Apply(), Is.False);
            Assert.That(console.Feedback, Is.EqualTo("There is not enough power for these loads."));
            Assert.That(power.Snapshot.Loads.Single(load => load.Id == StationPower.Seedlings).Requested, Is.False);

            Assert.That(console.Toggle(StationPower.Seedlings));
            Assert.That(console.Toggle(StationPower.Lab));
            Assert.That(console.Apply(), Is.True);
            Assert.That(console.Live.Loads.All(load => !load.Requested || load.Supplied));
            Assert.That(power.EssentialSupportSupplied);
        }

        private static IEnumerator LoadStation()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var player = Object.FindAnyObjectByType<StationFirstPerson>();
            player.SendMessage("OnApplicationFocus", true);
            Physics.SyncTransforms();
        }

        private static Ray AimAt(StableId target)
        {
            var anchor = Object.FindObjectsByType<StationInteractionAnchor>().Single(item => item.TargetId == target);
            Vector3 eye = anchor.Approach.position + Vector3.up * 1.62f;
            return new Ray(eye, (anchor.transform.position - eye).normalized);
        }

        private sealed class ManualClock : ISessionClock
        {
            public double Seconds { get; set; }
        }
    }
}
#endif
