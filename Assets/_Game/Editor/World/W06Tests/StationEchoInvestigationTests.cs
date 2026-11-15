#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
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
            var panel = player.GetComponent<StationEchoInvestigation>();
            var reader = player.GetComponent<StationArchiveReader>();
            var review = player.GetComponent<StationEchoReview>();
            yield return CompleteArrivalAndPower(bridge);
            var session = bridge.Session;

            StationEchoReviewTests.OpenReview(bridge, reader, review);
            player.SetPaused(true);
            try { Assert.That(review.OpenInvestigation(), Is.False); }
            finally { player.SetPaused(false); }
            panel.enabled = false;
            Assert.That(review.OpenInvestigation(), Is.False);
            Assert.That(review.IsOpen && player.HasReadingFocus);
            panel.enabled = true;
            Assert.That(review.Replay());
            OpenInvestigation(review, panel);
            Assert.That(session.Echo.Snapshot.Playing, Is.False);
            Assert.That(panel.Checkpoint.Collected, Is.Empty);
            Assert.That(session.Investigation.Conclusion, Is.EqualTo(EchoConclusion.Unexamined));

            Press(panel, KeyCode.Return);
            Assert.That(panel.Source, Is.EqualTo(Outage));
            Assert.That(panel.Feedback, Is.EqualTo("Read and mark the required archive entry first."));
            Assert.That(panel.Checkpoint.Collected, Is.Empty);
            Press(panel, KeyCode.Return);
            Assert.That(panel.Source.IsValid, Is.False);
            Assert.That(panel.ConfirmQuest(), Is.False);
            Press(panel, KeyCode.Tab, shift: true);
            Press(panel, KeyCode.Return);
            Assert.That(panel.IsOpen || player.HasReadingFocus, Is.False);
            Assert.That(player.HasWorldFocus, Is.True);
            OpenArchiveAndAcknowledge(bridge, reader);
            Assert.That(reader.OpenReplay());
            OpenInvestigation(review, panel);

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
            Press(panel, KeyCode.Tab, shift: true);
            Press(panel, KeyCode.Return);
            Assert.That(panel.IsOpen || player.HasReadingFocus, Is.False);
            Assert.That(player.HasWorldFocus, Is.True);
        }

        [UnityTest]
        public IEnumerator PausedOrReplacedViewsCannotSubmitAndTheConfirmedOrderRestores()
        {
            yield return LoadStation();
            var player = Object.FindAnyObjectByType<StationFirstPerson>();
            var bridge = player.GetComponent<StationInteraction>();
            var panel = player.GetComponent<StationEchoInvestigation>();
            var reader = player.GetComponent<StationArchiveReader>();
            var review = player.GetComponent<StationEchoReview>();
            yield return CompleteArrivalAndPower(bridge);
            var session = bridge.Session;
            OpenArchiveAndAcknowledge(bridge, reader);
            Assert.That(reader.OpenReplay());
            OpenInvestigation(review, panel);
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
            Assert.That(panel.ConfirmQuest());
            var folder = Path.Combine(Application.temporaryCachePath, "lks-w11-" + Guid.NewGuid().ToString("N"));
            var slot = new StableId("echo-investigation");
            var saves = new StationSaves(folder, (StationScienceCatalog)session.Science,
                new[] { new StableId("donki.2017-09-04t23-52-00-sep-001") });
            try
            {
                Assert.That(saves.Save(slot, session).Succeeded);
                var load = saves.Load(slot, player.GetComponent<SessionBootstrap>(),
                    () => Array.Empty<IInteractable>(), _ => ResultCode.EvidenceInsufficient);
                Assert.That(load.Succeeded);
                yield return null;
                Assert.That(panel.IsOpen || player.HasReadingFocus, Is.False);
                Assert.That(panel.SubmitOrder(), Is.False);
                Assert.That(bridge.Session.Archive.Capture(), Is.EqualTo(new[] { RequiredArchive }));
                StationEchoReviewTests.OpenReview(bridge, reader, review);
                OpenInvestigation(review, panel);
                Assert.That(panel.Draft, Is.EqualTo(ordered));
                Assert.That(panel.Checkpoint.Collected, Is.EquivalentTo(EchoInvestigation.RequiredClues));
                Assert.That(bridge.Session.Investigation.Conclusion, Is.EqualTo(EchoConclusion.Confirmed));
                Assert.That(bridge.Session.Quests.Snapshot[Quest], Is.EqualTo(QuestStatus.Completed));
                Assert.That(bridge.Session.Quests.Snapshot[StationQuests.RequiredStages[3]], Is.EqualTo(QuestStatus.Available));
                panel.enabled = false;
                Assert.That(panel.IsOpen || player.HasReadingFocus, Is.False);
                Assert.That(panel.ConfirmQuest(), Is.False);
            }
            finally
            {
                if (Directory.Exists(folder))
                {
                    foreach (string file in Directory.GetFiles(folder)) File.Delete(file);
                    Directory.Delete(folder);
                }
            }
        }

        private static IEnumerator LoadStation()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            Object.FindAnyObjectByType<StationFirstPerson>().SendMessage("OnApplicationFocus", true);
        }

        private static IEnumerator CompleteArrivalAndPower(StationInteraction bridge)
        {
            var arrival = StationInteractionTests.Anchor(StationQuests.RequiredStages[0]);
            Assert.That(bridge.Submit(StationInteractionTests.AimAt(arrival)));
            yield return new WaitForSecondsRealtime(.3f);
            var power = StationInteractionTests.Anchor(StationPowerConsole.Target);
            Assert.That(bridge.Submit(StationInteractionTests.AimAt(power)));
            var console = bridge.GetComponent<StationPowerConsole>();
            Assert.That(console.Toggle(StationPower.Air) && console.Toggle(StationPower.Water) &&
                console.Toggle(StationPower.Archive));
            Assert.That(console.Apply() && console.ConfirmLifeSupport());
            console.Close();
            Assert.That(bridge.Session.CaptureQuestCheckpoint().Count, Is.EqualTo(2));
        }

        private static void OpenArchiveAndAcknowledge(StationInteraction bridge, StationArchiveReader reader)
        {
            StationArchiveReaderTests.OpenAtTerminal(bridge, reader);
            Assert.That(reader.OpenDocument(0, Vector2.zero) && reader.MarkAsRead() && reader.ReturnToList());
            Assert.That(bridge.Session.Archive.Capture(), Is.EqualTo(new[] { RequiredArchive }));
        }

        private static void OpenInvestigation(StationEchoReview review, StationEchoInvestigation panel)
        {
            Assert.That(panel, Is.Not.Null, "The saved scene must bind the ECHO investigation.");
            review.HandleKeyboard(new Event { type = EventType.KeyDown, keyCode = KeyCode.Tab,
                modifiers = EventModifiers.Shift });
            review.HandleKeyboard(new Event { type = EventType.KeyDown, keyCode = KeyCode.Return });
            Assert.That(panel.IsOpen && !review.IsOpen && !review.ProjectionVisible);
        }

        private static void Collect(StationEchoInvestigation panel, StableId clue)
        {
            Assert.That(panel.Inspect(clue));
            Assert.That(panel.Checkpoint.Collected, Does.Contain(clue));
            Assert.That(panel.ReturnToComparison());
        }

        private static void Press(StationEchoInvestigation panel, KeyCode key, bool shift = false) =>
            panel.HandleKeys(new Event { type = EventType.KeyDown, keyCode = key,
                modifiers = shift ? EventModifiers.Shift : EventModifiers.None });
    }
}
#endif
