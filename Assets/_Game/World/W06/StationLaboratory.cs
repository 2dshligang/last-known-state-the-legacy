using System;
using System.Linq;
using UnityEngine;

namespace LKS.World
{
    [DisallowMultipleComponent]
    public sealed class StationLaboratory : MonoBehaviour
    {
        internal static readonly StableId Target = new StableId("quest.analysis");
        private static readonly StableId Sampling = new StableId("quest.sampling");
        private StationFirstPerson player;
        private StationInteraction interaction;
        private EnglishContentCatalog text;
        private GameplaySession session;
        private IDisposable input;
        private StableId selected;
        private Vector2 scroll;
        private string feedback;
        private float activity;
        private readonly StationKeyboardFocus keyboard = new();
        private GUIStyle body, title, subtitle, button;

        internal string BlockReason
        {
            get
            {
                var current = interaction.Session;
                if (!isActiveAndEnabled || current?.Samples == null) return "Laboratory is unavailable.";
                return current.Quests.Snapshot[Sampling] == QuestStatus.Completed
                    ? null : "Complete specimen collection at the dust check first.";
            }
        }

        internal bool IsOpen => input != null;
        private bool CanControl => IsOpen && !player.IsPaused && player.HasApplicationFocus &&
            ReferenceEquals(session, interaction.Session);
        private bool Powered => session.Power.Snapshot.Loads.Any(load => load.Id == StationPower.Lab && load.Supplied);
        private SampleReading Pending => session.Samples.Readings.Values
            .FirstOrDefault(sample => sample.Phase == SamplePhase.AnalysisPending);

        internal void Bind(StationFirstPerson owner, StationInteraction bridge, EnglishContentCatalog catalog)
        {
            player = owner;
            interaction = bridge;
            text = catalog;
        }

        internal bool Open()
        {
            if (BlockReason != null || player.IsPaused || player.HasReadingFocus || !player.HasApplicationFocus)
                return false;
            session = interaction.Session;
            selected = Pending?.Definition.Id ?? StationSamples.Blank;
            scroll = Vector2.zero;
            activity = 0;
            feedback = null;
            keyboard.Reset();
            input = player.AcquireLivePanelFocus(Close);
            return true;
        }

        internal void Close()
        {
            var lease = input;
            input = null;
            session = null;
            lease?.Dispose();
        }

        private void OnDisable() => Close();

        private void Update()
        {
            if (!IsOpen) return;
            if (!ReferenceEquals(session, interaction.Session))
            {
                Close();
                return;
            }
            if (CanControl && Powered && Pending != null)
                activity = Mathf.Repeat(activity + Time.deltaTime, 1f);
        }

        internal bool StartAnalysis(StableId id)
        {
            if (!CanControl) return false;
            var result = session.Samples.Analyze(id);
            if (result.Code != ResultCode.Applied) return Report(result);
            feedback = null;
            return true;
        }
        internal bool CancelAnalysis() => CanControl && Report(session.Samples.Cancel());
        internal bool Clean() => CanControl && Report(session.Samples.CleanForRetry());

        internal bool Interpret(StableId id, bool compareWithControl)
        {
            if (!CanControl) return false;
            if (!compareWithControl)
            {
                feedback = "An instrument signal can come from the tools. Compare it with the empty control.";
                return false;
            }
            return Report(session.Samples.Review(id));
        }

        internal bool Confirm()
        {
            if (!CanControl || !session.TryGetTarget(Target, out var target)) return false;
            var state = target.Read(StationQuests.Player);
            return Report(target.Request(StationQuests.Player, state.PromptKey));
        }

        private bool Report(CommandResult result)
        {
            feedback = Resolve(result.MessageKey);
            return result.Code == ResultCode.Applied;
        }

        private string Resolve(StableId key) => text.Resolve(key, Debug.isDebugBuild || Application.isEditor);

