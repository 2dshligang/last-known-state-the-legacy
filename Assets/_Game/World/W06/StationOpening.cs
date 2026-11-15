using System;
using UnityEngine;

namespace LKS.World
{
    // The two illustrated shots leave the first-person camera, pose, and lens untouched.
    [DisallowMultipleComponent]
    public sealed class StationOpening : MonoBehaviour, INarrativeView
    {
        private static readonly StableId FarewellPortrait = new StableId("portrait.dmitry");
        private static readonly StableId FarewellCamera = new StableId("camera.farewell");
        private static readonly StableId ArrivalPortrait = new StableId("portrait.alexei");
        private static readonly StableId ArrivalCamera = new StableId("camera.station-arrival");
        private StationFirstPerson player;
        private StationInteraction bridge;
        private GameplaySession session;
        private NarrativeCue presented;
        private IDisposable input;
        private Texture2D farewell, arrival, photo;
        private Material photoMaterial;
        private GameObject photoCard;
        private string passage;
        private bool isFarewell;
        private bool titlePending = true;
        private int titleFocus;
        private float shotTime;
        private GUIStyle title, body, hint, button, primaryLabel, launchTitle;

        internal bool IsOpen => input != null;
        internal string Passage => passage;
        internal Texture2D Photograph => photo;

        internal void Bind(StationFirstPerson owner, StationInteraction interaction,
            StationInteractionAnchor photographAnchor, Material stationMaterial)
        {
            player = owner;
            bridge = interaction;
            farewell = Resources.Load<Texture2D>("W14/Farewell");
            arrival = Resources.Load<Texture2D>("W14/Arrival");
            photo = Resources.Load<Texture2D>("W14/DmitryPhoto");

            if (photo == null || photographAnchor == null || stationMaterial == null) return;
            var anchor = photographAnchor.transform;
            photoCard = GameObject.CreatePrimitive(PrimitiveType.Quad);
            photoCard.name = "Dmitry's photograph";
            Destroy(photoCard.GetComponent<Collider>());
            photoCard.transform.SetParent(anchor, true);
            var facing = Vector3.ProjectOnPlane(anchor.position - photographAnchor.Approach.position, Vector3.up);
            photoCard.transform.SetPositionAndRotation(anchor.position + Vector3.up * .67f,
                Quaternion.LookRotation(facing));
            photoCard.transform.localScale = new Vector3(.23f, .274f, 1);
            photoMaterial = new Material(stationMaterial);
            photoMaterial.mainTexture = photo;
            photoMaterial.color = Color.white;
            photoCard.GetComponent<MeshRenderer>().sharedMaterial = photoMaterial;
        }

        internal void Sync()
        {
            if (bridge == null || !bridge.isActiveAndEnabled) return;
            var current = bridge.Session;
            if (!ReferenceEquals(session, current)) Close();
            if (current?.Narrative == null || !current.Narrative.TryGetOpening(out var cue))
            {
                if (IsOpen) Close();
                return;
            }

            if (!IsOpen)
            {
                if (!player.isActiveAndEnabled || player.HasReadingFocus || player.IsPaused) return;
                session = current;
                input = player.AcquireReadingFocus(Close);
            }
            if (ReferenceEquals(presented, cue)) return;
            presented = cue;
            cue.Present(this, current.Archive.Content);
        }

        private void Update()
        {
            Sync();
            if (CanAct && !titlePending) shotTime += Time.unscaledDeltaTime;
        }

        private bool CanAct => IsOpen && !player.IsPaused && player.HasApplicationFocus &&
            ReferenceEquals(session, bridge.Session);

        internal bool Continue() => Act(skip: false);
        internal bool Skip() => Act(skip: true);
        internal void DismissLaunchTitle() => titlePending = false;

        internal bool OpenGameMenu()
        {
            if (!CanAct || !bridge.HasGameMenu) return false;
            Close();
            bridge.OpenGameMenu();
            return true;
        }

        private bool Act(bool skip)
        {
            if (!CanAct) return false;
            titlePending = false;
            bool changed = skip ? session.Narrative.SkipOpening() : session.Narrative.AdvanceOpening();
            Sync();
            if (changed && !IsOpen && !player.HasWorldFocus) player.ResumePointer();
            return changed;
        }

