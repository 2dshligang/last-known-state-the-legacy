using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LKS.World
{
    [RequireComponent(typeof(StationFirstPerson))]
    internal sealed class StationInputOverlay : MonoBehaviour
    {
        private static readonly Color Panel = new Color32(16, 25, 32, 248);
        private readonly StationKeyboardFocus focus = new();
        private StationFirstPerson player;
        private IDisposable reading;
        private string view;
        private bool wasPaused;
        private GUIStyle body, title, button, hint;
        private void OnEnable() => player = GetComponent<StationFirstPerson>();
        private void OnDisable() => CloseReading();

        private void OnGUI()
        {
            EnsureStyles();
            float scale = StationUi.HudScale(Mathf.Clamp(Mathf.Min(Screen.width / 1000f,
                Screen.height / 620f), .6f, 1.65f), 520, 520);
            var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            try
            {
                if (player.IsPaused != wasPaused)
                {
                    wasPaused = player.IsPaused;
                    focus.Reset();
                }
                if (player.IsPaused) DrawPause(width, height);
                else if (view != null) DrawReading(width, height);
                else if (player.HasReadingFocus) return;
                else if (!player.HasWorldFocus)
                {
                    var rect = new Rect(width / 2 - 180, height / 2 - 24, 360, 48);
                    var input = Event.current;
                    if (input.type == EventType.KeyDown && input.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
                    {
                        input.Use();
                        player.ResumePointer();
                    }
                    else if (GUI.Button(rect, "Resume  [Enter]", button)) player.ResumePointer();
                }
                else
                {
                    GUI.Label(new Rect(width / 2 - 12, height / 2 - 16, 24, 32), "+", title);
                    var controls = new Rect(14, height - 51, 400, 38);
                    StationUi.Fill(controls, Panel);
                    GUI.Label(new Rect(27, height - 48, 375, 33),
                        "WASD / ARROWS  Move   E  Interact   ESC  Pause", hint);
                }
            }
            finally { GUI.matrix = previous; }
        }

        private void DrawPause(float width, float height)
        {
            var input = Event.current;
            if (focus.Handle(input)) return;
            if (focus.IsSelected("sensitivity") && input.type == EventType.KeyDown &&
                input.keyCode is KeyCode.LeftArrow or KeyCode.RightArrow)
            {
                player.Sensitivity = Mathf.Clamp(player.Sensitivity +
                    (input.keyCode == KeyCode.LeftArrow ? -.015f : .015f), .035f, .4f);
                input.Use();
            }
            focus.Begin();
            float x = width / 2 - 224;
            float y = height / 2 - 215;
            StationUi.Fill(new Rect(x, y, 448, 430), Panel);
            GUI.Label(new Rect(x + 19, y + 13, 410, 35), "Paused", title);
            GUI.Label(new Rect(x + 34, y + 62, 380, 28), $"Mouse sensitivity  {player.Sensitivity.ToString("F2", CultureInfo.InvariantCulture)}", body);
            var slider = new Rect(x + 35, y + 93, 378, 21);
            player.Sensitivity = GUI.HorizontalSlider(slider, player.Sensitivity, .035f, .4f);
            focus.Register(slider, "sensitivity", null);
            focus.Button(new Rect(x + 34, y + 126, 380, 35),
                player.InvertY ? "Invert vertical look: On" : "Invert vertical look: Off",
                "invert", button, () => player.InvertY = !player.InvertY);
            focus.Button(new Rect(x + 34, y + 168, 380, 35),
                StationUi.LargeText ? "Text size: Large" : "Text size: Standard", "text", button,
                () => StationUi.LargeText = !StationUi.LargeText);
            if (focus.Button(new Rect(x + 34, y + 226, 380, 36),
                player.HasReadingFocus ? "Return to reading" : "Resume", "resume", button,
                () => player.SetPaused(false))) return;
            if (!player.HasReadingFocus && player.HasGameMenu &&
                focus.Button(new Rect(x + 34, y + 269, 380, 36), "Game menu", "menu", button,
                    player.RequestGameMenu)) return;
            if (focus.Button(new Rect(x + 34, y + 312, 380, 36), "Restart station", "restart", button,
                player.RequestRestart)) return;
            GUI.Label(new Rect(x + 34, y + 367, 380, 43),
                "Tab moves between controls. Enter selects.\nLeft and Right adjust the sensitivity slider.", hint);
        }

        internal AsyncOperation ReloadStation()
        {
            var scene = gameObject.scene;
#if UNITY_EDITOR
            if (scene.buildIndex < 0)
                return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                    scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            return SceneManager.LoadSceneAsync(scene.buildIndex);
        }

        private void DrawReading(float width, float height)
        {
            float x = width / 2 - 219;
            float y = height / 2 - 129;
            StationUi.Fill(new Rect(x, y, 438, 258), Panel);
            GUI.Label(new Rect(x + 20, y + 18, 398, 44), view, title);
            if (GUI.Button(new Rect(x + 70, y + 184, 298, 40), "Close", button)) CloseReading();
        }

        internal void OpenReading(string name)
        {
            CloseReading();
            view = name;
            reading = player.AcquireReadingFocus(() => { reading = null; view = null; });
            player.SetPaused(false);
        }

        internal void CloseReading()
        {
            view = null;
            reading?.Dispose();
            reading = null;
        }

        private void EnsureStyles()
        {
            if (body != null) return;
            body = new GUIStyle(GUI.skin.label)
                { fontSize = 18, wordWrap = true, normal = { textColor = Color.white } };
            title = new GUIStyle(body) { fontSize = 27, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            button = new GUIStyle(GUI.skin.button) { fontSize = 18 };
            hint = new GUIStyle(body) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
        }
    }
}
