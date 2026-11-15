using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LKS.World
{
    [DisallowMultipleComponent]
    public sealed class StationInteraction : MonoBehaviour
    {
        public const float Range = 2.4f;
        private const float AimRadius = .22f;
        private const float RepeatDelay = .28f;
        private static readonly StableId[] RequiredScience = { new StableId("donki.2017-09-04t23-52-00-sep-001") };
        private static readonly StableId CampaignSlot = new StableId("campaign");

        [SerializeField] private StationFirstPerson player;
        [SerializeField] private Camera view;
        [SerializeField] private SessionBootstrap bootstrap;
        [SerializeField] private StationScienceCatalog science;
        [SerializeField] private StationGenerationManifest station;
        [SerializeField] private StationExteriorGate gate;
        [SerializeField] private StationPowerConsole powerConsole;
        [SerializeField] private StationShelterSet shelter;
        [SerializeField] private StationArchiveReader archiveReader;
        private StationResearchTerminal reportTerminal;
        private StationFinale finale;

        private readonly RaycastHit[] obstructionHits = new RaycastHit[8];
        private EnglishContentCatalog text;
        private StationInteractionAnchor[] anchors;
        private GameplaySession observed;
        private StationLaboratory laboratory;
        private StationOpening opening;
        private string feedback = "";
        private double feedbackUntil;
        private double nextUse;
        private GUIStyle line, heading;

        public GameplaySession Session => bootstrap.Current;
        public string StartError { get; private set; }
        public string Feedback => Time.unscaledTimeAsDouble < feedbackUntil ? feedback : "";
        public bool HasDoorPower => Session?.Power?.EssentialSupportSupplied == true;
        internal StationSaves CampaignStorage { get; set; }
        private StationSaves Campaign => CampaignStorage ??= StationSaves.ForPlayer(science, RequiredScience);

        private void Awake()
        {
            text = EnglishContentCatalog.CreateDefault();
            anchors = station.GetComponentsInChildren<StationInteractionAnchor>();
            var ids = anchors.Select(anchor => anchor.TargetId).ToArray();
            StableId.ValidateUnique(ids);
            if (ids.Contains(StationLaboratory.Target))
            {
                laboratory = gameObject.AddComponent<StationLaboratory>();
                laboratory.Bind(player, this, text);
            }
            var photoAnchor = anchors.FirstOrDefault(anchor => anchor.TargetId == StationEnding.Photograph);
            var photoStand = station.Entries.FirstOrDefault(entry => entry.id.value == "fixture.photo");
            opening = gameObject.AddComponent<StationOpening>();
            opening.Bind(player, this, photoAnchor, photoStand?.node?.GetComponent<Renderer>()?.sharedMaterial);
            string[] missing = StationQuests.ValidateReachability(ids).ToArray();
            if (missing.Length != 0) StartError = "Some station controls are unavailable.";
            if (ids.Contains(StationResearchTerminal.Target))
                reportTerminal = gameObject.AddComponent<StationResearchTerminal>();
            if (photoAnchor != null)
            {
                finale = gameObject.AddComponent<StationFinale>();
                finale.Bind(player, this, opening.Photograph);
            }
        }

        private void OnEnable()
        {
            player.InteractPressed += OnInteractionPressed;
            player.RestartRequested += NewRun;
            if (finale != null) player.GameMenuRequested += finale.OpenMenu;
        }

        private void OnDisable()
        {
            player.InteractPressed -= OnInteractionPressed;
            player.RestartRequested -= NewRun;
            opening?.Close();
            reportTerminal?.Close();
            if (finale != null)
            {
                player.GameMenuRequested -= finale.OpenMenu;
                finale.Close();
            }
        }

        internal bool HasGameMenu => finale != null;
        internal void OpenGameMenu() => finale?.OpenMenu();
        internal void SyncOpening() => opening?.Sync();

        private void Start()
        {
            if (Session == null && StartError == null) NewRun();
            gate.Present(HasDoorPower, player.transform.position);
        }

        private void Update()
        {
            if (!ReferenceEquals(observed, Session))
            {
                observed = Session;
                nextUse = 0;
                feedback = "";
            }
            gate.Present(HasDoorPower, player.transform.position);
        }

        public void NewRun() => Restore(null, null, null, null, null, null, null, null);

        public bool Restore(PowerCheckpoint power, IEnumerable<StableId> quests,
            LifeSupportCheckpoint lifeSupport, LogisticsCheckpoint logistics,
            IEnumerable<StableId> archive, EchoInvestigationCheckpoint investigation, SampleCheckpoint samples,
            ResearchDraft research, OpeningProgress? openingCheckpoint = null)
        {
            var restoredQuests = quests?.ToArray();
            var errors = bootstrap.RestartWithPower(science, RequiredScience,
                () => Array.Empty<IInteractable>(), _ => ResultCode.EvidenceInsufficient,
                powerCheckpoint: power, questCheckpoint: restoredQuests, lifeSupportCheckpoint: lifeSupport,
                logisticsCheckpoint: logistics, archiveCheckpoint: archive,
                investigationCheckpoint: investigation, sampleCheckpoint: samples, researchCheckpoint: research,
                openingCheckpoint: openingCheckpoint ?? (restoredQuests?.Length > 0
                    ? OpeningProgress.Complete : OpeningProgress.Farewell));
            if (errors.Count != 0)
            {
                StartError = errors[0];
                return false;
            }
            ResetSessionPresentation();
            return true;
        }

        internal CommandResult SaveCampaign() => Campaign.Save(CampaignSlot, Session);

        internal CommandResult LoadCampaign()
        {
            var result = Campaign.Load(CampaignSlot, bootstrap, () => Array.Empty<IInteractable>(),
                _ => ResultCode.EvidenceInsufficient);
            if (result.Succeeded) ResetSessionPresentation();
            return result;
        }

        private void ResetSessionPresentation()
        {
            StartError = null;
            observed = Session;
            nextUse = 0;
            feedback = "";
            player.RestartAtSpawn();
            SyncOpening();
            gate.Present(HasDoorPower, player.transform.position);
        }

        public StationInteractionAnchor FindAimedTarget(Ray ray)
        {
            StationInteractionAnchor nearest = null;
            float nearestDistance = Range;
            foreach (var anchor in anchors)
            {
                if (anchor == null || !anchor.isActiveAndEnabled) continue;
                Vector3 delta = anchor.transform.position - ray.origin;
                float along = Vector3.Dot(delta, ray.direction);
                if (along <= 0 || delta.sqrMagnitude > nearestDistance * nearestDistance ||
                    (delta - along * ray.direction).sqrMagnitude > AimRadius * AimRadius ||
                    IsObstructed(ray.origin, delta))
                    continue;
                nearest = anchor;
                nearestDistance = delta.magnitude;
            }
            return nearest;
        }

        private bool IsObstructed(Vector3 origin, Vector3 offset)
        {
            float distance = offset.magnitude;
            Vector3 forward = offset / distance;
            int count = Physics.RaycastNonAlloc(origin, forward, obstructionHits, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            // The view can graze the player's capsule. Other colliders must still block the ray.
            Transform playerRoot = player.transform;
            if (count == obstructionHits.Length)
                return Physics.RaycastAll(origin, forward, distance, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore).Any(hit => !hit.collider.transform.IsChildOf(playerRoot));
            for (int i = 0; i < count; i++)
            {
                if (!obstructionHits[i].collider.transform.IsChildOf(playerRoot)) return true;
            }
            return false;
        }

        private static readonly StableId Sampling = new StableId("quest.sampling");
        private bool CanSample => Session?.Quests?.Snapshot[Sampling] == QuestStatus.Available;

        private bool TrySample(StableId id, out SampleReading sample)
        {
            sample = null;
            return Session?.Samples?.Readings.TryGetValue(id, out sample) == true;
        }

        private string DescribeSample(SampleReading sample)
        {
            string label = sample.Definition.Id.value switch
            {
                "sample.layered-mudstone" => "Layered outcrop",
                "sample.iron-rich-rock" => "Rust-colored comparison rock",
                _ => "Laboratory blank"
            };
            if (sample.Phase >= SamplePhase.Collected) return label + " / Already collected";
            if (!CanSample) return label + " / Confirm the ECHO investigation first.";
            string action = sample.Phase == SamplePhase.Unseen ? "Record context" :
                sample.Definition.Id == StationSamples.Blank ? "Prepare empty control" : "Collect specimen";
            return "E  " + action + "\n" + label;
        }

        private bool SubmitSample(SampleReading sample)
        {
            if (!CanSample)
            {
                Show(DescribeSample(sample));
                return false;
            }

            var service = Session.Samples;
            var result = sample.Phase == SamplePhase.Unseen
                ? service.RecordContext(sample.Definition.Id)
                : service.Collect(sample.Definition.Id);
            nextUse = Time.unscaledTimeAsDouble + RepeatDelay;
            Show(Resolve(result.MessageKey));
            return result.Code == ResultCode.Applied;
        }

        public string Describe(StationInteractionAnchor anchor)
        {
            if (reportTerminal != null && anchor.TargetId == StationResearchTerminal.Target)
                return "E  Open research report\nReport terminal";
            if (finale != null && anchor.TargetId == StationEnding.Photograph) return finale.Prompt();
            if (TrySample(anchor.TargetId, out var sample)) return DescribeSample(sample);
            if (laboratory != null && anchor.TargetId == StationLaboratory.Target)
                return laboratory.BlockReason ?? "E  Open sample analyzer\nLaboratory";
            if (shelter != null && StationShelterSet.Handles(anchor.TargetId))
                return shelter.Describe(anchor.TargetId);
            if (archiveReader != null && anchor.TargetId == StationArchiveReader.Target)
                return archiveReader.HasArchive(Session) ? "E  Open station archive\nECHO archive" :
                    "Open station archive / ECHO archive\nArchive is unavailable.";
            if (powerConsole != null && anchor.TargetId == StationPowerConsole.Target)
            {
                string reason = powerConsole.OpenBlockReason;
                return reason == null ? "E  Open power console\nPower control" :
                    "Open power console / Power control\n" + reason;
            }
            string name = Name(anchor.TargetId);
            string action = ActionText(anchor.TargetId);
            if (!TryRead(anchor, out _, out var state))
                return action + " / " + name + "\nStation controls are unavailable.";
            if (!state.Available)
                return action + " / " + name + "\n" + Unavailable(anchor.TargetId, state.PromptKey);
            return "E  " + action + "\n" + name;
        }

        private bool TryRead(StationInteractionAnchor anchor, out IInteractable target, out InteractionState state)
        {
            state = default;
            target = null;
            if (Session == null || !Session.TryGetTarget(anchor.TargetId, out target)) return false;
            state = target.Read(StationQuests.Player);
            return true;
        }

        private void OnInteractionPressed()
        {
            if (player.CanInteract)
                Submit(new Ray(view.transform.position, view.transform.forward));
        }

        // The caller uses the same ray for display and submission. Always recheck live geometry and authority.
        internal bool Submit(Ray ray)
        {
            if (player.IsPaused || player.HasReadingFocus ||
                Time.unscaledTimeAsDouble < nextUse) return false;
            var anchor = FindAimedTarget(ray);
            if (anchor == null) return false;
            if (reportTerminal != null && anchor.TargetId == StationResearchTerminal.Target)
                return reportTerminal.Open(player, () => Session, SaveCampaign);
            if (finale != null && anchor.TargetId == StationEnding.Photograph)
            {
                bool completed = finale.Inspect(out string message);
                if (!string.IsNullOrEmpty(message)) Show(message);
                return completed;
            }
            if (TrySample(anchor.TargetId, out var sample)) return SubmitSample(sample);
            if (laboratory != null && anchor.TargetId == StationLaboratory.Target)
            {
                if (laboratory.Open()) return true;
                Show(laboratory.BlockReason ?? "Laboratory is unavailable.");
                return false;
            }
            if (shelter != null && StationShelterSet.Handles(anchor.TargetId))
                return shelter.Open(anchor.TargetId);
            if (archiveReader != null && anchor.TargetId == StationArchiveReader.Target)
            {
                if (archiveReader.Open(player, () => Session)) return true;
                Show("Archive is unavailable.");
                return false;
            }
            if (powerConsole != null && anchor.TargetId == StationPowerConsole.Target)
            {
                if (powerConsole.Open()) return true;
                Show(powerConsole.OpenBlockReason ?? "Power controls are unavailable.");
                return false;
            }
            if (!TryRead(anchor, out var target, out var state)) return false;
            if (!state.Available)
            {
                Show(Unavailable(anchor.TargetId, state.PromptKey));
                return false;
            }
            nextUse = Time.unscaledTimeAsDouble + RepeatDelay;
            var result = target.Request(StationQuests.Player, state.PromptKey);
            Show(Resolve(result.MessageKey));
            return result.Succeeded;
        }

        private void Show(string message)
        {
            feedback = message;
            feedbackUntil = Time.unscaledTimeAsDouble + 3.5;
        }

        internal string Resolve(StableId key) => text.Resolve(key, Debug.isDebugBuild || Application.isEditor);

        private string Unavailable(StableId target, StableId reason)
        {
            if (target.value == "quest.safe-power" && reason.value == "quest.result.resources")
                return HasDoorPower ? "Wait for the air reserve to recover." : "Restore air and water at the recovery control.";
            if (reason.value == "quest.result.evidence")
                return target.value switch
                {
                    "quest.echo-evidence" => "Confirm the ECHO evidence before completing this step.",
                    "quest.sampling" => "Collect both rocks and prepare the empty control in the lab.",
                    "quest.analysis" => "Review the lab results with a valid blank control.",
                    "quest.report" => "An accepted research report is required.",
                    "quest.ending" => "Finish the research before viewing the photograph.",
                    _ => Resolve(reason)
                };
            return Resolve(reason);
        }

        private static string ActionText(StableId id) => id.value switch
        {
            "quest.arrival" => "Confirm arrival",
            "quest.safe-power" => "Confirm safe life support",
            "quest.echo-evidence" => "Confirm ECHO evidence",
            "quest.sampling" => "Confirm specimen collection",
            "quest.analysis" => "Confirm sample analysis",
            "quest.report" => "Confirm research report",
            "quest.ending" => "View photograph",
            "target.power-recovery" => "Restore a safe power allocation",
            _ => "Confirm"
        };

        private static string Name(StableId id) => id.value switch
        {
            "quest.arrival" => "Arrival check",
            "quest.safe-power" => "Power control",
            "quest.echo-evidence" => "ECHO archive",
            "quest.sampling" => "Exterior sample pickup",
            "quest.analysis" => "Laboratory",
            "quest.report" => "Research report",
            "quest.ending" => "Photograph",
            "target.power-recovery" => "Power recovery control",
            _ => "Station control"
        };

        private StationInteractionAnchor Objective()
        {
            var snapshot = Session?.Quests?.Snapshot;
            if (snapshot == null) return null;
            foreach (var target in StationQuests.RequiredStages)
            {
                if (snapshot[target] != QuestStatus.Available) continue;
                return anchors.FirstOrDefault(anchor => anchor.TargetId == target);
            }
            return null;
        }

        private void OnGUI()
        {
            if (!player.HasWorldFocus || player.IsPaused) return;
            if (line == null)
            {
                line = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true, alignment = TextAnchor.MiddleCenter };
                heading = new GUIStyle(line) { fontStyle = FontStyle.Bold };
            }
            float scale = StationUi.HudScale(Mathf.Clamp(Screen.height / 640f, .65f, 1.5f), 520, 500);
            var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            try
            {
                float width = Screen.width / scale;
                float height = Screen.height / scale;
                float feedbackTop = 15;
                var objective = Objective();
                if (objective != null)
                {
                    Vector3 offset = view.transform.InverseTransformPoint(objective.Approach.position);
                    string direction = offset.z < -.5f ? "Behind you" :
                        offset.x < -.7f ? "Left" : offset.x > .7f ? "Right" : "Ahead";
                    string label = "NEXT   " + Name(objective.TargetId) + "   /   " + direction;
                    StationTextCapture.Record("quest.objective", label);
                    float panelWidth = Mathf.Min(400, width - 32);
                    float panelHeight = Mathf.Max(32,
                        Mathf.Ceil(heading.CalcHeight(new GUIContent(label), panelWidth - 16)) + 8);
                    Draw(new Rect((width - panelWidth) / 2, feedbackTop, panelWidth, panelHeight), label, heading);
                    feedbackTop += panelHeight + 8;
                }
                var aimed = FindAimedTarget(new Ray(view.transform.position, view.transform.forward));
                string prompt = aimed == null ? "" : Describe(aimed);
                if (aimed == null && !gate.IsOpen && gate.IsFacing(view.transform))
                    prompt = "Exit through the exterior airlock\nRestore air and water at the power recovery control.";
                var result = StartError ?? Feedback;
                if (prompt.Length > 0 && prompt != result)
                {
                    StationTextCapture.Record("interaction.prompt", prompt);
                    DrawMessage(width, height - 63, prompt, line, bottomAligned: true);
                }
                if (!string.IsNullOrEmpty(result))
                {
                    StationTextCapture.Record("interaction.error", result);
                    DrawMessage(width, feedbackTop, result, heading);
                }
            }
            finally { GUI.matrix = previous; }
        }

        private static void DrawMessage(float width, float edge, string label, GUIStyle style, bool bottomAligned = false)
        {
            float panelWidth = Mathf.Min(460, width - 32);
            float panelHeight = Mathf.Max(36, Mathf.Ceil(style.CalcHeight(new GUIContent(label), panelWidth - 16)) + 12);
            float top = bottomAligned ? edge - panelHeight : edge;
            Draw(new Rect((width - panelWidth) / 2, top, panelWidth, panelHeight), label, style);
        }

        private static void Draw(Rect area, string label, GUIStyle style)
        {
            StationUi.Fill(area, new Color32(16, 25, 32, 248));
            GUI.Label(new Rect(area.x + 8, area.y, area.width - 16, area.height), label, style);
        }
    }
}
