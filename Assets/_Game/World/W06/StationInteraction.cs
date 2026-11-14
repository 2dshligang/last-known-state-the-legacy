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

        [SerializeField] private StationFirstPerson player;
        [SerializeField] private Camera view;
        [SerializeField] private SessionBootstrap bootstrap;
        [SerializeField] private StationScienceCatalog science;
        [SerializeField] private StationGenerationManifest station;
        [SerializeField] private StationExteriorGate gate;
        [SerializeField] private StationPowerConsole powerConsole;
        [SerializeField] private StationShelterSet shelter;

        private EnglishContentCatalog text;
        private StationInteractionAnchor[] anchors;
        private GameplaySession observed;
        private string feedback = "";
        private double feedbackUntil;
        private double nextUse;
        private GUIStyle panel, line, heading;

        public GameplaySession Session => bootstrap.Current;
        public string StartError { get; private set; }
        public string Feedback => Time.unscaledTimeAsDouble < feedbackUntil ? feedback : "";
        public bool HasDoorPower => Session?.Power?.EssentialSupportSupplied == true;

        private void Awake()
        {
            text = EnglishContentCatalog.CreateDefault();
            anchors = station.GetComponentsInChildren<StationInteractionAnchor>();
            var ids = anchors.Select(anchor => anchor.TargetId).ToArray();
            StableId.ValidateUnique(ids);
            string[] missing = StationQuests.ValidateReachability(ids).ToArray();
            if (missing.Length != 0) StartError = "Some station controls are unavailable.";
        }

        private void OnEnable()
        {
            player.InteractPressed += OnInteractionPressed;
            player.RestartRequested += NewRun;
        }

        private void OnDisable()
        {
            player.InteractPressed -= OnInteractionPressed;
            player.RestartRequested -= NewRun;
        }

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
            ResearchDraft research)
        {
            var errors = bootstrap.RestartWithPower(science, RequiredScience,
                () => Array.Empty<IInteractable>(), _ => ResultCode.EvidenceInsufficient,
                powerCheckpoint: power, questCheckpoint: quests, lifeSupportCheckpoint: lifeSupport,
                logisticsCheckpoint: logistics, archiveCheckpoint: archive,
                investigationCheckpoint: investigation, sampleCheckpoint: samples, researchCheckpoint: research);
            if (errors.Count != 0)
            {
                StartError = errors[0];
                return false;
            }
            StartError = null;
            observed = Session;
            nextUse = 0;
            feedback = "";
            player.RestartAtSpawn();
            gate.Present(HasDoorPower, player.transform.position);
            return true;
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
                    Physics.Linecast(ray.origin, anchor.transform.position, Physics.DefaultRaycastLayers,
                        QueryTriggerInteraction.Ignore))
                    continue;
                nearest = anchor;
                nearestDistance = delta.magnitude;
            }
            return nearest;
        }

        public string Describe(StationInteractionAnchor anchor)
        {
            if (shelter != null && StationShelterSet.Handles(anchor.TargetId))
                return shelter.Describe(anchor.TargetId);
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
            if (shelter != null && StationShelterSet.Handles(anchor.TargetId))
                return shelter.Open(anchor.TargetId);
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

        private string Resolve(StableId key) => text.Resolve(key, Debug.isDebugBuild || Application.isEditor);

        private string Unavailable(StableId target, StableId reason)
        {
            if (target.value == "quest.safe-power" && reason.value == "quest.result.resources")
                return HasDoorPower ? "Wait for the air reserve to recover." : "Restore air and water at the recovery control.";
            if (reason.value == "quest.result.evidence")
                return target.value switch
                {
                    "quest.echo-evidence" => "Confirm the ECHO evidence before completing this step.",
                    "quest.sampling" => "Record and collect all required specimens.",
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
            if (panel == null)
            {
                panel = new GUIStyle(GUI.skin.box);
                line = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true, alignment = TextAnchor.MiddleCenter };
                heading = new GUIStyle(line) { fontStyle = FontStyle.Bold };
            }
            float scale = Mathf.Clamp(Screen.height / 640f, .65f, 1.5f);
            var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            try
            {
                float width = Screen.width / scale;
                float height = Screen.height / scale;
                var objective = Objective();
                if (objective != null)
                {
                    Vector3 offset = view.transform.InverseTransformPoint(objective.Approach.position);
                    string direction = offset.z < -.5f ? "Behind you" :
                        offset.x < -.7f ? "Left" : offset.x > .7f ? "Right" : "Ahead";
                    Draw(new Rect(width / 2 - 200, 15, 400, 32),
                        "NEXT   " + Name(objective.TargetId) + "   /   " + direction, heading);
                }
                var aimed = FindAimedTarget(new Ray(view.transform.position, view.transform.forward));
                string prompt = aimed == null ? "" : Describe(aimed);
                if (aimed == null && !gate.IsOpen && gate.IsFacing(view.transform))
                    prompt = "Exit through the exterior airlock\nRestore air and water at the power recovery control.";
                if (prompt.Length > 0) Draw(new Rect(width / 2 - 230, height / 2 + 30, 460, 62), prompt, line);
                var result = StartError ?? Feedback;
                if (!string.IsNullOrEmpty(result))
                    Draw(new Rect(width / 2 - 230, height - 116, 460, 42), result, heading);
            }
            finally { GUI.matrix = previous; }
        }

        private void Draw(Rect area, string label, GUIStyle style)
        {
            GUI.Box(area, GUIContent.none, panel);
            GUI.Label(new Rect(area.x + 8, area.y, area.width - 16, area.height), label, style);
        }
    }
}
