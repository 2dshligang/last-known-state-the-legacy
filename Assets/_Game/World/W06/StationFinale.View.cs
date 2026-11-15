using UnityEngine;

namespace LKS.World
{
    public sealed partial class StationFinale
    {
        private const float Width = 900;
        private const float Height = 580;
        private static readonly Color Background = new Color32(13, 20, 26, 255);
        private static readonly Color Control = new Color32(47, 65, 73, 255);
        private static readonly Color Ink = new Color32(240, 239, 233, 255);
        private static readonly Color Muted = new Color32(189, 209, 217, 255);
        private static readonly Color Accent = new Color32(245, 194, 128, 255);
        private GUIStyle title, line, detail, hitArea;

        private void OnGUI()
        {
            if (!CanControl) return;
            EnsureStyles();
            title.fontSize = StationUi.TextSize(33, 39);
            line.fontSize = StationUi.TextSize(20, 24);
            detail.fontSize = StationUi.TextSize(17, 20);
            HandleKeyboard(Event.current);
            if (!IsOpen) return;
            var previous = GUI.matrix;
            Fill(new Rect(0, 0, Screen.width, Screen.height), Background);
            float scale = Mathf.Min(1.5f, Screen.width / (Width + 24), Screen.height / (Height + 24));
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - Width * scale) / 2,
                (Screen.height - Height * scale) / 2, 0), Quaternion.identity, new Vector3(scale, scale, 1));
            try
            {
                switch (page)
                {
                    case Page.Photograph: DrawPhotograph(); break;
                    case Page.Credits: DrawCredits(); break;
                    case Page.Menu: DrawMenu(); break;
                }
                if (IsOpen && !string.IsNullOrEmpty(status))
                    Write(new Rect(70, 531, 760, 35), status, detail, Accent);
            }
            finally { GUI.matrix = previous; }
        }

        private void DrawPhotograph()
        {
            if (DisplayedPhotograph != null)
            {
                Fill(new Rect(315, 25, 270, 326), Ink);
                GUI.DrawTexture(new Rect(324, 34, 252, 308), DisplayedPhotograph, ScaleMode.ScaleToFit, false);
            }
            StationTextCapture.Record("ending.final", FinalText);
            Write(new Rect(80, 370, 740, 60), FinalText, title, Ink);
            if (Button(new Rect(232, 472, 213, 42), "View credits", 0)) Credits();
            if (Button(new Rect(455, 472, 213, 42), "Skip to menu", 1)) SkipToMenu();
        }

        private void DrawCredits()
        {
            Write(new Rect(45, 86, 810, 71), "LAST KNOWN STATE: THE LEGACY", title, Ink);
            string team = station.Resolve(new StableId("credits.team"));
            string sources = station.Resolve(new StableId("credits.sources"));
            StationTextCapture.Record("credits.team", team);
            StationTextCapture.Record("credits.sources", sources);
            Write(new Rect(112, 204, 676, 82), team, line, Ink);
            Write(new Rect(112, 318, 676, 72), sources, detail, Muted);
            if (Button(new Rect(300, 462, 300, 44), "Return to menu", 0)) SkipToMenu();
        }

        private void DrawMenu()
        {
            const string gameTitle = "LAST KNOWN STATE: THE LEGACY";
            StationTextCapture.Record("menu.main", gameTitle);
            Write(new Rect(45, 62, 810, 66), gameTitle, title, Ink);
            if (Button(new Rect(275, 164, 350, 44), station.Resolve(new StableId("menu.continue")) + " saved game", 0)) Continue();
            if (Button(new Rect(275, 222, 350, 44), station.Resolve(new StableId("menu.new-game")), 1)) station.NewRun();
            if (Button(new Rect(275, 280, 350, 44), "Replay ending", 2, Completed)) Replay();
            bool openingPending = observed?.Narrative?.CaptureOpening() is OpeningProgress.Farewell or OpeningProgress.Arrival;
            if (Button(new Rect(275, 338, 350, 44), openingPending ? "Return to opening" : "Return to station", 3))
                ReturnToStation();
            if (Button(new Rect(275, 396, 350, 44), "Save progress", 4)) SaveProgress();
            if (Button(new Rect(275, 452, 350, 40),
                StationUi.LargeText ? "Text size: Large" : "Text size: Standard", 5))
                ToggleTextSize();
            Write(new Rect(70, 502, 760, 26), "Tab moves between controls. Enter selects.", detail, Muted);
        }

        internal void HandleKeyboard(Event input)
        {
            if (!CanControl || input.type != EventType.KeyDown) return;
            int count = page == Page.Menu ? 6 : page == Page.Photograph ? 2 : 1;
            if (input.keyCode == KeyCode.Tab)
            {
                int step = input.shift ? -1 : 1;
                do { focus = (focus + step + count) % count; }
                while (page == Page.Menu && focus == 2 && !Completed);
            }
            else if (input.keyCode == KeyCode.Return || input.keyCode == KeyCode.KeypadEnter)
            {
                if (page == Page.Photograph && focus == 0) Credits();
                else if (page == Page.Photograph || page == Page.Credits) SkipToMenu();
                else
                {
                    switch (focus)
                    {
                        case 0: Continue(); break;
                        case 1: station.NewRun(); break;
                        case 2: Replay(); break;
                        case 3: ReturnToStation(); break;
                        case 4: SaveProgress(); break;
                        case 5: ToggleTextSize(); break;
                    }
                }
            }
            else return;
            input.Use();
        }

        private static void ToggleTextSize() => StationUi.LargeText = !StationUi.LargeText;

        private bool Button(Rect rect, string label, int index, bool enabled = true)
        {
            if (focus == index && enabled)
                Fill(new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4), Accent);
            Fill(rect, enabled ? Control : Background);
            bool original = GUI.enabled;
            GUI.enabled = original && enabled;
            bool pressed = GUI.Button(rect, GUIContent.none, hitArea);
            GUI.enabled = original;
            Write(rect, label, line, enabled ? Ink : Muted);
            if (pressed) focus = index;
            return pressed;
        }

        private void EnsureStyles()
        {
            if (title != null) return;
            line = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20, wordWrap = true };
            title = new GUIStyle(line) { fontSize = 33, fontStyle = FontStyle.Bold };
            detail = new GUIStyle(line) { fontSize = 17 };
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
