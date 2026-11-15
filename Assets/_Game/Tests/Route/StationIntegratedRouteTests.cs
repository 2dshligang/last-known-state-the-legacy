#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using LKS.World;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace LKS.Tests
{
    public sealed class StationIntegratedRouteTests
    {
        private const string Scene = "Assets/_Game/Scenes/Station.unity";
        private static readonly StableId ArchiveId = new StableId("archive.donki-sep");
        private static readonly StableId RequiredSource = new StableId("donki.2017-09-04t23-52-00-sep-001");
        private static readonly StableId Slot = new StableId("integrated-route");
        private const BindingFlags ViewMethods = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator NewGameToLegitimateEnding() => PlayRoute(true);

        [UnityTest]
        public IEnumerator MissingTransmissionCannotReachEnding() => PlayRoute(false);

        private static IEnumerator PlayRoute(bool transmit)
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(Scene, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var station = Object.FindAnyObjectByType<StationInteraction>();
            Assert.That(station, Is.Not.Null, "Designer-owned Station scene must bind StationInteraction.");
            Assert.That(station.StartError, Is.Null, "Station bootstrap or anchor validation failed.");
            var bootstrap = station.GetComponent<SessionBootstrap>();
            var player = station.GetComponent<StationFirstPerson>();
            Assert.That(bootstrap, Is.Not.Null, "Missing real station bootstrap.");
            Assert.That(player, Is.Not.Null, "Missing first person entry point.");
            var opening = player.GetComponent<StationOpening>();
            var terminal = player.GetComponent<StationResearchTerminal>();
            var finale = player.GetComponent<StationFinale>();
            Assert.That(opening, Is.Not.Null, "The Station must bind W14.");
            Assert.That(terminal, Is.Not.Null, "The Station must bind the W15 report.");
            Assert.That(finale, Is.Not.Null, "The Station must bind the W15 photograph.");
            var run = station.Session;
            Assert.That(run, Is.Not.Null, "New Game did not start.");
            Assert.That(run.CaptureQuestCheckpoint(), Is.Empty);
            Assert.That(run.Narrative.TryGetOpening(out var intro), Is.True);
            Assert.That(intro.TextKey.value, Is.EqualTo("narrative.farewell"));
            player.SendMessage("OnApplicationFocus", true);
            Assert.That(ViewAction(opening, "Continue"), Is.True, "Continue from the farewell.");
            Assert.That(ViewAction(opening, "Continue"), Is.True, "Enter Station from the arrival.");
            Assert.That(run.Narrative.CaptureOpening(), Is.EqualTo(OpeningProgress.Complete));

            yield return ConfirmArrivalThroughScene(station, player);
            Assert.That(run.CaptureQuestCheckpoint(), Is.EqualTo(StationQuests.RequiredStages.Take(1)));

            var clock = new FixedClock();
            string folder = Path.Combine(Application.temporaryCachePath, "lks-s18-" + Guid.NewGuid().ToString("N"));
            var catalog = (StationScienceCatalog)run.Science;
            var saves = new StationSaves(folder, catalog, new[] { RequiredSource });
            var storage = typeof(StationInteraction).GetProperty("CampaignStorage", ViewMethods);
            Assert.That(storage, Is.Not.Null, "Supply an isolated campaign slot for the W15 menu.");
            storage.SetValue(station, saves);
            try
            {
                run = Reload(run, saves, bootstrap, clock);
                Applied(run.Power.Commit(new[] { StationPower.Archive }), "Power misuse leaves essentials without supply");
                Rejected(Request(run, 1), "quest.result.resources", "Unsafe life support must block chapter 2");
                Applied(Request(run, StationQuests.RecoveryTarget, StationQuests.Recover), "Recover essential loads");
                Assert.That(run.Power.EssentialSupportSupplied, Is.True);
                Applied(Request(run, 1), "Confirm safe life support");
                Assert.That(run.LifeSupport.Snapshot.Air, Is.EqualTo(AirStatus.Safe));

                AssertEvidenceSources(run);
                Rejected(run.Investigation.Collect(EchoInvestigation.Timing), "echo.result.read-archive", "Unread NASA entry");
                using (run.Archive.Open(ArchiveId))
                {
                    var power = run.Power.Capture();
                    var air = run.LifeSupport.Capture();
                    Advance(run, clock, 1);
                    Assert.That(run.Power.Capture().BatteryKwh, Is.EqualTo(power.BatteryKwh), "Reading changed battery.");
                    Assert.That(run.LifeSupport.Capture().AirReserve, Is.EqualTo(air.AirReserve), "Reading changed air reserve.");
                    Applied(run.Archive.Acknowledge(ArchiveId), "Read historical training source");
                }

                run.Echo.Restart();
                run.Echo.Seek(8m);
                Assert.That(run.Echo.Snapshot.Frame.Evidence.Quality, Is.EqualTo(EvidenceQuality.Missing));
                Assert.That(run.Echo.Snapshot.Frame.Evidence.RecordedAt, Is.Null);
                run.Echo.Stop();
                Assert.That(run.Echo.Snapshot.Seconds, Is.Zero);
                Assert.That(run.Investigation.Capture().Collected, Is.Empty);
                foreach (var clue in EchoInvestigation.RequiredClues.Reverse())
                    Applied(run.Investigation.Collect(clue), "Collect ECHO clue " + clue);
                Rejected(run.Investigation.SubmitSequence(EchoInvestigation.RequiredClues.Reverse().ToArray()),
                    "echo.result.timing-before-decision", "Reject unsupported sequence");
                Applied(run.Investigation.SubmitSequence(EchoInvestigation.RequiredClues), "Accept supported sequence");
                Applied(Request(run, 2), "Confirm ECHO chapter");

                var specimens = new[] { FictionalSample.All[1].Id, StationSamples.Blank, FictionalSample.All[0].Id };
                Rejected(run.Samples.Collect(specimens[0]), "sample.result.record-context", "No context recorded");
                foreach (var sample in specimens)
                {
                    Applied(run.Samples.RecordContext(sample), "Record " + sample);
                    Applied(run.Samples.Collect(sample), "Collect " + sample);
                }
                Assert.That(run.Samples.Collect(specimens[0]).Code, Is.EqualTo(ResultCode.AlreadyApplied));
                Applied(Request(run, 3), "Confirm sampling");
                Applied(run.Power.RequestLoad(StationPower.Lab, true), "Supply analyzer");
                Rejected(run.Samples.Analyze(specimens[0]), "sample.result.run-blank", "Run the blank first");
                Applied(run.Samples.Analyze(StationSamples.Blank), "Run dirty blank");
                Advance(run, clock, .2);
                run = Reload(run, saves, bootstrap, clock);
                Assert.That(run.Samples.Capture().PendingId, Is.EqualTo(StationSamples.Blank), "Lost in-flight blank.");
                Advance(run, clock, .5);
                Assert.That(run.Samples.BlankControl, Is.EqualTo(ControlStatus.Invalid));
                Rejected(Request(run, 4), "quest.result.evidence", "Failed control blocks analysis confirmation");
                Applied(run.Samples.CleanForRetry(), "Clean chamber without losing samples");
                Assert.That(run.Samples.AllCollected, Is.True);
                foreach (var sample in new[] { StationSamples.Blank, specimens[0], specimens[2] })
                {
                    Applied(run.Samples.Analyze(sample), "Analyze " + sample);
                    Advance(run, clock, .6);
                    Applied(run.Samples.Review(sample), "Review " + sample);
                }
                Assert.That(run.Samples.BlankControl, Is.EqualTo(ControlStatus.Valid));
                Applied(Request(run, 4), "Confirm completed analysis");

                var evidence = run.Research.ReadEvidence();
                Assert.That(evidence.Count, Is.EqualTo(3));
                Assert.That(evidence.All(item => item.StationKind == EvidenceKind.Fiction), Is.True);
                Assert.That(evidence.All(item => item.ReferenceSource.Host == "ntrs.nasa.gov"), Is.True);
                // Let the scene observe the reloaded session and clear the earlier arrival input cooldown.
                yield return null;
                Assert.That(SceneSubmit(station, StationResearchTerminal.Target), Is.True, "Open the installed report terminal.");
                foreach (var observation in new[] { ReportObservation.LayersVisible, ReportObservation.IronMineralSigns,
                    ReportObservation.BlankPassed })
                    Assert.That(ViewAction(terminal, "ToggleObservation", observation), Is.True, "Choose " + observation);
                for (int section = 1; section < 4; section++)
                    Assert.That(ViewAction(terminal, "CycleChoice", section, 1), Is.True);
                Assert.That(ViewAction(terminal, "CycleChoice", 0, -1), Is.True, "Choose the unsupported interpretation.");
                Assert.That(ViewAction(terminal, "SubmitReport"), Is.False, "W15 must reject a claim of confirmed life.");
                Assert.That(ViewProperty<string>(terminal, "Feedback"), Does.Contain("cannot confirm life"));
                Assert.That(run.Research.CaptureAccepted(), Is.Null);
                Assert.That(ViewAction(terminal, "CycleChoice", 0, 1), Is.True);
                Assert.That(ViewAction(terminal, "CycleChoice", 0, 1), Is.True);
                Assert.That(ViewAction(terminal, "SubmitReport"), Is.True, "Submit the revised report through W15.");
                Assert.That(run.Quests.Snapshot[StationResearchTerminal.Target], Is.EqualTo(QuestStatus.Completed));
                Assert.That(run.Pacing.ReportRetries, Is.EqualTo(1));
                CallView(terminal, "HandleKeys", new Event { type = EventType.KeyDown, keyCode = KeyCode.Return });
                Assert.That(player.HasWorldFocus, Is.True, "Return to Station from the report.");
                Assert.That(SceneSubmit(station, StationEnding.Photograph), Is.False, "The overlook still needs transmission.");
                Assert.That(station.Feedback, Does.Contain("Queue the accepted report"));
                Assert.That(run.Ending.TryGetFinalCue(out _), Is.False);
                Assert.That(run.Ending.Completion.Snapshot, Is.False);
                if (!transmit)
                {
                    run = Reload(run, saves, bootstrap, clock);
                    yield return null;
                    Assert.That(SceneSubmit(station, StationEnding.Photograph), Is.False, "Reload cannot supply missing transmission.");
                    Assert.That(station.Feedback, Does.Contain("Queue the accepted report"));
                    Assert.That(run.CaptureQuestCheckpoint(), Is.EqualTo(StationQuests.RequiredStages.Take(6)));
                    yield break;
                }

                Assert.That(SceneSubmit(station, StationResearchTerminal.Target), Is.True);
                Assert.That(ViewAction(terminal, "QueueReport"), Is.True, "Queue transmission through W15.");
                CallView(terminal, "HandleKeys", new Event { type = EventType.KeyDown, keyCode = KeyCode.Return });
                run = Reload(run, saves, bootstrap, clock);
                yield return null;
                Assert.That(run.Logistics.Delivery, Is.EqualTo(DeliveryState.Queued));
                int completionEvents = 0;
                using var subscription = run.Ending.Completion.Subscribe(_ => completionEvents++);
                Assert.That(SceneSubmit(station, StationEnding.Photograph), Is.True, "Inspect the bound W15 photograph.");
                run.Dispatch();
                Assert.That(completionEvents, Is.EqualTo(1));
                Assert.That(run.CaptureQuestCheckpoint(), Is.EqualTo(StationQuests.RequiredStages));
                Assert.That(run.Ending.TryGetFinalCue(out var cue), Is.True);
                Assert.That(run.Archive.Content.Resolve(cue.TextKey, false), Is.EqualTo("Dad... I did it."));
                Assert.That(ViewProperty<string>(finale, "FinalText"), Is.EqualTo("Dad... I did it."));
                Assert.That(Request(run, 6).Code, Is.EqualTo(ResultCode.AlreadyApplied));
                Assert.That(ViewAction(finale, "SkipToMenu"), Is.True);
                Assert.That(ViewAction(finale, "Continue"), Is.True, "Continue through the installed menu.");
                Assert.That(station.Session, Is.Not.SameAs(run));
                run = station.Session;
                run.Dispatch();
                Assert.That(run.Ending.Completion.Snapshot, Is.True);
                Assert.That(ViewProperty<string>(finale, "FinalText"), Is.EqualTo("Dad... I did it."));
                Assert.That(completionEvents, Is.EqualTo(1), "Loading does not replay completion.");
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

        private static IEnumerator ConfirmArrivalThroughScene(StationInteraction station, StationFirstPerson player)
        {
            var anchor = Object.FindObjectsByType<StationInteractionAnchor>().Single(item =>
                item.TargetId == StationQuests.RequiredStages[0]);
            var camera = player.GetComponentInChildren<Camera>();
            var body = player.GetComponent<CharacterController>();
            var offset = anchor.transform.position - anchor.Approach.position - Vector3.up * 1.62f;
            body.enabled = false;
            player.transform.SetPositionAndRotation(anchor.Approach.position,
                Quaternion.LookRotation(Vector3.ProjectOnPlane(offset, Vector3.up)));
            body.enabled = true;
            Physics.SyncTransforms();
            player.SendMessage("OnApplicationFocus", true);
            player.ResumePointer();
            yield return null;
            yield return null;
            var aim = Quaternion.LookRotation(anchor.transform.position - camera.transform.position);
            var ray = new Ray(camera.transform.position, aim * Vector3.forward);
            Assert.That(station.FindAimedTarget(ray),
                Is.SameAs(anchor), "Arrival control is not visible from the authored approach.");
            Assert.That(station.Describe(anchor), Is.EqualTo("E  Confirm arrival\nArrival check"));
            if (Application.isBatchMode)
            {
                // Unity batch mode cannot lock the OS cursor. Use the same scene ray submit handler
                // reached by the installed action once the player is allowed to interact.
                Assert.That(ViewAction(station, "Submit", ray), Is.True, station.Feedback);
                yield break;
            }

            var keyboard = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            float pitch = -Mathf.Asin((aim * Vector3.forward).y) * Mathf.Rad2Deg;
            float mouseDelta = pitch / player.Sensitivity * (player.InvertY ? 1 : -1);
            try
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(0, mouseDelta) });
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Assert.That(station.Session.Quests.Snapshot[StationQuests.RequiredStages[0]], Is.EqualTo(QuestStatus.Completed),
                    "The installed E action did not confirm arrival. World focus: " + player.HasWorldFocus + ", cursor: " + Cursor.lockState);
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
                InputSystem.RemoveDevice(keyboard);
            }
        }

        private static void AssertEvidenceSources(GameplaySession run)
        {
            Assert.That(run.Science.TryGet(RequiredSource, out var record), Is.True);
            Assert.That(record.Kind, Is.EqualTo(EvidenceKind.Observation));
            Assert.That(EnglishContentCatalog.Archive.Single(e => e.Id == ArchiveId).IsFictional, Is.False);
            Assert.That(StationEcho.Timeline.Any(frame => frame.Evidence.Kind == EvidenceKind.EngineeringModel), Is.True);
            Assert.That(StationEcho.Timeline.Any(frame => frame.Evidence.Kind == EvidenceKind.Fiction), Is.True);
        }

        private static bool SceneSubmit(StationInteraction station, StableId id)
        {
            var anchor = Object.FindObjectsByType<StationInteractionAnchor>().Single(item => item.TargetId == id);
            var eye = anchor.Approach.position + Vector3.up * 1.62f;
            var ray = new Ray(eye, (anchor.transform.position - eye).normalized);
            Assert.That(station.FindAimedTarget(ray), Is.SameAs(anchor), "Scene ray must reach " + id);
            return ViewAction(station, "Submit", ray);
        }

        // Use the installed button and input handlers without giving test code access to World internals in production.
        private static bool ViewAction(MonoBehaviour view, string method, params object[] args) =>
            (bool)CallView(view, method, args);

        private static object CallView(MonoBehaviour view, string method, params object[] args)
        {
            var action = view.GetType().GetMethod(method, ViewMethods);
            Assert.That(action, Is.Not.Null, view.GetType().Name + "." + method);
            return action.Invoke(view, args);
        }

        private static T ViewProperty<T>(MonoBehaviour view, string name)
        {
            var property = view.GetType().GetProperty(name, ViewMethods);
            Assert.That(property, Is.Not.Null, view.GetType().Name + "." + name);
            return (T)property.GetValue(view);
        }

        private static GameplaySession Reload(GameplaySession run, StationSaves saves, SessionBootstrap bootstrap, FixedClock clock)
        {
            var quests = run.CaptureQuestCheckpoint().ToArray();
            Applied(saves.Save(Slot, run), "Write real station slot");
            Applied(saves.Load(Slot, bootstrap, () => Array.Empty<IInteractable>(), _ => ResultCode.EvidenceInsufficient, clock),
                "Restore station checkpoint");
            Assert.That(bootstrap.Current, Is.Not.SameAs(run));
            Assert.That(bootstrap.Current.CaptureQuestCheckpoint(), Is.EqualTo(quests));
            return bootstrap.Current;
        }

        private static CommandResult Request(GameplaySession run, int stage) =>
            Request(run, StationQuests.RequiredStages[stage], StationQuests.Confirm);

        private static CommandResult Request(GameplaySession run, StableId id, StableId action)
        {
            Assert.That(run.TryGetTarget(id, out var target), Is.True, "Missing target " + id);
            return target.Request(StationQuests.Player, action);
        }

        private static void Advance(GameplaySession run, FixedClock clock, double seconds)
        {
            clock.Seconds += seconds;
            run.Tick();
            run.Dispatch();
        }

        private static void Applied(CommandResult result, string at) =>
            Assert.That(result.Code, Is.EqualTo(ResultCode.Applied), at + ": " + result.MessageKey);

        private static void Rejected(CommandResult result, string why, string at)
        {
            Assert.That(result.Succeeded, Is.False, at);
            Assert.That(result.MessageKey.value, Is.EqualTo(why), at);
        }

        private sealed class FixedClock : ISessionClock { public double Seconds { get; set; } }
    }
}
#endif
