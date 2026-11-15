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
                var opening = bridge.GetComponent<StationOpening>();
                Assert.That(opening.Photograph, Is.Not.Null);
                Assert.That(finale.DisplayedPhotograph, Is.SameAs(opening.Photograph));
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
                Assert.That(finale.DisplayedPhotograph, Is.SameAs(opening.Photograph));
                Assert.That(bridge.Session.CaptureQuestCheckpoint(), Is.EqualTo(StationQuests.RequiredStages));
                Assert.That(completions, Is.EqualTo(1));
            }
            finally
            {
                RemoveTemporaryCampaign(path);
            }
        }

        [UnityTest]
        public IEnumerator NewCampaignRestoresArrivalAndReachesTheEndingThroughTheSavedStation()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(StationSceneEntry.Path,
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var bridge = StationSceneEntry.Resolve(SceneManager.GetSceneByPath(StationSceneEntry.Path));
            Assert.That(bridge.StartError, Is.Null);
            var player = bridge.GetComponent<StationFirstPerson>();
            player.SendMessage("OnApplicationFocus", true);
            var opening = player.GetComponent<StationOpening>();
            var finale = player.GetComponent<StationFinale>();
            string path = SetTemporaryCampaign(bridge);
            try
            {
                Assert.That(opening.IsOpen && opening.Continue());
                Assert.That(bridge.Session.Narrative.CaptureOpening(), Is.EqualTo(OpeningProgress.Arrival));
                Assert.That(opening.OpenGameMenu() && finale.SaveProgress());
                Assert.That(finale.IsOpen && !opening.IsOpen);
                bridge.NewRun();
                Assert.That(opening.Passage, Does.Contain("January 20, 2059"));
                Assert.That(opening.OpenGameMenu() && finale.Continue());
                Assert.That(opening.IsOpen && !finale.IsOpen);
                Assert.That(opening.Passage, Does.Contain("November 14, 2089"));
                Assert.That(bridge.Session.CaptureQuestCheckpoint(), Is.Empty);
                Assert.That(opening.Continue() && player.HasWorldFocus);

                Assert.That(bridge.Submit(WalkToInterior(bridge, StationQuests.RequiredStages[0])));
                yield return new WaitForSecondsRealtime(.31f);
                Assert.That(bridge.Submit(WalkToInterior(bridge, StationPowerConsole.Target)));
                var power = player.GetComponent<StationPowerConsole>();
                Assert.That(power.Toggle(StationPower.Air) && power.Toggle(StationPower.Water) &&
                    power.Toggle(StationPower.Archive) && power.Apply() && power.ConfirmLifeSupport());
                power.Close();
                ReloadChapter(bridge, finale, 2, "life-support");
                var reader = player.GetComponent<StationArchiveReader>();
                var review = player.GetComponent<StationEchoReview>();
                var investigation = player.GetComponent<StationEchoInvestigation>();
                Assert.That(bridge.Submit(WalkToInterior(bridge, StationArchiveReader.Target)));
                Assert.That(reader.OpenDocument(0, Vector2.zero) && reader.MarkAsRead() && reader.ReturnToList());
                Assert.That(reader.OpenReplay());
                StationEchoInvestigationTests.OpenInvestigation(review, investigation);
                foreach (var clue in EchoInvestigation.RequiredClues)
                    Assert.That(investigation.Inspect(clue) && investigation.ReturnToComparison() && investigation.ToggleOrder(clue));
                Assert.That(investigation.SubmitOrder() && investigation.ConfirmQuest());
                investigation.Close();
                ReloadChapter(bridge, finale, 3, "echo");
                Assert.That(bridge.Session.Archive.RequiredEntryAcknowledged);
                Assert.That(bridge.Session.Investigation.Conclusion, Is.EqualTo(EchoConclusion.Confirmed));

                yield return CollectSamples(bridge);
                ReloadChapter(bridge, finale, 4, "sampling");
                Assert.That(bridge.Session.Samples.AllCollected);
                yield return AnalyzeSamples(bridge, finale, power);
                ReloadChapter(bridge, finale, 5, "analysis");
                Assert.That(bridge.Session.Samples.AllReviewed);

                var report = player.GetComponent<StationResearchTerminal>();
                Assert.That(bridge.Submit(WalkToInterior(bridge, StationResearchTerminal.Target)));
                Assert.That(report.ToggleObservation(ReportObservation.LayersVisible) &&
                    report.ToggleObservation(ReportObservation.IronMineralSigns) &&
                    report.ToggleObservation(ReportObservation.BlankPassed));
                for (int section = 0; section < 4; section++) Assert.That(report.CycleChoice(section, 1));
                Assert.That(report.SubmitReport() && report.QueueReport());
                report.HandleKeys(new Event { type = EventType.KeyDown, keyCode = KeyCode.Return });
                Assert.That(player.HasWorldFocus);
                ReloadChapter(bridge, finale, 6, "report-queued");
                Assert.That(bridge.Session.Logistics.Delivery, Is.EqualTo(DeliveryState.Queued));
                Assert.That(bridge.Submit(WalkToInterior(bridge, StationPowerConsole.Target)));
                Assert.That(power.Toggle(StationPower.Lab) && power.Toggle(StationPower.Transmit) && power.Apply());
                power.Close();
                var photoRay = WalkToPhotograph(bridge);
                Assert.That(bridge.Session.Power.Snapshot.Loads.Single(load => load.Id == StationPower.Transmit).Supplied);
                Assert.That(bridge.Submit(photoRay));
                Assert.That(finale.FinalText, Is.EqualTo("Dad... I did it."));
                Assert.That(finale.DisplayedPhotograph, Is.SameAs(opening.Photograph));
                Assert.That(bridge.Session.CaptureQuestCheckpoint(), Is.EqualTo(StationQuests.RequiredStages));
                Assert.That(finale.SkipToMenu() && finale.Continue());
                Assert.That(bridge.Session.Ending.Completion.Snapshot);
                Assert.That(finale.FinalText, Is.EqualTo("Dad... I did it."));
                Debug.Log("W19_ROUTE_PASS: opened the final photograph and loaded the completed campaign.");
            }
            finally
            {
                RemoveTemporaryCampaign(path);
            }
        }

        private static IEnumerator CollectSamples(StationInteraction bridge)
        {
            yield return PickUp(bridge, WalkToInterior(bridge, StationSamples.Blank));
            var motor = bridge.GetComponent<FirstPersonMotor>();
            var layout = Object.FindAnyObjectByType<StationGenerationManifest>().Layout;
            float near = layout.InteriorEnd + 2;
            float far = layout.InteriorEnd + layout.ExteriorDepth - 1.5f;
            var camera = bridge.GetComponentInChildren<Camera>().transform;
            Physics.SyncTransforms();
            Walk(new Vector3(0, 0, motor.transform.position.z));
            Walk(new Vector3(0, 0, near));
            foreach (var sample in FictionalSample.All.Where(item => item.Id != StationSamples.Blank))
            {
                var rock = StationInteractionTests.Anchor(sample.Id);
                Walk(new Vector3(rock.Approach.position.x, 0, near));
                Walk(rock.Approach.position);
                yield return PickUp(bridge, Face(camera, rock));
                Walk(new Vector3(rock.Approach.position.x, 0, near));
                Walk(new Vector3(0, 0, near));
            }

            var dust = StationInteractionTests.Anchor(StationQuests.RequiredStages[3]);
            float side = layout.ExteriorWidth / 2 - 1.3f;
            Walk(new Vector3(side, 0, near));
            Walk(new Vector3(side, 0, far));
            Walk(dust.Approach.position);
            Assert.That(bridge.Submit(Face(camera, dust)));
            Walk(new Vector3(side, 0, far));
            Walk(new Vector3(side, 0, near));
            Walk(new Vector3(0, 0, near));
            Walk(new Vector3(0, 0, layout.InteriorEnd - 2));
            yield return new WaitForSecondsRealtime(.31f);

            void Walk(Vector3 point) => StationArchiveReaderTests.Walk(motor, point);
        }

        private static IEnumerator PickUp(StationInteraction bridge, Ray aim)
        {
            Assert.That(bridge.Submit(aim));
            yield return new WaitForSecondsRealtime(.31f);
            Assert.That(bridge.Submit(aim));
            yield return new WaitForSecondsRealtime(.31f);
        }

        private static IEnumerator AnalyzeSamples(StationInteraction bridge, StationFinale finale, StationPowerConsole power)
        {
            Assert.That(bridge.Submit(WalkToInterior(bridge, StationPowerConsole.Target)));
            Assert.That(power.Toggle(StationPower.Archive) && power.Toggle(StationPower.Lab) && power.Apply());
            power.Close();
            Assert.That(bridge.Submit(WalkToInterior(bridge, StationLaboratory.Target)));
            var lab = bridge.GetComponent<StationLaboratory>();
            Assert.That(lab.StartAnalysis(StationSamples.Blank));
            lab.Close();
            ReloadChapter(bridge, finale, 4, "lab-in-flight");
            var session = bridge.Session;
            Assert.That(session.Samples.Capture().PendingId, Is.EqualTo(StationSamples.Blank));
            Assert.That(bridge.Submit(WalkToInterior(bridge, StationLaboratory.Target)));
            yield return new WaitForSecondsRealtime(.75f);
            Assert.That(session.Samples.BlankControl, Is.EqualTo(ControlStatus.Invalid));
            Assert.That(lab.Clean());
            foreach (var id in new[] { StationSamples.Blank }.Concat(
                FictionalSample.All.Select(item => item.Id).Where(id => id != StationSamples.Blank)))
            {
                Assert.That(lab.StartAnalysis(id));
                yield return new WaitForSecondsRealtime(.75f);
                Assert.That(lab.Interpret(id, true));
            }
            Assert.That(lab.Confirm());
            lab.Close();
        }

        private static void ReloadChapter(StationInteraction bridge, StationFinale finale, int stage, string chapter)
        {
            var before = bridge.Session;
            var expected = StationQuests.RequiredStages.Take(stage).ToArray();
            Assert.That(before.CaptureQuestCheckpoint(), Is.EqualTo(expected), chapter);
            finale.OpenMenu();
            Assert.That(finale.SaveProgress() && finale.Continue(), chapter);
            Assert.That(bridge.Session, Is.Not.SameAs(before), chapter);
            Assert.That(bridge.Session.CaptureQuestCheckpoint(), Is.EqualTo(expected), chapter);
            Assert.That(bridge.GetComponent<StationFirstPerson>().HasWorldFocus, Is.True, chapter);
            Debug.Log($"W19_CHECKPOINT_PASS {chapter}: {stage} milestones");
        }

        private static Ray WalkToInterior(StationInteraction bridge, StableId target)
        {
            var anchor = StationInteractionTests.Anchor(target);
            var motor = bridge.GetComponent<FirstPersonMotor>();
            var approach = anchor.Approach.position;
            Physics.SyncTransforms();
            StationArchiveReaderTests.Walk(motor, new Vector3(0, 0, motor.transform.position.z));
            StationArchiveReaderTests.Walk(motor, new Vector3(0, 0, approach.z));
            StationArchiveReaderTests.Walk(motor, approach);
            var ray = Face(bridge.GetComponentInChildren<Camera>().transform, anchor);
            Assert.That(bridge.FindAimedTarget(ray), Is.SameAs(anchor), $"Approach and sight line for {target}");
            return ray;
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
            StationArchiveReaderTests.Walk(motor, new Vector3(0, 0, motor.transform.position.z));
            StationArchiveReaderTests.Walk(motor, new Vector3(0, 0, approach.z));
            StationArchiveReaderTests.Walk(motor, approach);
            var ray = Face(bridge.GetComponentInChildren<Camera>().transform, anchor);
            Assert.That(bridge.FindAimedTarget(ray), Is.SameAs(anchor));
            return ray;
        }

        private static Ray Face(Transform camera, StationInteractionAnchor anchor)
        {
            camera.LookAt(anchor.transform.position);
            Physics.SyncTransforms();
            return new Ray(camera.position, camera.forward);
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
