using System.Globalization;
using System.Linq;
using UnityEngine;

namespace LKS.World
{
    public sealed partial class StationEchoInvestigation
    {
        private const float PanelWidth = 1104;
        private const float PanelHeight = 658;
        private static readonly Color Panel = new Color32(14, 23, 29, 255);
        private static readonly Color Surface = new Color32(24, 38, 48, 255);
        private static readonly Color Control = new Color32(48, 67, 77, 255);
        private static readonly Color Ink = new Color32(240, 247, 249, 255);
        private static readonly Color Muted = new Color32(190, 211, 221, 255);
        private static readonly Color Accent = new Color32(111, 227, 219, 255);
        private static readonly Color Notice = new Color32(255, 203, 131, 255);
        private GUIStyle title, heading, body, small, centered, hitArea;
        private bool exitFromSource;

        private void OnGUI()
        {
            if (!CanControl) return;
            EnsureStyles();
            HandleKeys(Event.current);
            if (!IsOpen) return;
            var old = GUI.matrix;
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, .76f));
            float scale = Mathf.Min(1.45f, Screen.width / (PanelWidth + 28), Screen.height / (PanelHeight + 28));
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - PanelWidth * scale) / 2,
                (Screen.height - PanelHeight * scale) / 2, 0), Quaternion.identity, new Vector3(scale, scale, 1));
            try
            {
                Fill(new Rect(0, 0, PanelWidth, PanelHeight), Panel);
                Fill(new Rect(0, 0, PanelWidth, 3), Accent);
                if (Source.IsValid) DrawSource();
                else DrawInvestigation();
                if (IsOpen && !string.IsNullOrEmpty(Feedback))
                    Write(new Rect(26, 574, 1049, 48), Feedback, body, FeedbackSucceeded ? Accent : Notice);
            }
            finally { GUI.matrix = old; }
        }

        private void DrawInvestigation()
        {
            Write(new Rect(24, 17, 831, 50), "ECHO investigation", title, Ink);
            Write(new Rect(26, 72, 860, 32),
                "Fictional ASTERION incident  /  Compare the records and explain what happened.", body, Muted);
            if (Button(new Rect(909, 25, 171, 42), "Exit investigation", keyboardFocus == 9))
            {
                ExitToStation();
                return;
            }
            DrawComparison();
            DrawClues();
            DrawOrder();
            Write(new Rect(26, 625, 1052, 24), "Tab moves between controls. Enter selects the outlined control.", small, Muted);
        }

        private void DrawComparison()
        {
            Fill(new Rect(24, 109, 650, 179), Surface);
            Write(new Rect(37, 119, 611, 28), "Compare what the station recorded", heading, Ink);
            var sensor = RecordOf(EchoInvestigation.Timing);
            var action = RecordOf(EchoInvestigation.Decision);
            var model = RecordOf(BackupEstimate);
            Metric(38, 203, "Sensor timestamp", TimeOf(sensor));
            Metric(240, 184, "Forecast / model flag", SourceType(model));
            Metric(442, 209, "Controller action", TimeOf(action));
            var age = action.Evidence.RecordedAt - sensor.Evidence.RecordedAt;
            string ageText = age.HasValue
                ? "The reading was " + age.Value.TotalMinutes.ToString("0", CultureInfo.InvariantCulture) + " minutes old. "
                : "";
            string state = Investigation.ControlReadingFreshness switch
            {
                ReadingFreshness.Stale => "The timing rule marks it as too old for the decision.",
                ReadingFreshness.Fresh => "The timing rule marks the timestamp as recent enough.",
                ReadingFreshness.Uncertain => "The known timestamps leave too much uncertainty.",
                _ => "The timestamp is unknown."
            };
            Write(new Rect(38, 215, 611, 25), Resolve(action.CaptionKey), small, Ink);
            Write(new Rect(38, 243, 611, 40), ageText + state, small, Muted);
        }

        private void Metric(float x, float width, string name, string value)
        {
            Write(new Rect(x, 161, width, 22), name, small, Muted);
            Write(new Rect(x, 183, width, 28), value, heading, Ink);
        }

        private void DrawClues()
        {
            Fill(new Rect(24, 300, 650, 261), Surface);
            Write(new Rect(37, 310, 626, 30), "Source clues", heading, Ink);
            bool confirmed = Investigation.Conclusion == EchoConclusion.Confirmed;
            for (int i = 0; i < Clues.Count; i++)
            {
                var id = Clues[i];
                float y = 348 + i * 68;
                bool collected = Checkpoint.Collected.Contains(id);
                bool selected = draft.Contains(id);
                Fill(new Rect(38, y, 621, 61), Panel);
                Write(new Rect(50, y + 5, 334, 27), TitleOf(id), heading, Ink);
                Write(new Rect(51, y + 33, 330, 24),
                    TimeOf(RecordOf(id)) + "  /  " + (collected ? "Inspected" : "Uninspected"), small, Muted);
                if (Button(new Rect(382, y + 12, 108, 36), "Inspect", keyboardFocus == i))
                    Inspect(id);
                if (Button(new Rect(502, y + 12, 145, 36), selected ? "Remove" : "Add to order",
                    keyboardFocus == i + 3, collected && !confirmed))
                    ToggleOrder(id);
            }
        }

        private void DrawOrder()
        {
            Fill(new Rect(690, 109, 390, 452), Surface);
            Write(new Rect(704, 120, 360, 32), "Your explanation", heading, Ink);
            Write(new Rect(704, 157, 357, 54), "Add inspected clues in the order you think they happened.", body, Muted);
            for (int i = 0; i < Clues.Count; i++)
            {
                float y = 220 + i * 45;
                Fill(new Rect(705, y, 360, 37), Panel);
                string item = i < draft.Count ? TitleOf(draft[i]) : "Choose a clue";
                Write(new Rect(715, y + 5, 331, 28), (i + 1) + ".  " + item, body, i < draft.Count ? Ink : Muted);
            }
            bool confirmed = Investigation.Conclusion == EchoConclusion.Confirmed;
            Write(new Rect(704, 363, 359, 31), confirmed ? "Explanation confirmed" : "Sequence to check",
                heading, confirmed ? Accent : Ink);
            if (Button(new Rect(705, 402, 360, 36), "Check sequence", keyboardFocus == 7)) SubmitOrder();
            bool complete = QuestCompleted;
            if (Button(new Rect(705, 448, 360, 36), complete ? "ECHO step complete" : "Confirm ECHO step",
                keyboardFocus == 8, confirmed && !complete)) ConfirmQuest();
            if (Button(new Rect(705, 494, 360, 36), "Read model and missing interval", keyboardFocus == 6)) InspectContext();
            Write(new Rect(705, 535, 360, 25), openedSession.Archive.RequiredEntryAcknowledged
                ? "Required archive entry marked as read" : "Mark Solar particle observation as read", small, Muted);
        }

        private void DrawSource()
        {
            var record = RecordOf(Source);
            bool context = Source == BackupEstimate;
            Write(new Rect(24, 17, 829, 51), TitleOf(Source), title, Ink);
            Write(new Rect(26, 72, 858, 28), "Source card  /  Fictional ASTERION evidence", body, Muted);
            if (Button(new Rect(909, 25, 171, 42), "Exit investigation", exitFromSource))
            {
                ExitToStation();
                return;
            }
            Fill(new Rect(24, 112, 1056, 361), Surface);
            Write(new Rect(41, 130, 1004, 29), SourceType(record) + "  /  " + TimeOf(record), heading, Accent);
            Write(new Rect(41, 178, 1004, 68), Resolve(record.CaptionKey), title, Ink);
            Write(new Rect(41, 261, 1004, 36), context ? "Limits of this record" : "How to use this clue", heading, Ink);
            string guidance;
            if (context)
            {
                var missing = RecordOf(MissingInterval);
                guidance = "The estimate is a simplified model of what may have happened. " +
                    Resolve(missing.CaptionKey) + " The missing interval has no original timestamp.";
            }
            else if (Source == EchoInvestigation.Timing || Source == EchoInvestigation.Decision)
                guidance = Resolve(FreshnessRule);
            else
                guidance = "This is a later log entry. It shows the outage, while the reading and controller logs help explain the order.";
            Write(new Rect(41, 311, 998, 84), guidance, body, Muted);
            Write(new Rect(41, 412, 998, 49),
                "The separate NASA training entry describes a historical observation.", body, Muted);
            if (Button(new Rect(24, 493, 236, 43), "Back to comparison", !exitFromSource)) ReturnToComparison();
            Write(new Rect(26, 625, 1052, 24), "The comparison keeps your order while you read the source card.", small, Muted);
        }

        internal void HandleKeys(Event input)
        {
            if (!CanControl || input.type != EventType.KeyDown) return;
            if (input.keyCode == KeyCode.Tab)
            {
                if (Source.IsValid) exitFromSource = !exitFromSource;
                else
                {
                    int change = input.shift ? -1 : 1;
                    do { keyboardFocus = (keyboardFocus + change + 10) % 10; }
                    while (!FocusEnabled(keyboardFocus));
                }
            }
            else if (input.keyCode == KeyCode.Return || input.keyCode == KeyCode.KeypadEnter)
            {
                if (Source.IsValid)
                {
                    if (exitFromSource) ExitToStation();
                    else ReturnToComparison();
                }
                else if (keyboardFocus < 3) Inspect(Clues[keyboardFocus]);
                else if (keyboardFocus < 6) ToggleOrder(Clues[keyboardFocus - 3]);
                else if (keyboardFocus == 6) InspectContext();
                else if (keyboardFocus == 7) SubmitOrder();
                else if (keyboardFocus == 8 && FocusEnabled(8)) ConfirmQuest();
                else if (keyboardFocus == 9) ExitToStation();
            }
            else return;
            input.Use();
        }

        private bool FocusEnabled(int index) => index switch
        {
            >= 3 and < 6 => Investigation.Conclusion != EchoConclusion.Confirmed &&
                Checkpoint.Collected.Contains(Clues[index - 3]),
            8 => Investigation.Conclusion == EchoConclusion.Confirmed && !QuestCompleted,
            _ => true
        };

        private bool Button(Rect rect, string message, bool focused, bool enabled = true)
        {
            if (focused) Fill(new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4), Notice);
            Fill(rect, enabled ? Control : Panel);
            bool old = GUI.enabled;
            GUI.enabled = old && enabled;
            bool clicked = GUI.Button(rect, GUIContent.none, hitArea);
            GUI.enabled = old;
            Write(rect, message, centered, enabled ? Ink : Muted);
            return clicked;
        }

        private void EnsureStyles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, wordWrap = true };
            heading = new GUIStyle(GUI.skin.label) { fontSize = 21, fontStyle = FontStyle.Bold };
            body = new GUIStyle(GUI.skin.label) { fontSize = 19, wordWrap = true };
            small = new GUIStyle(body) { fontSize = 16 };
            centered = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            hitArea = new GUIStyle(GUIStyle.none);
        }

        private static void Write(Rect rect, string message, GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            GUI.Label(rect, message, style);
        }

        private static void Fill(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
