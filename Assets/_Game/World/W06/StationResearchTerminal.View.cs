using System.Linq;
using UnityEngine;

namespace LKS.World
{
    public sealed partial class StationResearchTerminal
    {
        private const int LastControl = 15;
        private const float Width = 1100;
        private const float Height = 668;
        private static readonly string[] Sections = { "interpretation", "alternatives", "uncertainty", "follow-up" };
        private static readonly StableId Organics = new StableId("science.organics");
        private static readonly Color Background = new Color32(14, 23, 29, 255);
        private static readonly Color Surface = new Color32(24, 38, 48, 255);
        private static readonly Color Control = new Color32(48, 67, 77, 255);
        private static readonly Color Ink = new Color32(240, 247, 249, 255);
        private static readonly Color Muted = new Color32(188, 211, 221, 255);
        private static readonly Color Accent = new Color32(112, 225, 206, 255);
        private static readonly Color Notice = new Color32(255, 203, 131, 255);
        private GUIStyle title, heading, body, small, button, hitArea;

        private void OnGUI()
        {
            if (!CanControl) return;
            EnsureStyles();
            HandleKeys(Event.current);
            if (!IsOpen) return;
            var original = GUI.matrix;
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, .73f));
            float scale = Mathf.Min(1.5f, Screen.width / (Width + 28), Screen.height / (Height + 28));
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - Width * scale) / 2,
                (Screen.height - Height * scale) / 2, 0), Quaternion.identity, new Vector3(scale, scale, 1));
            try
            {
                Fill(new Rect(0, 0, Width, Height), Background);
                Fill(new Rect(0, 0, Width, 3), Accent);
                if (reference == null) DrawReport();
                else DrawReference();
            }
            finally { GUI.matrix = original; }
        }

        private void DrawReport()
        {
            Write(new Rect(21, 14, 852, 53), "Dmitry's research report", title, Ink);
            Write(new Rect(23, 70, 800, 25), Accepted ? "Accepted research" :
                "Use the station evidence to finish the report.", body, Accepted ? Accent : Muted);
            if (Button(new Rect(885, 26, 191, 37), "Return to station", 0))
            {
                ExitToStation();
                return;
            }
            if (Button(new Rect(706, 26, 163, 37), "Save progress", 15)) SaveProgress();

            Fill(new Rect(22, 106, 685, 458), Surface);
            Write(new Rect(37, 116, 647, 29), Resolve(new StableId("report.section.observations")), heading, Ink);
            for (int index = 0; index < Observations.Length; index++)
            {
                var choice = Observations[index];
                string label = (selected.Contains(choice) ? "[x]   " : "[ ]   ") + Resolve(StationResearch.TextKey(choice));
                if (Button(new Rect(38, 151 + index * 30, 651, 26), label, index + 1, !Accepted))
                    ToggleObservation(choice);
            }
            for (int index = 0; index < Sections.Length; index++)
            {
                float y = 280 + index * 68;
                Write(new Rect(37, y, 653, 25), Resolve(new StableId("report.section." + Sections[index])), heading, Ink);
                var key = ChoiceKey(index);
                string label = key.IsValid ? Resolve(key) : "Choose a statement";
                if (Button(new Rect(38, y + 27, 651, 33), label + (Accepted ? "" : "   >"), 5 + index, !Accepted))
                    CycleChoice(index, 1);
            }
            if (Button(new Rect(22, 579, 226, 40), Accepted ? "Confirm report step" : "Submit report", 9, CanSubmit))
                SubmitReport();
            string result = Feedback ?? (CanSubmit ? "" : Accepted ?
                "The research report is complete." : "Finish reviewing the laboratory results first.");
            Write(new Rect(266, 575, 441, 64), result, body, Feedback == null || FeedbackSucceeded ? Muted : Notice);
            DrawEvidenceAndTransmission();
            Write(new Rect(23, 645, 1055, 20),
                "Tab moves between controls. Enter selects. Left and Right change a statement. Escape pauses.", small, Muted);
        }

        private void DrawEvidenceAndTransmission()
        {
            Fill(new Rect(722, 106, 354, 236), Surface);
            Write(new Rect(736, 116, 326, 27), "Station evidence", heading, Ink);
            if (evidence.Count == 0)
                Write(new Rect(736, 153, 320, 57), "Review results in the laboratory to see them here.", body, Muted);
            for (int index = 0; index < evidence.Count; index++)
            {
                var sample = evidence[index].StationResult.Definition;
                if (Button(new Rect(736, 151 + index * 41, 326, 35), NameOf(sample.Id), index + 10))
                    OpenReference(sample);
            }
            if (FeedbackReference != null &&
                Button(new Rect(736, 283, 326, 35), "Read context for the feedback", 13))
                OpenReference(FeedbackReference);

            Fill(new Rect(722, 354, 354, 210), Surface);
            Write(new Rect(736, 364, 325, 32), "Transmission", heading, Ink);
            Write(new Rect(736, 401, 325, 47), Transmission, body, Accent);
            Write(new Rect(736, 451, 325, 65),
                "Station model. The queue advances with transmitter power. You can keep exploring.", small, Muted);
            if (Button(new Rect(736, 515, 326, 35), "Queue accepted report", 14, CanQueue)) QueueReport();
            Write(new Rect(724, 580, 350, 59), Resolve(Organics), small, Muted);
        }

        private void DrawReference()
        {
            var sample = reference;
            Write(new Rect(22, 18, 825, 52), NameOf(sample.Id), title, Ink);
            if (Button(new Rect(843, 27, 233, 39), "Back to report", 0))
            {
                reference = null;
                return;
            }
            var result = evidence.FirstOrDefault(item => item.StationResult.Definition.Id == sample.Id);
            Fill(new Rect(22, 109, 1054, 214), Surface);
            Write(new Rect(38, 121, 1015, 42), "Fictional ASTERION station evidence", heading, Accent);
            Write(new Rect(38, 176, 1010, 59), result == null ? "Station result pending." :
                Resolve(result.StationResult.ObservationKey), body, Ink);
            if (result != null)
                Write(new Rect(38, 240, 1010, 64), Resolve(result.StationResult.InterpretationKey), body, Muted);
            Fill(new Rect(22, 340, 1054, 213), Surface);
            Write(new Rect(38, 354, 1015, 41), "Science reference", heading, Ink);
            Write(new Rect(38, 407, 1010, 66), Resolve(result?.ReferenceContextKey ?? sample.ContextKey), body, Muted);
            Write(new Rect(38, 489, 1010, 39),
                (result?.ReferenceSource ?? sample.Reference).AbsoluteUri, small, Accent);
            Write(new Rect(23, 637, 1053, 28),
                "The report keeps your choices while you read. Enter returns to the report.", small, Muted);
        }

        private static string NameOf(StableId id) => id.value switch
        {
            "sample.layered-mudstone" => "Layered mudstone",
            "sample.iron-rich-rock" => "Iron-rich rock",
            _ => "Empty control"
        };

        private StableId ChoiceKey(int section) => section switch
        {
            0 when interpretation != ReportInterpretation.Unspecified => StationResearch.TextKey(interpretation),
            1 when alternative != ReportAlternative.Unspecified => StationResearch.TextKey(alternative),
            2 when uncertainty != ReportUncertainty.Unspecified => StationResearch.TextKey(uncertainty),
            3 when followUp != ReportFollowUp.Unspecified => StationResearch.TextKey(followUp),
            _ => default
        };

        internal void HandleKeys(Event input)
        {
            if (!CanControl || input.type != EventType.KeyDown) return;
            if (reference != null)
            {
                if (input.keyCode != KeyCode.Return && input.keyCode != KeyCode.KeypadEnter) return;
                reference = null;
            }
            else
            {
                switch (input.keyCode)
                {
                    case KeyCode.Tab:
                        int step = input.shift ? -1 : 1;
                        do { focus = (focus + step + LastControl + 1) % (LastControl + 1); }
                        while (!Enabled(focus));
                        break;
                    case KeyCode.LeftArrow:
                    case KeyCode.RightArrow:
                        if (focus >= 5 && focus <= 8) CycleChoice(focus - 5, input.keyCode == KeyCode.LeftArrow ? -1 : 1);
                        break;
                    case KeyCode.Return:
                    case KeyCode.KeypadEnter:
                        ActivateFocus();
                        break;
                    default: return;
                }
            }
            input.Use();
        }

        private void ActivateFocus()
        {
            if (!Enabled(focus)) return;
            switch (focus)
            {
                case 0: ExitToStation(); break;
                case >= 1 and <= 4: ToggleObservation(Observations[focus - 1]); break;
                case >= 5 and <= 8: CycleChoice(focus - 5, 1); break;
                case 9: SubmitReport(); break;
                case >= 10 and <= 12: OpenReference(evidence[focus - 10].StationResult.Definition); break;
                case 13: OpenReference(FeedbackReference); break;
                case 14: QueueReport(); break;
                case 15: SaveProgress(); break;
            }
        }

        private bool Enabled(int control) => control switch
        {
            >= 1 and <= 8 => !Accepted,
            9 => CanSubmit,
            >= 10 and <= 12 => control - 10 < evidence.Count,
            13 => FeedbackReference != null,
            14 => CanQueue,
            _ => true
        };

        private bool Button(Rect rect, string label, int control, bool enabled = true)
        {
            if (focus == control && enabled)
                Fill(new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4), Notice);
            Fill(rect, enabled ? Control : Background);
            bool original = GUI.enabled;
            GUI.enabled = enabled && original;
            bool clicked = GUI.Button(rect, GUIContent.none, hitArea);
            GUI.enabled = original;
            Write(rect, label, button, enabled ? Ink : Muted);
            if (clicked) focus = control;
            return clicked;
        }

        private void EnsureStyles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 36, fontStyle = FontStyle.Bold };
            body = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
            heading = new GUIStyle(body) { fontSize = 21, fontStyle = FontStyle.Bold };
            small = new GUIStyle(body) { fontSize = 16 };
            button = new GUIStyle(body) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(12, 8, 0, 0) };
            hitArea = new GUIStyle(GUIStyle.none);
        }

        private static void Write(Rect rect, string text, GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            GUI.Label(rect, text, style);
        }

        private static void Fill(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            var original = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = original;
        }
    }
}
