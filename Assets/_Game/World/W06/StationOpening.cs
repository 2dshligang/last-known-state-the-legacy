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
        private float shotTime;
        private GUIStyle title, body, hint, button;

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
            if (CanAct) shotTime += Time.unscaledDeltaTime;
        }

        private bool CanAct => IsOpen && !player.IsPaused && player.HasApplicationFocus &&
            ReferenceEquals(session, bridge.Session);

        internal bool Continue() => Act(skip: false);
        internal bool Skip() => Act(skip: true);

        private bool Act(bool skip)
        {
            if (!CanAct) return false;
            bool changed = skip ? session.Narrative.SkipOpening() : session.Narrative.AdvanceOpening();
            Sync();
            if (changed && !IsOpen) player.ResumePointer();
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
            }

            title ??= new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold };
            body ??= new GUIStyle(GUI.skin.label) { fontSize = 23, wordWrap = true };
            hint ??= new GUIStyle(GUI.skin.label) { fontSize = 16 };
            button ??= new GUIStyle(GUI.skin.button) { fontSize = 19 };
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color32(10, 15, 20, 255));
            const float width = 940, height = 650;
            float scale = Mathf.Min(Screen.width / (width + 24), Screen.height / (height + 20));
            var previous = GUI.matrix;
            bool wasEnabled = GUI.enabled;
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - width * scale) * .5f,
                (Screen.height - height * scale) * .5f, 0), Quaternion.identity, new Vector3(scale, scale, 1));
            try
            {
                GUI.Label(new Rect(0, 0, width, 27), isFarewell ? "01  /  EARTH" : "02  /  MARS", hint);
                DrawShot();
                GUI.Label(new Rect(0, 424, width, 48), isFarewell ? "Farewell" : "Arrival at ASTERION", title);
                GUI.Label(new Rect(0, 474, width, 112), passage, body);
                GUI.enabled = wasEnabled && CanAct;
                if (GUI.Button(new Rect(0, 598, 188, 43), "Skip opening  [Space]", button)) Skip();
                else if (GUI.Button(new Rect(682, 598, 258, 43),
                    isFarewell ? "Continue  [Enter]" : "Enter station  [Enter]", button)) Continue();
            }
            finally
            {
                GUI.enabled = wasEnabled;
                GUI.matrix = previous;
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