        public void ShowText(string englishText, StableId portrait, StableId cameraCue)
        {
            passage = englishText;
            if (portrait == FarewellPortrait && cameraCue == FarewellCamera) isFarewell = true;
            else if (portrait == ArrivalPortrait && cameraCue == ArrivalCamera) isFarewell = false;
            else Debug.LogWarning($"Unbound station opening shot: {portrait} / {cameraCue}");
            shotTime = 0;
        }

        internal void Close()
        {
            var lease = input;
            input = null;
            presented = null;
            session = null;
            passage = null;
            lease?.Dispose();
        }

        private void OnDisable() => Close();

        private void OnDestroy()
        {
            if (photoCard != null) Destroy(photoCard);
            if (photoMaterial != null) Destroy(photoMaterial);
        }

        private void OnGUI()
        {
            if (!IsOpen || player.IsPaused || passage == null) return;
            if (titlePending && bridge.HasGameMenu)
            {
                DrawLaunchMenu();
                return;
            }
            if (CanAct && Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
                {
                    Event.current.Use();
                    Continue();
                    return;
                }
                if (Event.current.keyCode == KeyCode.Space)
                {
                    Event.current.Use();
                    Skip();
                    return;
                }
                if (Event.current.keyCode == KeyCode.T)
                {
                    Event.current.Use();
                    StationUi.LargeText = !StationUi.LargeText;
                    return;
                }
                if (Event.current.keyCode == KeyCode.M && bridge.HasGameMenu)
                {
                    Event.current.Use();
                    OpenGameMenu();
                    return;
                }
            }

            title ??= new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold };
            body ??= new GUIStyle(GUI.skin.label) { wordWrap = true };
            body.fontSize = StationUi.TextSize(23, 28);
            hint ??= new GUIStyle(GUI.skin.label) { fontSize = 16 };
            button ??= StationUi.ButtonStyle(19, wrap: true);
            primaryLabel ??= new GUIStyle(hint) { fontSize = 19, alignment = TextAnchor.MiddleCenter };
            Fill(new Rect(0, 0, Screen.width, Screen.height), StationUi.Panel);
            const float width = 940, height = 650;
            float scale = Mathf.Min(Screen.width / (width + 24), Screen.height / (height + 20));
            var previous = GUI.matrix;
            bool wasEnabled = GUI.enabled;
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - width * scale) * .5f,
                (Screen.height - height * scale) * .5f, 0), Quaternion.identity, new Vector3(scale, scale, 1));
            try
            {
                GUI.Label(new Rect(0, 0, width, 27), isFarewell ? "01  /  EARTH" : "02  /  MARS", hint);
                if (GUI.Button(new Rect(692, 0, 248, 28),
                    StationUi.LargeText ? "Large text  [T]" : "Standard text  [T]", button))
                    StationUi.LargeText = !StationUi.LargeText;
                StationUi.Frame(new Rect(0, 30, 940, 384), StationUi.Amber);
                DrawShot();
                GUI.Label(new Rect(0, 424, width, 48), isFarewell ? "Farewell" : "Arrival at ASTERION", title);
                StationTextCapture.Record(isFarewell ? "narrative.farewell" : "narrative.arrival", passage);
                GUI.Label(new Rect(0, 474, width, 116), passage, body);
                GUI.enabled = wasEnabled && CanAct;
                if (GUI.Button(new Rect(0, 598, 242, 43), bridge.Resolve(new StableId("narrative.action.skip")) + "  [Space]", button)) Skip();
                else if (bridge.HasGameMenu && GUI.Button(new Rect(349, 598, 242, 43), "Game menu  [M]", button)) OpenGameMenu();
                else if (StationUi.DrawButton(new Rect(682, 598, 258, 43),
                    isFarewell ? bridge.Resolve(new StableId("narrative.action.continue")) + "  [Enter]" : "Enter station  [Enter]", primaryLabel, primary: true)) Continue();
            }
            finally
            {
                GUI.enabled = wasEnabled;
                GUI.matrix = previous;
            }
        }

        private void DrawLaunchMenu()
        {
            if (CanAct && Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.Tab)
                {
                    titleFocus = (titleFocus + (Event.current.shift ? 3 : 1)) % 4;
                    Event.current.Use();
                }
                else if (Event.current.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
                {
                    Event.current.Use();
                    LaunchAction(titleFocus);
                    return;
                }
            }

            const float width = 900, height = 510;
            Fill(new Rect(0, 0, Screen.width, Screen.height), StationUi.Panel);
            float scale = Mathf.Min(1.5f, Screen.width / (width + 24), Screen.height / (height + 24));
            var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - width * scale) / 2,
                (Screen.height - height * scale) / 2, 0), Quaternion.identity, new Vector3(scale, scale, 1));
            try
            {
                launchTitle ??= new GUIStyle(GUI.skin.label)
                {
                    fontSize = 37,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = StationUi.Ink }
                };
                primaryLabel ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
                primaryLabel.fontSize = StationUi.TextSize(20, 24);
                StationUi.Frame(new Rect(0, 0, width, height), StationUi.Accent);
                Fill(new Rect(24, 24, 377, 462), StationUi.Surface);
                const string gameTitle = "LAST KNOWN STATE: THE LEGACY";
                StationTextCapture.Record("menu.main", gameTitle);
                GUI.Label(new Rect(32, 48, 361, 140), "LAST KNOWN STATE\nTHE LEGACY", launchTitle);
                if (arrival != null)
                    GUI.DrawTexture(new Rect(44, 223, 337, 191), arrival, ScaleMode.ScaleToFit, false);
                var first = new Rect(431, 88, 423, 54);
                if (StationUi.DrawButton(first, bridge.Resolve(new StableId("menu.new-game")), primaryLabel,
                    CanAct, titleFocus == 0, primary: true)) LaunchAction(0);
                else if (StationUi.DrawButton(new Rect(431, 156, 423, 54), "Continue", primaryLabel,
                    CanAct, titleFocus == 1)) LaunchAction(1);
                else if (StationUi.DrawButton(new Rect(431, 224, 423, 54), "Settings", primaryLabel,
                    CanAct, titleFocus == 2)) LaunchAction(2);
                else if (StationUi.DrawButton(new Rect(431, 292, 423, 54), "Quit game", primaryLabel,
                    CanAct, titleFocus == 3)) LaunchAction(3);
                GUI.Label(new Rect(431, 375, 423, 48), "Tab: move  |  Enter: select", primaryLabel);
            }
            finally { GUI.matrix = previous; }
        }

        private void LaunchAction(int index)
        {
            switch (index)
            {
                case 0:
                    titlePending = false;
                    break;
                case 1:
                    Close();
                    if (bridge.ContinueFromLaunch()) DismissLaunchTitle();
                    break;
                case 2:
                    Close();
                    bridge.SettingsFromLaunch();
                    break;
                case 3:
                    Application.Quit();
                    break;
            }
        }

        private void DrawShot()
        {
            var shot = new Rect(0, 34, 940, 376);
            var image = isFarewell ? farewell : arrival;
            if (image != null)
            {
                // Crop a little of the top and bottom, keeping the hands and window in frame.
                GUI.DrawTextureWithTexCoords(shot, image, new Rect(0, .10f, 1, .80f));
            }
            else Fill(shot, new Color32(34, 49, 57, 255));

            if (photo == null) return;
            float ease = Mathf.SmoothStep(0, 1, Mathf.Clamp01(shotTime / 1.5f));
            float x = isFarewell ? Mathf.Lerp(496, 528, ease) : Mathf.Lerp(405, 410, ease);
            float y = isFarewell ? Mathf.Lerp(250, 259, ease) : Mathf.Lerp(260, 237, ease);
            var rect = new Rect(x, y, 47, 56);
            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Lerp(-10f, isFarewell ? 10f : -3f, ease), rect.center);
            Fill(new Rect(rect.x + 2, rect.y + 3, rect.width, rect.height), new Color(0, 0, 0, .45f));
            GUI.DrawTexture(rect, photo);
            GUI.matrix = matrix;
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
