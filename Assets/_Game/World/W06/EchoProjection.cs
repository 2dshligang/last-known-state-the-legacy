using System;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LKS.World
{
    internal sealed class EchoProjection : IDisposable
    {
        private const int ProjectionLayer = 31;
        private static readonly int Tint = Shader.PropertyToID("_Tint");
        private static readonly Color Dark = new Color32(36, 52, 63, 255);
        private static readonly Color Outline = new Color32(57, 81, 94, 255);
        private static readonly Color Recorded = new Color32(105, 222, 217, 255);
        private static readonly Color Estimated = new Color32(254, 198, 111, 255);

        private readonly GameObject root;
        private readonly GameObject set;
        private readonly Camera camera;
        private readonly Material material;
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private readonly Renderer console, controller, backup;
        private readonly GameObject estimate;
        private StableId presentedCue;

        internal RenderTexture Texture { get; }
        internal bool ShowsReconstruction => set.activeInHierarchy;
        internal int ObjectCount => root.GetComponentsInChildren<Transform>(true).Length;

        internal EchoProjection(Transform owner)
        {
            var shader = Resources.Load<Shader>("W10Echo/Projection");
            if (shader == null) throw new InvalidOperationException("The W10 ECHO projection shader is missing.");
            material = new Material(shader) { name = "ECHO projection material" };
            root = new GameObject("ECHO replay projection");
            root.transform.SetParent(owner, false);
            root.transform.position = Vector3.down * 2048;
            set = Group("Instrument silhouettes", root.transform);
            Box("Projection deck", set.transform, new Vector3(0,-.10f,0), new Vector3(9,.14f,5), Dark);
            for (int i = -4; i <= 4; i++)
                Box("Deck marking", set.transform, new Vector3(i,-.024f,0), new Vector3(.021f,.01f,5), Outline);
            for (int i = -2; i <= 2; i++)
                Box("Deck marking", set.transform, new Vector3(0,-.022f,i), new Vector3(9,.013f,.021f), Outline);
            console = Instrument("Power monitor", -2.6f, .98f);
            controller = Instrument("Controller rack", 0, 1.40f);
            backup = Instrument("Backup pack", 2.6f, .78f);
            estimate = Group("Model estimate outline", set.transform);
            for (int i = 0; i < 5; i++)
            {
                Box("Estimated boundary", estimate.transform, new Vector3(2 + .30f * i,1.15f,-.4f),
                    new Vector3(.15f,.065f,.075f), Estimated);
                Box("Estimated boundary", estimate.transform, new Vector3(2 + .30f * i,1.15f,.4f),
                    new Vector3(.15f,.065f,.075f), Estimated);
            }

            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                Texture = new RenderTexture(640, 360, 24, RenderTextureFormat.ARGB32) { name = "ECHO viewport" };
            var view = Group("Projection camera", root.transform);
            view.transform.localPosition = new Vector3(4.1f, 4.4f, -8.2f);
            view.transform.LookAt(root.transform.TransformPoint(new Vector3(0,.50f,0)));
            camera = view.AddComponent<Camera>();
            camera.targetTexture = Texture;
            camera.depth = -20;
            camera.orthographic = true;
            camera.orthographicSize = 3.15f;
            camera.cullingMask = 1 << ProjectionLayer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(11, 22, 30, 255);
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 30;
            camera.allowHDR = false;
            Hide();
        }

        internal void Present(EchoFrame frame)
        {
            if (frame == null || frame.Evidence.Quality == EvidenceQuality.Missing)
            {
                Hide();
                return;
            }
            if (root.activeSelf && presentedCue == frame.VisualCue) return;
            presentedCue = frame.VisualCue;
            string cue = frame.VisualCue.value;
            Paint(console, cue == "cue.echo-console" ? Recorded : Outline);
            Paint(controller, cue == "cue.echo-controller" ? Recorded : Outline);
            Paint(backup, cue == "cue.echo-outage" ? Outline : Dark);
            estimate.SetActive(frame.Evidence.Quality == EvidenceQuality.Estimated);
            root.SetActive(true);
            camera.enabled = Texture != null;
        }

        internal void Hide()
        {
            presentedCue = default;
            camera.enabled = false;
            root.SetActive(false);
        }

        private Renderer Instrument(string name, float x, float height)
        {
            var actor = Group(name, set.transform);
            actor.transform.localPosition = Vector3.right * x;
            Box("Housing", actor.transform, new Vector3(0,height/2,0), new Vector3(1.58f,height,.83f), Outline);
            Box("Instrument face", actor.transform, new Vector3(0,height*.57f,-.427f),
                new Vector3(1.28f,height*.45f,.045f), Dark);
            return Box("Signal panel", actor.transform, new Vector3(0,height*.62f,-.455f),
                new Vector3(.99f,.17f,.028f), Outline);
        }

        private static GameObject Group(string name, Transform parent)
        {
            var item = new GameObject(name) { layer = ProjectionLayer };
            item.transform.SetParent(parent, false);
            return item;
        }

        private Renderer Box(string name, Transform parent, Vector3 position, Vector3 size, Color color)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name;
            item.layer = ProjectionLayer;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = size;
            var collider = item.GetComponent<Collider>();
            collider.enabled = false;
            Object.Destroy(collider);
            var renderer = item.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Paint(renderer, color);
            return renderer;
        }

        private void Paint(Renderer renderer, Color color)
        {
            properties.SetColor(Tint, color);
            renderer.SetPropertyBlock(properties);
        }

        public void Dispose()
        {
            camera.targetTexture = null;
            Object.Destroy(root);
            if (Texture != null)
            {
                Texture.Release();
                Object.Destroy(Texture);
            }
            Object.Destroy(material);
        }
    }
}
