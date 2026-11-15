using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace LKS.World
{
    public sealed class W17EvidenceFixture : MonoBehaviour
    {
        private readonly StableId slot = new("w17-evidence");
        private readonly StableId requiredSource = new("donki.2017-09-04t23-52-00-sep-001");
        private Transform eye;
        private Vector3 look;
        private string output;
        private StationFirstPerson player;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Environment.GetCommandLineArgs().Contains("-lksW17Evidence"))
                new GameObject("W17 accessibility capture").AddComponent<W17EvidenceFixture>();
        }

        private void LateUpdate()
        {
            if (eye != null) eye.LookAt(look);
        }

        private IEnumerator Start()
        {
            yield return null;
            var args = Environment.GetCommandLineArgs();
            output = args[Array.IndexOf(args, "-lksW17Evidence") + 1];
            Directory.CreateDirectory(output);
            AudioListener.volume = 0;
            bool previousSetting = StationUi.LargeText;
            StationUi.LargeText = false;
            var station = FindAnyObjectByType<StationInteraction>();
            player = station.GetComponent<StationFirstPerson>();
            player.SendMessage("OnApplicationFocus", true);
            var opening = player.GetComponent<StationOpening>();
            var finale = player.GetComponent<StationFinale>();
            var terminal = player.GetComponent<StationResearchTerminal>();
            var archive = player.GetComponent<StationArchiveReader>();
            var review = player.GetComponent<StationEchoReview>();
            var investigation = player.GetComponent<StationEchoInvestigation>();
            var lab = player.GetComponent<StationLaboratory>();
            var shelter = FindAnyObjectByType<StationShelterSet>();
            var power = player.GetComponent<StationPowerConsole>();
            Require(station.StartError == null && Debug.isDebugBuild, "Development station must bootstrap");
            var run = station.Session;
            var saves = new StationSaves(Path.Combine(output, "campaign-" + Guid.NewGuid().ToString("N")),
                (StationScienceCatalog)run.Science, new[] { requiredSource });
            station.CampaignStorage = saves;
            var clock = new FixedClock();
            try
            {
                yield return Shot("01-farewell");
                Require(opening.Continue(), "Advance to arrival");
                yield return Shot("02-arrival");
                Require(opening.Continue(), "Enter station");

                Aim(StationLaboratory.Target);
                Require(!station.Submit(Ray()), "Early laboratory is blocked");
                yield return Shot("03-blocked-lab");
                player.SetPaused(true);
                yield return Shot("04-pause-standard");
                var overlay = player.GetComponents<MonoBehaviour>().Single(item => item.GetType().Name == "StationInputOverlay");
                var pauseKeys = Keys(overlay, "focus");
                Press(pauseKeys, KeyCode.Tab, 3);
                Require(pauseKeys.IsSelected("text"), "Pause focus reaches text size");
                Press(pauseKeys, KeyCode.Return);
                Require(StationUi.LargeText, "Keyboard turns on large text");
                yield return Shot("05-pause-large");
                Press(pauseKeys, KeyCode.Tab);
                Require(pauseKeys.IsSelected("resume"), "Pause focus reaches Resume");
                Press(pauseKeys, KeyCode.Return);
                Require(!player.IsPaused, "Return to world through keyboard focus");

                Aim(StationQuests.RequiredStages[0]);
                yield return Shot("06-arrival-hud");
                Require(station.Submit(Ray()), "Confirm arrival from the station ray");
                Applied(saves.Save(slot, run), "Write initial slot");
                Applied(saves.Load(slot, player.GetComponent<SessionBootstrap>(), () => Array.Empty<IInteractable>(),
                    _ => ResultCode.EvidenceInsufficient, clock), "Restore the same campaign using a fixed clock");
                run = station.Session;
                yield return null;

                Aim(StationPowerConsole.Target);
                Require(station.Submit(Ray()), "Open power console");
                yield return Shot("07-power");
                power.Close();
                Applied(run.Power.Commit(new[] { StationPower.Air, StationPower.Water }), "Supply safe loads");
                Applied(Request(run, 1), "Confirm safe life support");

                Aim(StationArchiveReader.Target);
                Require(station.Submit(Ray()), "Open installed archive");
                for (int index = 0; index < EnglishContentCatalog.Archive.Count; index++)
                {
                    Require(archive.OpenDocument(index, Vector2.zero) && archive.ToggleDetails(), "Expand archive record");
                    yield return Shot(index == 0 ? "08-observation" : index == 1 ? "09-model" : index == 2 ? "10-fiction" : "10c-arrival-record");
                    archive.HandleKeyboard(Key(KeyCode.PageDown));
                    if (index == 0)
                    {

                        Require(archive.MarkAsRead(), "Acknowledge historical source");
                    }
                    if (index == 2)
                    {
                        var position = (Vector2)typeof(StationArchiveReader).GetField("documentPosition",
                            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(archive);
                        Require(position.y > 0, "Page Down reaches the lower part of the longest archive record");
                        yield return Shot("10b-fiction-scrolled");
                    }
                    Require(archive.ReturnToList(), "Return to the archive list");
                }
                Require(archive.OpenReplay() && review.Scrub(8m), "Inspect missing ECHO interval");
                yield return Shot("11-replay");
                Require(review.OpenInvestigation(), "Open investigation");
                yield return Shot("12-investigation");
                investigation.Close();
                foreach (var clue in EchoInvestigation.RequiredClues)
                    Applied(run.Investigation.Collect(clue), "Collect ECHO evidence");
                Applied(run.Investigation.SubmitSequence(EchoInvestigation.RequiredClues), "Submit supported sequence");
                Applied(Request(run, 2), "Confirm ECHO chapter");

                Aim(StationShelterSet.ShelterTarget);
                Require(station.Submit(Ray()), "Open shelter controls");
                yield return Shot("13-shelter");
                var shelterKeys = Keys(shelter, "keyboard");
                Press(shelterKeys, KeyCode.Tab);
                Require(shelterKeys.IsSelected("close"), "Shelter keyboard focus selects the Close control");
                Press(shelterKeys, KeyCode.Return);
                Require(!player.HasReadingFocus, "Close shelter using Enter");

                Aim(StationShelterSet.SeedlingTarget);
                Require(station.Submit(Ray()), "Open seedling controls");
                yield return Shot("14-seedlings");
                Press(shelterKeys, KeyCode.Tab, 3);
                Require(shelterKeys.IsSelected("lab"), "Seedling keyboard focus skips to the lab choice");
                Press(shelterKeys, KeyCode.Return);
                shelter.Close();

                Aim(FictionalSample.All.First(item => item.Id.value == "sample.layered-mudstone").Id);
                yield return Shot("15-field-sample");
                foreach (var sample in FictionalSample.All)
                {
                    Applied(run.Samples.RecordContext(sample.Id), "Record field context");
                    Applied(run.Samples.Collect(sample.Id), "Collect specimen");
                }
                Applied(Request(run, 3), "Confirm collection");
                var labLoad = run.Power.Snapshot.Loads.Single(load => load.Id == StationPower.Lab);
                if (!labLoad.Supplied) Applied(run.Power.RequestLoad(StationPower.Lab, true), "Power the lab");
                Aim(StationLaboratory.Target);
                Require(station.Submit(Ray()), "Open installed lab");
                yield return Shot("16-lab-blank");
                var labKeys = Keys(lab, "keyboard");
                Press(labKeys, KeyCode.Tab, 3);
                Require(labKeys.IsSelected("sample.layered-mudstone"), "Tab reaches the layered-rock reading");
                Press(labKeys, KeyCode.Return);
                yield return Shot("16b-lab-layered");
                lab.Close();

                Applied(run.Samples.Analyze(StationSamples.Blank), "Run initial blank");
                Advance(run, clock);
                if (run.Samples.BlankControl == ControlStatus.Invalid)
                    Applied(run.Samples.CleanForRetry(), "Clean a failed blank");
                if (run.Samples.BlankControl != ControlStatus.Valid)
                {
                    Applied(run.Samples.Analyze(StationSamples.Blank), "Run a clean blank");
                    Advance(run, clock);
                }
                Applied(run.Samples.Review(StationSamples.Blank), "Review blank control");
                foreach (var sample in FictionalSample.All.Where(item => item.Id != StationSamples.Blank))
                {
                    Applied(run.Samples.Analyze(sample.Id), "Analyze sample");
                    Advance(run, clock);
                    Applied(run.Samples.Review(sample.Id), "Review simulated evidence");
                }
                Applied(Request(run, 4), "Confirm lab chapter");

                Aim(StationResearchTerminal.Target);
                Require(station.Submit(Ray()), "Open report terminal");
                foreach (var observation in new[] { ReportObservation.LayersVisible, ReportObservation.IronMineralSigns,
                    ReportObservation.BlankPassed })
                    Require(terminal.ToggleObservation(observation), "Select a supported station result");
                for (int section = 0; section < 4; section++)
                    Require(terminal.CycleChoice(section, section == 0 ? -1 : 1), "Choose a report response");
                Require(!terminal.SubmitReport(), "Reject unsupported life claim");
                yield return Shot("17-report-feedback");
                Require(terminal.OpenReference(terminal.FeedbackReference), "Open associated science source");
                yield return Shot("18-report-context");
                terminal.HandleKeys(Key(KeyCode.Return));
                Require(terminal.CycleChoice(0, 1) && terminal.CycleChoice(0, 1) &&
                    terminal.SubmitReport() && terminal.QueueReport(), "Revise and queue the report");
                terminal.HandleKeys(Key(KeyCode.Return));
                Aim(StationEnding.Photograph);
                Require(station.Submit(Ray()) && finale.FinalText == "Dad... I did it.", "Present exact finale");
                yield return Shot("19-finale");
                Require(finale.Credits(), "Show the actual credits");
                yield return Shot("20-credits");
                Require(finale.SkipToMenu(), "Open main menu");
                yield return Shot("21-menu");
                Require(finale.Continue() && finale.FinalText == "Dad... I did it.", "Continue sees the saved finale");
                yield return Shot("22-continue");
                Debug.Log("W17_EVIDENCE_PASS Muted installed station route, both sizes, archive scrolling and keyboard focus.");
                Application.Quit();
            }
            finally { StationUi.LargeText = previousSetting; }
        }

        private void Aim(StableId id)
        {
            var anchor = FindObjectsByType<StationInteractionAnchor>().Single(item => item.TargetId == id);
            var body = player.GetComponent<CharacterController>();
            body.enabled = false;
            player.transform.position = anchor.Approach.position;
            body.enabled = true;
            eye = player.GetComponentInChildren<Camera>().transform;
            look = anchor.transform.position;
            eye.LookAt(look);
            Physics.SyncTransforms();
        }

        private Ray Ray() => new(eye.position, eye.forward);

        private IEnumerator Shot(string name)
        {
            for (int frame = 0; frame < 14; frame++)
            {
                player.SendMessage("OnApplicationFocus", true);
                yield return null;
            }
            yield return new WaitForEndOfFrame();
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
            Debug.Log($"W17_SHOT {name} {image.width}x{image.height} large={StationUi.LargeText} muted={AudioListener.volume == 0}");
            Destroy(image);
        }

        private static Event Key(KeyCode key) => new() { type = EventType.KeyDown, keyCode = key };
        private static StationKeyboardFocus Keys(MonoBehaviour view, string field) =>
            (StationKeyboardFocus)view.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);

        private static void Press(StationKeyboardFocus focus, KeyCode key, int count = 1)
        {
            for (int step = 0; step < count; step++) Require(focus.Handle(Key(key)), "Keyboard focus must consume " + key);
        }

        private static CommandResult Request(GameplaySession session, int chapter)
        {
            var id = StationQuests.RequiredStages[chapter];
            Require(session.TryGetTarget(id, out var target), "Missing chapter target " + id);
            return target.Request(StationQuests.Player, StationQuests.Confirm);
        }

        private static void Advance(GameplaySession session, FixedClock clock)
        {
            clock.Seconds += .7;
            session.Tick();
            session.Dispatch();
        }

        private static void Applied(CommandResult result, string message) =>
            Require(result.Succeeded, message + " (" + result.MessageKey + ")");

        private static void Require(bool passed, string message)
        {
            if (!passed) throw new InvalidOperationException(message);
        }

        private sealed class FixedClock : ISessionClock { public double Seconds { get; set; } }
    }
}
