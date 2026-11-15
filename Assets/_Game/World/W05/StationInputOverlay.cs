using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LKS.World
{
    // Temporary graybox views for testing input ownership before terminal UI is authored.
    [RequireComponent(typeof(StationFirstPerson))]
    internal sealed class StationInputOverlay : MonoBehaviour
    {
        private StationFirstPerson player;
        private IDisposable reading;
        private string view;
        private GUIStyle body, title, button, hint;
        private void OnEnable() => player = GetComponent<StationFirstPerson>();
        private void OnDisable() => CloseReading();

        private void OnGUI()
        {
            EnsureStyles();
            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1000f, Screen.height / 620f), .6f, 1.65f);
            var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            try
            {
                if (player.IsPaused) DrawPause(width, height);
                else if (view != null) DrawReading(width, height);
                else if (player.HasReadingFocus) return;
                else if (!player.HasWorldFocus)
                {
                    var rect = new Rect(width/2-180, height/2-24, 360, 48);
                    if (GUI.Button(rect, "Click to resume", button)) player.ResumePointer();
                }
                else
                {
                    GUI.Label(new Rect(width/2-12, height/2-16, 24, 32), "+", title);
                    GUI.Box(new Rect(14, height-51, 400, 38), GUIContent.none);
                    GUI.Label(new Rect(27,height-48,375,33),
                        "WASD / ARROWS  Move   E  Interact   ESC  Pause", hint);
                }
            }
            finally { GUI.matrix = previous; }
        }

        private void DrawPause(float width, float height)
        {
            float x = width/2-202;
            float y = height/2-206;
            GUI.Box(new Rect(x,y,404,412), GUIContent.none);
            GUI.Label(new Rect(x+19,y+15,366,36),"PAUSED",title);
            GUI.Label(new Rect(x+35,y+66,334,26), $"Mouse sensitivity  {player.Sensitivity:F2}", body);
            player.Sensitivity = GUI.HorizontalSlider(new Rect(x+36,y+95,330,22),player.Sensitivity,.035f,.4f);
            player.InvertY = GUI.Toggle(new Rect(x+35,y+125,334,27),player.InvertY,"Invert vertical look",body);
            if (GUI.Button(new Rect(x+35,y+175,334,34), player.HasReadingFocus ? "Return to reading" : "Resume", button))
                player.SetPaused(false);
            if (!player.HasReadingFocus && GUI.Button(new Rect(x+35,y+219,334,38),"Terminal preview",button))
                OpenReading("TERMINAL");
            if (GUI.Button(new Rect(x+35,y+283,334,37),"Restart station",button))
                player.RequestRestart();
            if (GUI.Button(new Rect(x+35,y+333,334,37),"Reload station",button))
                ReloadStation();
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
            float x = width/2-219;
            float y = height/2-129;
            GUI.Box(new Rect(x,y,438,258), GUIContent.none);
            GUI.Label(new Rect(x+20,y+18,398,44),view,title);
            string message = view == "TERMINAL"
                ? "Station terminal preview. You can read this window without moving the camera."
                : "Archive preview. Mission records will appear here when the archive is connected.";
            GUI.Label(new Rect(x+30,y+88,378,80),message,body);
            if (GUI.Button(new Rect(x+70,y+184,298,40),"Close",button)) CloseReading();
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
            body = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true };
            title = new GUIStyle(body) { fontSize = 25, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            button = new GUIStyle(GUI.skin.button) { fontSize = 16 };
            hint = new GUIStyle(body) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
        }
    }
}
