using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using LKS.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LKS.Editor.World
{
    public static class W03Verification
    {
        private const float Tolerance = .001f;

        public static void Verify()
        {
            Require(Application.isBatchMode, "Verify W03 in batch mode on a clean checkout.");
            var before = Snapshot();
            W03StationKit.Generate();
            W03StationKit.Generate();
            Require(before.SequenceEqual(Snapshot()), "Repeat generation rewrote or duplicated kit assets.");

            var scene = Open();
            var samples = scene.GetRootGameObjects().Single(item => item.name == "W03 Samples").transform;
            var corridorA = Module(samples, "Corridor A");
            var corridorB = Module(samples, "Corridor B");
            var airlock = Module(samples, "Airlock");
            var echo = Module(samples, "ECHO Terminal");
            Require(samples.childCount == 7, "Expected two bays and five other modules.");
            var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { W03StationKit.Prefabs });
            Require(prefabs.Length == 6, "Expected six module prefabs.");
            StableId.ValidateUnique(prefabs.Select(guid => AssetDatabase.LoadAssetAtPath<GameObject>(
                AssetDatabase.GUIDToAssetPath(guid)).GetComponent<StationModule>().ModuleId));
            foreach (var module in samples.GetComponentsInChildren<StationModule>())
            {
                Require(module.ModuleId.IsValid && module.InspectPoint != null &&
                    module.SnapStart != null && module.SnapEnd != null, "Module anchors or ID missing.");
                Require(module.GetComponentsInChildren<Collider>(true).All(
                    collider => collider is BoxCollider && collider.enabled &&
                    !collider.isTrigger && collider.gameObject.layer == 0),
                    "Kit colliders must be solid Default-layer boxes.");
            }
            RequireNear(corridorA.SnapEnd.position, corridorB.SnapStart.position, "Bay snap points");
            RequireNear(corridorB.SnapStart.position, airlock.SnapStart.position, "Airlock snap point");

            CompareVariants(airlock, echo);
            CompareImportedPartBounds(airlock, echo);
            var presentation = airlock.GetComponent<AirlockPresentation>();
            var leaf = airlock.transform.Find("SlidingLeaf");
            var movingCollider = leaf.GetComponentInChildren<BoxCollider>();
            var label = airlock.GetComponentInChildren<TextMesh>();
            presentation.ApplyOpen(false);
            Physics.SyncTransforms();
            Require(label.text == "AIRLOCK  /  CLOSED", "Closed text did not follow the leaf.");
            Require(Physics.Raycast(new Vector3(-.35f,1,3), Vector3.forward, out var hit, 2) &&
                hit.collider == movingCollider, "Closed leaf did not block the passage.");
            presentation.ApplyOpen(true);
            Physics.SyncTransforms();
            Require(label.text == "AIRLOCK  /  OPEN", "Open text did not follow the leaf.");

            float left = -1.05f;
            float right = .35f;
            Require(Physics.Raycast(new Vector3(-.35f,1.1f,4), Vector3.left, out var leftHit, 1.1f) &&
                Mathf.Abs(leftHit.point.x - left) < Tolerance, "Left opening face differs.");
            Require(Physics.Raycast(new Vector3(-.35f,1.1f,4), Vector3.right, out var rightHit, 1.1f) &&
                Mathf.Abs(rightHit.point.x - right) < Tolerance, "Right opening face differs.");
            Require(Physics.Raycast(new Vector3(-.35f,1.1f,4), Vector3.up, out var headHit, 1.5f) &&
                Mathf.Abs(headHit.point.y - 2.2f) < Tolerance, "Lintel clearance differs.");
            Require(movingCollider.bounds.min.x >= right + .10f, "Parked leaf intrudes into the route.");

            for (int step = 0; step <= 140; step++)
            {
                float z = .5f + step * .05f;
                var sample = new Vector3(-.35f, .34f, z);
                Require(!Physics.CheckCapsule(sample, sample + 1.14f * Vector3.up, .32f, 1,
                    QueryTriggerInteraction.Ignore), $"Capsule intersects the kit at Z={z:F2}.");
                Require(Physics.Raycast(new Vector3(sample.x,1,z), Vector3.down, out var floorHit, 1.01f) &&
                    Mathf.Abs(floorHit.point.y) < Tolerance, $"Floor step or gap at Z={z:F2}.");
            }
            foreach (var bay in new[] { corridorA, corridorB })
            {
                var wall = bay.transform.Find("Collision/Right wall").GetComponent<BoxCollider>();
                float offset = bay == corridorA ? -.0003f : .0003f;
                var ray = new Ray(new Vector3(0, 1, 4 + offset), Vector3.right);
                Require(wall.Raycast(ray, out var wallHit, 2.01f) &&
                    Mathf.Abs(wallHit.point.x - 2) < Tolerance, "Side wall continuity failed at the seam.");
            }
            Require(before.SequenceEqual(Snapshot()), "Geometry validation rewrote assets.");
            Debug.Log("W03_VERIFY_PASS six unique modules, two visual swaps, 1.4 x 2.2 m door, 141 capsule and floor samples.");
        }

        private static void CompareVariants(params StationModule[] modules)
        {
            foreach (var module in modules)
            {
                var id = module.ModuleId;
                var anchor = module.InspectPoint;
                var colliders = module.GetComponentsInChildren<Collider>();
                var transforms = module.GetComponentsInChildren<Transform>(true);
                var authored = transforms.Where(item => item.name == "Authored").ToArray();
                var fallback = transforms.Where(item => item.name == "Fallback").ToArray();
                Require(authored.Length > 0 && authored.Length == fallback.Length,
                    "Expected matched variants on " + id);
                var native = authored.Select(VisualBounds).ToArray();
                Require(module.TrySetFallbackVisual(true) &&
                    authored.All(item => !item.gameObject.activeSelf) &&
                    fallback.All(item => item.gameObject.activeSelf), "Fallback switch failed on " + id);
                for (int i = 0; i < fallback.Length; i++)
                {
                    var alternative = VisualBounds(fallback[i]);
                    RequireNear(native[i].min, alternative.min, "Fallback minimum bounds");
                    RequireNear(native[i].max, alternative.max, "Fallback maximum bounds");
                    Require(fallback[i].GetComponentsInChildren<MeshFilter>().All(item =>
                        item.sharedMesh.uv.Length == item.sharedMesh.vertexCount), "Fallback UVs are missing.");
                }
                Require(module.ModuleId == id && module.InspectPoint == anchor &&
                    colliders.SequenceEqual(module.GetComponentsInChildren<Collider>()),
                    "Visual swap changed ID, inspect point or collider references.");
                Require(module.TrySetFallbackVisual(false) &&
                    authored.All(item => item.gameObject.activeSelf) &&
                    fallback.All(item => !item.gameObject.activeSelf), "Authored visual restore failed.");
            }
        }

        private static void CompareImportedPartBounds(StationModule airlock, StationModule echo)
        {
            CheckPart(airlock.transform.Find("Visual/Authored"), "AirlockFrame");
            CheckPart(airlock.transform.Find("SlidingLeaf/Visual/Authored"), "AirlockLeaf");
            CheckPart(echo.transform.Find("Visual/Authored"), "EchoHousing");
        }

        private static void CheckPart(Transform authored, string name)
        {
            var spec = W03StationKit.ReadModels().Single(item => item.name == name);
            var model = authored.GetChild(0);
            Require(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(model) ==
                W03StationKit.ModelPath(name), "Source model reference differs: " + name);
            foreach (var part in spec.parts)
            {
                var filter = model.GetComponentsInChildren<MeshFilter>().Single(item => item.name == part.name);
                var mesh = filter.sharedMesh;
                Require(mesh.vertexCount > 0 && mesh.vertexCount == mesh.uv.Length &&
                    mesh.vertexCount == mesh.uv2.Length && mesh.vertexCount == mesh.normals.Length,
                    "Missing geometry, UVs or normals on " + part.name);
                var points = mesh.vertices.Select(vertex => authored.InverseTransformPoint(
                    filter.transform.TransformPoint(vertex))).ToArray();
                var actual = new Bounds(points[0], Vector3.zero);
                foreach (var point in points) actual.Encapsulate(point);
                RequireNear(actual.center, part.Center, name + "/" + part.name + " center");
                RequireNear(actual.size, part.Size, name + "/" + part.name + " size");
                Require(filter.GetComponent<MeshRenderer>().sharedMaterial.name == part.surface,
                    "Material slot differs on " + part.name);
            }
        }

        private static Bounds VisualBounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            Require(renderers.Length > 0, "Missing visual geometry.");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static string[] Snapshot()
        {
            var paths = Directory.GetFiles(W03StationKit.Prefabs, "*", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(W03StationKit.Materials, "*", SearchOption.AllDirectories))
                .Concat(new[] {W03StationKit.ScenePath, W03StationKit.ScenePath + ".meta",
                    "ArtSource/Developer1/W03/module-inventory.json"});
            using var hash = SHA256.Create();
            return paths.OrderBy(path => path, StringComparer.Ordinal).Select(path => path + ":" +
                Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(path)))).ToArray();
        }

        public static void RenderPreview()
        {
            Require(Application.isBatchMode, "Capture W03 in batch mode.");
            var openPath = Environment.GetEnvironmentVariable("LKS_W03_OPEN_PREVIEW");
            var closePath = Environment.GetEnvironmentVariable("LKS_W03_CLOSED_PREVIEW");
            var farPath = Environment.GetEnvironmentVariable("LKS_W03_FAR_PREVIEW");
            var scene = Open();
            var camera = Camera.main ?? throw new InvalidDataException("Kit camera missing.");
            Capture(camera, openPath);
            var samples = scene.GetRootGameObjects().Single(item => item.name == "W03 Samples").transform;
            Module(samples, "Airlock").GetComponent<AirlockPresentation>().ApplyOpen(false);
            Capture(camera, closePath);
            if (!string.IsNullOrWhiteSpace(farPath))
            {
                camera.transform.position = new Vector3(-.90f, 1.57f, 4.27f);
                camera.transform.LookAt(new Vector3(.10f, 1.10f, 6.66f));
                camera.fieldOfView = 80;
                Capture(camera, farPath);
            }
            Debug.Log("W03_URP_PREVIEW_PASS");
        }

        private static void Capture(Camera camera, string path)
        {
            Require(!string.IsNullOrWhiteSpace(path) && !File.Exists(path), "Set a new PNG output path.");
            var target = new RenderTexture(1600, 1000, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                var request = new RenderPipeline.StandardRequest { destination = target };
                Require(RenderPipeline.SupportsRenderRequest(camera, request), "URP render request unavailable.");
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                pixels.Apply();
                using var file = new FileStream(path, FileMode.CreateNew);
                byte[] png = pixels.EncodeToPNG();
                file.Write(png, 0, png.Length);
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(target);
            }
        }

        private static Scene Open() => EditorSceneManager.OpenScene(W03StationKit.ScenePath, OpenSceneMode.Single);
        private static StationModule Module(Transform samples, string name) =>
            samples.Find(name).GetComponent<StationModule>();
        private static void RequireNear(Vector3 actual, Vector3 expected, string subject) =>
            Require(Vector3.Distance(actual, expected) < Tolerance, $"{subject}: {actual:F4} != {expected:F4}");
        private static void Require(bool condition, string error)
        {
            if (!condition) throw new InvalidDataException(error);
        }
    }
}
