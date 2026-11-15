#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
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
    public sealed class StationResearchTerminalTests
    {
        [UnityTest]
        public IEnumerator RevisesRejectedReportAndRestoresOneQueuedTransmission()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_Game/Scenes/Station.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var bridge = Object.FindAnyObjectByType<StationInteraction>();
            var player = bridge.GetComponent<StationFirstPerson>();
            player.SendMessage("OnApplicationFocus", true);
            RestoreReviewed(bridge);
            var report = bridge.GetComponent<StationResearchTerminal>();
            Ray aim = AimAt(StationResearchTerminal.Target);
            Assert.That(bridge.Submit(aim));
            Assert.That(report.IsOpen && player.HasReadingFocus);
            Assert.That(report.QueueReport(), Is.False);
            Assert.That(bridge.Session.Logistics.Delivery, Is.EqualTo(DeliveryState.NotQueued));

            Assert.That(report.ToggleObservation(ReportObservation.LayersVisible) &&
                report.ToggleObservation(ReportObservation.IronMineralSigns) &&
                report.ToggleObservation(ReportObservation.BlankPassed));
            Assert.That(report.CycleChoice(0, -1) && report.CycleChoice(1, 1) &&
                report.CycleChoice(2, 1) && report.CycleChoice(3, 1));
            Assert.That(report.SubmitReport(), Is.False);
            Assert.That(report.Feedback, Does.Contain("cannot confirm life"));
            Assert.That(bridge.Session.Quests.Snapshot[StationResearchTerminal.Target], Is.EqualTo(QuestStatus.Available));
            Assert.That(report.OpenReference(report.FeedbackReference));
            Assert.That(report.SubmitReport(), Is.False);
            report.HandleKeys(new Event { type = EventType.KeyDown, keyCode = KeyCode.Return });
            Assert.That(report.Draft.Observations.Count, Is.EqualTo(3));
            Assert.That(report.Draft.Interpretation, Is.EqualTo(ReportInterpretation.LifeConfirmed));
            Assert.That(report.CycleChoice(0, 1) && report.CycleChoice(0, 1) && report.SubmitReport());
            Assert.That(bridge.Session.Research.Snapshot, Is.EqualTo(ReportStatus.Accepted));
            Assert.That(bridge.Session.Quests.Snapshot[StationResearchTerminal.Target], Is.EqualTo(QuestStatus.Completed));
            Assert.That(report.SubmitReport(), Is.False);
            Assert.That(report.QueueReport());
            var queued = bridge.Session.Logistics.Capture();
            Assert.That(queued.Delivery, Is.EqualTo(DeliveryState.Queued));
            Assert.That(report.Transmission, Does.Contain("Waiting for transmitter power"));
            Assert.That(report.QueueReport());
            Assert.That(bridge.Session.Logistics.Capture().RemainingSeconds, Is.EqualTo(queued.RemainingSeconds));
            Assert.That(bridge.Session.Ending.Completion.Snapshot, Is.False);

            string path = SetTemporaryCampaign(bridge);
            try
            {
                Assert.That(report.SaveProgress());
                var previous = bridge.Session;
                Assert.That(bridge.LoadCampaign().Succeeded);
                Assert.That(previous, Is.Not.SameAs(bridge.Session));
                Assert.That(report.IsOpen || player.HasReadingFocus, Is.False);
                Assert.That(bridge.Session.Logistics.Delivery, Is.EqualTo(DeliveryState.Queued));
                Assert.That(bridge.Session.Logistics.Capture().RemainingSeconds, Is.EqualTo(queued.RemainingSeconds));
                Assert.That(bridge.Submit(aim));
                Assert.That(report.Accepted);
                Assert.That(report.Draft.Interpretation, Is.EqualTo(ReportInterpretation.SedimentMayHaveSettled));
                Assert.That(report.CycleChoice(0, 1), Is.False);
                report.HandleKeys(new Event { type = EventType.KeyDown, keyCode = KeyCode.Return });
                Assert.That(report.IsOpen, Is.False);
                Assert.That(player.HasWorldFocus, Is.True);
            }
            finally
            {
                RemoveTemporaryCampaign(path);
            }
        }

        [UnityTest]
        public IEnumerator PhotographRequiresQueuedReportAndContinueRestoresTheCommittedEnding()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_Game/Scenes/Station.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var bridge = Object.FindAnyObjectByType<StationInteraction>();
            bridge.GetComponent<StationFirstPerson>().SendMessage("OnApplicationFocus", true);
            var valid = new ResearchDraft(new[] { ReportObservation.LayersVisible, ReportObservation.IronMineralSigns,
                    ReportObservation.BlankPassed }, ReportInterpretation.SedimentMayHaveSettled,
                ReportAlternative.WindDeposition, ReportUncertainty.MoreEvidenceNeededForLife,
                ReportFollowUp.CompareNearbyLayersWithBlank);
            RestoreReviewed(bridge, valid);
            var finale = bridge.GetComponent<StationFinale>();
            var photo = WalkToPhotograph(bridge);
            Assert.That(bridge.Submit(photo), Is.False);
            Assert.That(bridge.Feedback, Does.Contain("Queue the accepted report"));
            Assert.That(finale.IsOpen || bridge.Session.Ending.Completion.Snapshot, Is.False);
            Assert.That(finale.Replay(), Is.False);

            var report = bridge.GetComponent<StationResearchTerminal>();
            Assert.That(bridge.Submit(AimAt(StationResearchTerminal.Target)));
            Assert.That(report.QueueReport());
            report.HandleKeys(new Event { type = EventType.KeyDown, keyCode = KeyCode.Return });
            string path = SetTemporaryCampaign(bridge);
            try
            {
                var session = bridge.Session;
                int completions = 0;
                using var subscription = session.Ending.Completion.Subscribe(_ => completions++);
                Assert.That(bridge.Submit(photo));
                Assert.That(finale.IsOpen);
                Assert.That(finale.FinalText, Is.EqualTo("Dad... I did it."));
                Assert.That(finale.InCredits, Is.False);
                Assert.That(session.Ending.Completion.Snapshot);
                Assert.That(session.Logistics.Delivery, Is.EqualTo(DeliveryState.Queued));
                yield return null;
                Assert.That(completions, Is.EqualTo(1));
                Assert.That(finale.Credits() && finale.InCredits);
                Assert.That(finale.SkipToMenu());
                Assert.That(finale.Replay());
                Assert.That(finale.FinalText, Is.EqualTo("Dad... I did it."));
                Assert.That(finale.SkipToMenu() && finale.Continue());
                Assert.That(bridge.Session, Is.Not.SameAs(session));
                Assert.That(bridge.Session.Ending.Completion.Snapshot);
                Assert.That(finale.FinalText, Is.EqualTo("Dad... I did it."));
                Assert.That(bridge.Session.CaptureQuestCheckpoint(), Is.EqualTo(StationQuests.RequiredStages));
                Assert.That(completions, Is.EqualTo(1));
            }
            finally
            {
                RemoveTemporaryCampaign(path);
            }
        }

        private static void RestoreReviewed(StationInteraction bridge, ResearchDraft accepted = null)
        {
            var clues = EchoInvestigation.RequiredClues;
            var samples = new SampleCheckpoint(FictionalSample.All.ToDictionary(item => item.Id, _ => SamplePhase.Reviewed),
                ControlStatus.Valid, default, 0, true);
            Assert.That(bridge.Restore(new PowerCheckpoint(1m, 0, new[] { StationPower.Air, StationPower.Water }),
                StationQuests.RequiredStages.Take(accepted == null ? 5 : 6), null, null,
                new[] { new StableId("archive.donki-sep") },
                new EchoInvestigationCheckpoint(clues, clues), samples, accepted));
            Physics.SyncTransforms();
        }

        private static Ray AimAt(StableId target)
        {
            var anchor = Object.FindObjectsByType<StationInteractionAnchor>().Single(item => item.TargetId == target);
            Vector3 eye = anchor.Approach.position + Vector3.up * 1.62f;
            return new Ray(eye, (anchor.transform.position - eye).normalized);
        }

        private static Ray WalkToPhotograph(StationInteraction bridge)
        {
            var anchor = Object.FindObjectsByType<StationInteractionAnchor>().Single(item =>
                item.TargetId == StationEnding.Photograph);
            var layout = Object.FindAnyObjectByType<StationGenerationManifest>().Layout;
            Assert.That(anchor.Approach.position.z, Is.GreaterThan(layout.InteriorEnd));
            var motor = bridge.GetComponent<FirstPersonMotor>();
            var approach = anchor.Approach.position;
            WalkTo(new Vector3(0, 0, approach.z));
            WalkTo(approach);
            var camera = bridge.GetComponentInChildren<Camera>().transform;
            camera.LookAt(anchor.transform.position);
            Physics.SyncTransforms();
            var ray = new Ray(camera.position, camera.forward);
            Assert.That(bridge.FindAimedTarget(ray), Is.SameAs(anchor));
            return ray;

            void WalkTo(Vector3 point)
            {
                for (int index = 0; index < 160; index++)
                {
                    Vector3 direction = point - motor.transform.position;
                    direction.y = 0;
                    if (direction.sqrMagnitude < .001f) return;
                    Vector3 local = motor.transform.InverseTransformDirection(direction);
                    motor.Move(Vector2.ClampMagnitude(new Vector2(local.x, local.z) / .27f, 1), .08f);
                }
                Assert.Fail($"Unable to walk to the overlook at {point:F2} from {motor.transform.position:F2}.");
            }
        }

        private static string SetTemporaryCampaign(StationInteraction bridge)
        {
            string path = Path.Combine(Application.temporaryCachePath, "lks-w15-" + Guid.NewGuid().ToString("N"));
            bridge.CampaignStorage = new StationSaves(path, (StationScienceCatalog)bridge.Session.Science,
                new[] { new StableId("donki.2017-09-04t23-52-00-sep-001") });
            return path;
        }

        private static void RemoveTemporaryCampaign(string path)
        {
            if (!Directory.Exists(path)) return;
            foreach (string file in Directory.GetFiles(path)) File.Delete(file);
            Directory.Delete(path);
        }
    }
}
#endif
