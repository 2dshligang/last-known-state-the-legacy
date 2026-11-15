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
    public sealed class StationEchoInvestigationTests
    {
        private const string ScenePath = "Assets/_Game/Scenes/Station.unity";
        private static readonly StableId RequiredArchive = new StableId("archive.donki-sep");
        private static readonly StableId Quest = new StableId("quest.echo-evidence");
        private static readonly StableId Timing = EchoInvestigation.Timing;
        private static readonly StableId Decision = EchoInvestigation.Decision;
        private static readonly StableId Outage = EchoInvestigation.Consequence;

        [UnityTest]
        public IEnumerator InspectionOrderingAndQuestConfirmationUseTheAuthoritativeServices()
        {
            yield return LoadStation();
            var player = Object.FindAnyObjectByType<StationFirstPerson>();
            var bridge = player.GetComponent<StationInteraction>();
            var panel = player.gameObject.AddComponent<StationEchoInvestigation>();
            var session = StartAtInvestigation(player);
            Assert.That(panel.Open(player, () => bridge.Session));
            Assert.That(panel.Checkpoint.Collected, Is.Empty);
            Assert.That(session.Investigation.Conclusion, Is.EqualTo(EchoConclusion.Unexamined));

            Press(panel, KeyCode.Return);
            Assert.That(panel.Source, Is.EqualTo(Outage));
            Assert.That(panel.Feedback, Is.EqualTo("Read and mark the required archive entry first."));
            Assert.That(panel.Checkpoint.Collected, Is.Empty);
            Press(panel, KeyCode.Return);
            Assert.That(panel.Source.IsValid, Is.False);
            Assert.That(panel.ConfirmQuest(), Is.False);
            Acknowledge(session.Archive);

            Collect(panel, Decision);
            Assert.That(panel.ToggleOrder(Decision));
            Assert.That(panel.SubmitOrder(), Is.False);
            Assert.That(panel.Feedback, Does.Contain("Collect all three clues"));
            Assert.That(panel.Draft, Is.EqualTo(new[] { Decision }));
            Assert.That(panel.Checkpoint.Collected, Is.EqualTo(new[] { Decision }));

            Assert.That(panel.Inspect(Timing));
            Assert.That(panel.Source, Is.EqualTo(Timing));
            Assert.That(panel.Draft, Is.EqualTo(new[] { Decision }));
            Assert.That(panel.ReturnToComparison());
            Assert.That(panel.Draft, Is.EqualTo(new[] { Decision }));
            Collect(panel, Outage);
            Assert.That(panel.ToggleOrder(Timing) && panel.ToggleOrder(Outage));
            Assert.That(panel.InspectContext());
            Assert.That(panel.Source.value, Is.EqualTo("echo.backup-estimate"));
            Assert.That(panel.ReturnToComparison());
            Assert.That(panel.Draft, Is.EqualTo(new[] { Decision, Timing, Outage }));

            Assert.That(panel.SubmitOrder(), Is.False);
            Assert.That(panel.Feedback, Is.EqualTo("Put the old reading before the decision that used it."));
            Assert.That(panel.Draft, Is.EqualTo(new[] { Decision, Timing, Outage }));
            Assert.That(panel.ToggleOrder(Decision) && panel.ToggleOrder(Decision));
            Assert.That(panel.SubmitOrder(), Is.False);
            Assert.That(panel.Feedback, Is.EqualTo("Put the controller decision before the backup power ran out."));
            Assert.That(panel.Draft, Is.EqualTo(new[] { Timing, Outage, Decision }));
            Assert.That(panel.Checkpoint.Collected, Is.EquivalentTo(EchoInvestigation.RequiredClues));

            Assert.That(panel.ToggleOrder(Outage) && panel.ToggleOrder(Outage));
            Assert.That(panel.SubmitOrder());
            Assert.That(panel.Feedback, Is.EqualTo("The old reading guided the controller decision. Backup power later ran out."));
            Assert.That(session.Investigation.Conclusion, Is.EqualTo(EchoConclusion.Confirmed));
            Assert.That(session.Quests.Snapshot[Quest], Is.EqualTo(QuestStatus.Available));
            Assert.That(panel.SubmitOrder());
            Assert.That(panel.Feedback, Is.EqualTo("This explanation is already confirmed."));
            Assert.That(panel.ConfirmQuest());
            Assert.That(session.Quests.Snapshot[Quest], Is.EqualTo(QuestStatus.Completed));
            Assert.That(session.Quests.Snapshot[StationQuests.RequiredStages[3]], Is.EqualTo(QuestStatus.Available));
            Assert.That(panel.ConfirmQuest(), Is.False);
            Assert.That(panel.Feedback, Is.EqualTo("This step is already complete."));
            Assert.That(session.CaptureQuestCheckpoint().Count, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator PausedOrReplacedViewsCannotSubmitAndTheConfirmedOrderRestores()
        {
            yield return LoadStation();
            var player = Object.FindAnyObjectByType<StationFirstPerson>();
            var bridge = player.GetComponent<StationInteraction>();
            var panel = player.gameObject.AddComponent<StationEchoInvestigation>();
            var session = StartAtInvestigation(player);
            Acknowledge(session.Archive);
            Assert.That(panel.Open(player, () => bridge.Session));
            foreach (var id in new[] { Timing, Decision, Outage })
            {
                Collect(panel, id);
                Assert.That(panel.ToggleOrder(id));
            }
            player.SetPaused(true);
            try
            {
                Assert.That(panel.SubmitOrder(), Is.False);
                Assert.That(panel.ConfirmQuest(), Is.False);
            }
            finally { player.SetPaused(false); }
            player.SendMessage("OnApplicationFocus", false);
            Assert.That(panel.SubmitOrder(), Is.False);
            player.SendMessage("OnApplicationFocus", true);
            Assert.That(panel.SubmitOrder());
            var ordered = session.Investigation.Capture().AcceptedOrder;
            Assert.That(StationEchoInvestigation.TimeOf(StationEchoInvestigation.RecordOf(Timing)), Is.EqualTo("14:16 UTC"));
            Assert.That(StationEchoInvestigation.TimeOf(StationEchoInvestigation.RecordOf(Decision)), Is.EqualTo("14:20 UTC"));
            Assert.That(StationEchoInvestigation.SourceType(StationEchoInvestigation.RecordOf(new StableId("echo.backup-estimate"))),
                Is.EqualTo("Model estimate"));
            Assert.That(bridge.Restore(session.Power.Capture(), session.CaptureQuestCheckpoint(),
                session.LifeSupport.Capture(), session.Logistics.Capture(), session.Archive.Capture(),
                session.Investigation.Capture(), session.Samples.Capture(), session.Research.CaptureAccepted()));
            Assert.That(panel.IsOpen || player.HasReadingFocus, Is.False);
            Assert.That(panel.SubmitOrder(), Is.False);
            Assert.That(panel.Open(player, () => bridge.Session));
            Assert.That(panel.Draft, Is.EqualTo(ordered));
            Assert.That(bridge.Session.Investigation.Conclusion, Is.EqualTo(EchoConclusion.Confirmed));
            panel.enabled = false;
            Assert.That(panel.IsOpen || player.HasReadingFocus, Is.False);
            Assert.That(panel.ConfirmQuest(), Is.False);
        }

        private static IEnumerator LoadStation()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            Object.FindAnyObjectByType<StationFirstPerson>().SendMessage("OnApplicationFocus", true);
        }

        private static GameplaySession StartAtInvestigation(StationFirstPerson player)
        {
            var bootstrap = player.GetComponent<SessionBootstrap>();
            Assert.That(bootstrap.RestartWithPower(bootstrap.Current.Science, Array.Empty<StableId>(),
                () => Array.Empty<IInteractable>(), _ => ResultCode.EvidenceInsufficient,
                questCheckpoint: StationQuests.RequiredStages.Take(2)), Is.Empty);
            return bootstrap.Current;
        }

        private static void Acknowledge(StationArchive archive)
        {
            using var document = archive.Open(RequiredArchive);
            Assert.That(archive.Acknowledge(RequiredArchive).Succeeded);
        }

        private static void Collect(StationEchoInvestigation panel, StableId clue)
        {
            Assert.That(panel.Inspect(clue));
            Assert.That(panel.Checkpoint.Collected, Does.Contain(clue));
            Assert.That(panel.ReturnToComparison());
        }

        private static void Press(StationEchoInvestigation panel, KeyCode key) =>
            panel.HandleKeys(new Event { type = EventType.KeyDown, keyCode = key });
    }
}
#endif