        private void OnGUI()
        {
            if (!CanControl) return;
            if (keyboard.Handle(Event.current)) return;
            keyboard.Begin();
            var input = Event.current;
            if (input.type == EventType.KeyDown && input.keyCode is KeyCode.PageUp or KeyCode.PageDown)
            {
                scroll.y = Mathf.Max(0, scroll.y + (input.keyCode == KeyCode.PageUp ? -180 : 180));
                input.Use();
            }
            body ??= new GUIStyle(GUI.skin.label) { wordWrap = true };
            body.fontSize = StationUi.TextSize(17, 20);
            button ??= StationUi.ButtonStyle(17, wrap: true);
            button.fontSize = StationUi.TextSize(14, 17);
            title ??= new GUIStyle(body) { fontSize = 28, fontStyle = FontStyle.Bold };
            subtitle ??= new GUIStyle(body) { fontStyle = FontStyle.Bold };
            subtitle.fontSize = body.fontSize;
            const float width = 760, height = 535;
            float scale = Mathf.Min(1.4f, Screen.width / (width + 24), Screen.height / (height + 24));
            var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - width * scale) / 2,
                (Screen.height - height * scale) / 2, 0), Quaternion.identity, new Vector3(scale, scale, 1));
            try
            {
                GUI.Box(new Rect(0, 0, width, height), GUIContent.none);
                var color = GUI.color;
                GUI.color = StationUi.Panel;
                GUI.DrawTexture(new Rect(0, 0, width, height), Texture2D.whiteTexture);
                GUI.color = color;
                GUI.Label(new Rect(18, 10, 525, 37), "Sample analysis laboratory", title);
                if (keyboard.Button(new Rect(581, 14, 160, 30), "Return to station", "close", button, Close))
                    return;
                GUI.Label(new Rect(18, 503, 724, 25),
                    "Tab and Enter select. Page Up and Page Down scroll the reading.", body);

                GUILayout.BeginArea(new Rect(18, 55, 724, 439));
                try { DrawReadings(); }
                finally { GUILayout.EndArea(); }
            }
            finally { GUI.matrix = previous; }
        }

        private void DrawReadings()
        {
            var samples = session.Samples;
            var pending = Pending;
            var reading = samples.Readings[selected];
            string power = Powered ? "Lab power supplied" : "Lab power unavailable";
            string control = samples.BlankControl switch
            {
                ControlStatus.Valid => "Passed",
                ControlStatus.Invalid => "Failed: instrument background detected",
                ControlStatus.Pending => "Run in progress",
                _ => "Awaiting a run"
            };
            GUILayout.Label(power + "   |   Fictional blank result: " + control, subtitle);
            if (!Powered)
                GUILayout.Label("Supply the lab at the power bay. If power is limited, turn off an optional load or choose the lab plan at the seedling cabinet.", body);
            if (pending != null)
            {
                string status = Powered ? "Analyzing" + new string('.', 1 + (int)(activity * 3)) : "Paused until lab power returns";
                GUILayout.Label(status + "  " + Name(pending.Definition.Id), subtitle);
                GUILayout.Label("Compare field notes with the empty control, or return to the station.", body);
                ActionButton("Cancel active run", "cancel", () => CancelAnalysis(), height: 26);
            }

            GUILayout.BeginHorizontal();
            foreach (var id in new[] { StationSamples.Blank, new StableId("sample.layered-mudstone"),
                new StableId("sample.iron-rich-rock") })
            {
                ActionButton(Name(id) + (id == selected ? "  [Selected]" : ""), id.value,
                    () => { selected = id; scroll = Vector2.zero; }, height: 32);
            }
            GUILayout.EndHorizontal();

            scroll = GUILayout.BeginScrollView(scroll);
            DrawSample(reading, samples.BlankControl);
            GUILayout.EndScrollView();

            if (samples.BlankControl == ControlStatus.Invalid && pending == null)
            {
                GUILayout.Label("Clean the chamber and run a fresh blank. Cleaning keeps the samples and clears old results.", body);
                ActionButton("Clean chamber", "clean", () => Clean(), height: 28);
            }
            if (!string.IsNullOrEmpty(feedback)) GUILayout.Label(feedback, subtitle);
            if (samples.AllReviewed && session.Quests.Snapshot[Target] == QuestStatus.Available)
                ActionButton("Confirm sample analysis", "confirm", () => Confirm(), height: 32);
            if (session.Quests.Snapshot[Target] == QuestStatus.Completed)
                GUILayout.Label("Sample analysis complete. Continue to the research report.", subtitle);
        }

        private void DrawSample(SampleReading reading, ControlStatus control)
        {
            var id = reading.Definition.Id;
            GUILayout.Label(Name(id), subtitle);
            if (id == StationSamples.Blank)
                GUILayout.Label("A blank is an empty control. It checks whether the tools or instrument add a signal.", body);
            GUILayout.Label(Resolve(reading.Definition.ContextKey), body);
            GUILayout.Label("NASA reference for context", subtitle);
            GUILayout.Label(reading.Definition.Reference.AbsoluteUri, body);
            ActionButton("Open NASA reference", "source",
                () => Application.OpenURL(reading.Definition.Reference.AbsoluteUri), inReading: true);

            if (reading.Phase >= SamplePhase.EvidenceAvailable)
            {
                const string fictional = "Simulated instrument response / Fiction";
                StationTextCapture.Record("science.sample", fictional);
                GUILayout.Label(fictional, subtitle);
                GUILayout.Label(Resolve(reading.ObservationKey), body);
                GUILayout.Label(Resolve(reading.InterpretationKey), body);
                if (reading.Phase == SamplePhase.Reviewed)
                    GUILayout.Label("Interpretation reviewed.", subtitle);
                else
                {
                    GUILayout.Label("How should you use this signal?", subtitle);
                    string supported = control == ControlStatus.Valid
                        ? "Compare with the passed blank before interpreting the result"
                        : "Treat the result as limited until the blank passes";
                    ActionButton(supported, "compare", () => Interpret(id, true), inReading: true);
                    ActionButton("Treat the signal as proof of life", "unsupported",
                        () => Interpret(id, false), inReading: true);
                }
            }
            else if (reading.Phase == SamplePhase.Collected)
            {
                ActionButton("Run analysis", "analyze", () => StartAnalysis(id), inReading: true);
            }
            else if (reading.Phase < SamplePhase.Collected)
                GUILayout.Label("Prepare or collect this specimen at its station first.", body);

        }

        private void ActionButton(string label, string id, Action action, bool inReading = false, float height = 0)
        {
            bool clicked = height > 0 ? GUILayout.Button(label, button, GUILayout.Height(height)) :
                GUILayout.Button(label, button);
            if (Event.current.type == EventType.Repaint)
            {
                Rect area = GUILayoutUtility.GetLastRect();
                keyboard.Register(area, id, action, reveal: inReading ? () => RevealReadingControl(area) : null);
            }
            if (clicked) action();
        }

        private void RevealReadingControl(Rect area)
        {
            if (area.y < scroll.y || area.yMax > scroll.y + 160)
                scroll.y = Mathf.Max(0, area.y - 32);
        }

        private static string Name(StableId id) => id.value switch
        {
            "sample.procedural-blank" => "Empty control",
            "sample.layered-mudstone" => "Layered rock",
            "sample.iron-rich-rock" => "Iron-rich rock",
            _ => "Sample"
        };
    }
}
