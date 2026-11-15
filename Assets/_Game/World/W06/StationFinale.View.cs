using UnityEngine;

namespace LKS.World
{
    public sealed partial class StationFinale
    {
        private const float Width = 900;
        private const float Height = 580;
        private static readonly Color Background = StationUi.Panel;
        private static readonly Color Ink = StationUi.Ink;
        private static readonly Color Muted = StationUi.Muted;
        private static readonly Color Accent = StationUi.Accent;
        private GUIStyle title, line, detail, creditLine;
        private Vector2 creditScroll;

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
                    case Page.Settings: DrawSettings(); break;
                    case Page.ConfirmNew: DrawNewGameConfirmation(); break;
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
            if (Button(new Rect(232, 472, 213, 42), "View credits", 0, primary: true)) Credits();
            if (Button(new Rect(455, 472, 213, 42), "Skip to menu", 1)) SkipToMenu();
        }

        private void DrawCredits()
        {
            Write(new Rect(42, 24, 816, 59), "Credits and sources", title, Ink);
            string team = station.Resolve(new StableId("credits.team"));
            string sources = station.Resolve(new StableId("credits.sources"));
            StationTextCapture.Record("credits.team", team);
            StationTextCapture.Record("credits.sources", sources);
            Write(new Rect(50, 92, 800, 44), team, line, Ink);
            Write(new Rect(50, 139, 800, 41), sources, detail, Muted);
            creditLine.fontSize = StationUi.TextSize(17, 21);
            Fill(new Rect(62, 187, 776, 277), StationUi.Surface);
            var entries = StationSourceCredits.All;
            creditScroll = GUI.BeginScrollView(new Rect(74, 199, 748, 252), creditScroll,
                new Rect(0, 0, 714, entries.Count * 51));
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                Write(new Rect(4, i * 51, 701, 46), entry.Attribution, creditLine, Ink);
                StationTextCapture.Record("credits.source." + entry.Id, entry.Attribution);
            }
            GUI.EndScrollView();
            if (Button(new Rect(300, 477, 300, 44), "Return to menu", 0, primary: true)) SkipToMenu();
        }

        private void DrawMenu()
        {
            const string gameTitle = "LAST KNOWN STATE: THE LEGACY";
            StationTextCapture.Record("menu.main", gameTitle);
            StationUi.Frame(new Rect(0, 0, Width, Height), Accent);
            Fill(new Rect(20, 26, 380, 510), StationUi.Surface);
            Write(new Rect(40, 51, 340, 38), "ASTERION  /  CAMPAIGN", detail, Accent);
            Write(new Rect(48, 100, 325, 160), "LAST KNOWN STATE\nTHE LEGACY", title, Ink);
            if (photograph != null)
                GUI.DrawTexture(new Rect(143, 277, 134, 166), photograph, ScaleMode.ScaleToFit, false);

            for (int index = 0; index < 7; index++)
            {
                int current = index;
                var rect = new Rect(426, 33 + index * 65, 428, 48);
                if (Button(rect, MenuLabel(index), index, MenuEnabled(index), index == (confirmedEmptySlot ? 1 : 0)))
                {
                    MenuAction(current);
                    return;
                }
            }
            Write(new Rect(426, 493, 428, 26), "Tab: move   |   Enter: select", detail, Muted);
        }

        private string MenuLabel(int index) => index switch
        {
            0 => confirmedEmptySlot ? "Continue  /  No save found" : "Continue",
            1 => Resolve("menu.new-game"),
            2 => observed?.Narrative?.CaptureOpening() is OpeningProgress.Farewell or OpeningProgress.Arrival
                ? "Return to opening" : "Return to station",
            3 => "Save progress",
            4 => "Settings",
            5 => Completed ? "Replay ending" : "Replay ending  /  Finish the story",
            6 => "Quit game",
            _ => ""
        };

        private bool MenuEnabled(int index) => index switch
        {
            0 => !confirmedEmptySlot,
            5 => Completed,
            _ => true
        };

        private void MenuAction(int index)
        {
            switch (index)
            {
                case 0: Continue(); break;
                case 1: Show(Page.ConfirmNew); break;
                case 2: ReturnToStation(); break;
                case 3: SaveProgress(); break;
                case 4: Show(Page.Settings); break;
                case 5: Replay(); break;
                case 6: Application.Quit(); break;
            }
        }

        private void DrawSettings()
        {
            StationUi.Frame(new Rect(98, 36, 704, 469), Accent);
            Write(new Rect(110, 53, 680, 70), "Settings", title, Ink);
            var slider = new Rect(201, 176, 495, 24);
            Write(new Rect(131, 132, 630, 43),
                $"Mouse sensitivity  {player.Sensitivity.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}", line, Ink);
            if (focus == 0) StationUi.Outline(slider);
            player.Sensitivity = GUI.HorizontalSlider(slider, player.Sensitivity, .035f, .4f);
            if (Button(new Rect(198, 224, 504, 48), player.InvertY ? "Invert vertical look: On" : "Invert vertical look: Off", 1))
                player.InvertY = !player.InvertY;
            if (Button(new Rect(198, 282, 504, 48), StationUi.LargeText ? "Text size: Large" : "Text size: Standard", 2))
                StationUi.LargeText = !StationUi.LargeText;
            if (Button(new Rect(198, 364, 504, 48), "Back to game menu", 3, primary: true)) Show(Page.Menu);
            Write(new Rect(131, 420, 630, 56), "Left / Right: adjust sensitivity    |    Settings save automatically.", detail, Muted);
        }

        private void DrawNewGameConfirmation()
        {
            StationUi.Frame(new Rect(103, 101, 694, 357), Accent);
            Write(new Rect(128, 119, 644, 70), "Start a new game?", title, Ink);
            Write(new Rect(149, 196, 602, 114),
                "Unsaved progress in this session will be lost. Your saved game stays available until you save again.", line, Muted);
            if (Button(new Rect(140, 345, 280, 52), "Keep playing", 0, primary: true)) Show(Page.Menu);
            else if (Button(new Rect(480, 345, 280, 52), "Start new game", 1)) station.NewRunFromMenu();
        }

        internal void HandleKeyboard(Event input)
        {
            if (!CanControl || input.type != EventType.KeyDown) return;
            int count = page switch { Page.Menu => 7, Page.Settings => 4, Page.Photograph or Page.ConfirmNew => 2, _ => 1 };
            if (page == Page.Credits && input.keyCode is KeyCode.PageUp or KeyCode.PageDown)
                creditScroll.y = Mathf.Clamp(creditScroll.y + (input.keyCode == KeyCode.PageUp ? -220 : 220),
                    0, Mathf.Max(0, StationSourceCredits.All.Count * 51 - 252));
            else if (page == Page.Settings && focus == 0 && input.keyCode is KeyCode.LeftArrow or KeyCode.RightArrow)
                player.Sensitivity += input.keyCode == KeyCode.LeftArrow ? -.015f : .015f;
            else if (input.keyCode == KeyCode.Tab)
            {
                int step = input.shift ? -1 : 1;
                do { focus = (focus + step + count) % count; }
                while (page == Page.Menu && !MenuEnabled(focus));
            }
            else if (input.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
            {
                if (page == Page.Photograph && focus == 0) Credits();
                else if (page is Page.Photograph or Page.Credits) SkipToMenu();
                else if (page == Page.Menu && MenuEnabled(focus)) MenuAction(focus);
                else if (page == Page.ConfirmNew)
                {
                    if (focus == 0) Show(Page.Menu);
                    else station.NewRunFromMenu();
                }
                else if (page == Page.Settings)
                {
                    if (focus == 1) player.InvertY = !player.InvertY;
                    else if (focus == 2) StationUi.LargeText = !StationUi.LargeText;
                    else if (focus == 3) Show(Page.Menu);
                }
            }
            else return;
            input.Use();
        }

        private bool Button(Rect rect, string label, int index, bool enabled = true, bool primary = false)
        {
            bool pressed = StationUi.DrawButton(rect, label, line, enabled, focus == index, primary);
            if (pressed) focus = index;
            return pressed;
        }

        private void EnsureStyles()
        {
            if (title != null) return;
            line = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20, wordWrap = true };
            title = new GUIStyle(line) { fontSize = 33, fontStyle = FontStyle.Bold };
            detail = new GUIStyle(line) { fontSize = 17 };
            creditLine = new GUIStyle(detail) { alignment = TextAnchor.MiddleLeft };
        }

        private static void Write(Rect rect, string text, GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            GUI.Label(rect, text, style);
        }

        private static void Fill(Rect rect, Color color) => StationUi.Fill(rect, color);
    }
}
