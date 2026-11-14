using System;
using System.IO;
using System.Linq;
using LKS.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LKS.Editor.World
{
    public static class W04Verification
    {
        public static void Verify()
        {
            Check(Application.isBatchMode, "Run W04 verification in an isolated batch-mode checkout.");
            var scene = Open();
            var manifest = W04StationGenerator.FindManifest(scene);
            byte[] originalScene = File.ReadAllBytes(W04StationGenerator.ScenePath);
            byte[] originalLayout = File.ReadAllBytes(W04StationGenerator.LayoutPath);
            var ids = Ids(manifest);
            var identities = Identities(manifest);
            Check(ids.Length == manifest.transform.childCount, "Unexpected children under the generated root.");
            foreach (int pass in new[] {1, 2})
            {
                W04StationGenerator.Generate();
                Check(originalScene.SequenceEqual(File.ReadAllBytes(W04StationGenerator.ScenePath)),
                    "No-op regeneration rewrote the saved scene.");
                Check(!scene.isDirty && Ids(manifest).SequenceEqual(ids) &&
                    Identities(manifest).SequenceEqual(identities), "Regeneration changed IDs or references.");
                Debug.Log($"W04_NOOP_PASS {pass}: {ids.Length} objects with stable IDs and scene bytes.");
            }
            ValidateRoute(manifest);

            var reference = manifest.Entries.Single(entry => entry.id.value == "anchor.echo").node.transform;
            try
            {
                var manualRoot = new GameObject("Protected manual root");
                var manualChild = new GameObject("Protected manual child");
                manualChild.transform.SetParent(manifest.transform, false);
                manualChild.transform.localPosition = new Vector3(.57f, .36f, 3.61f);
                // A manually authored reference into the generated station.
                var manual = manualRoot.AddComponent<StationInteractionAnchor>();
                var edit = new SerializedObject(manual);
                edit.FindProperty("targetId").FindPropertyRelative("value").stringValue = "target.manual-check";
                edit.FindProperty("approach").objectReferenceValue = reference;
                edit.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                bool rejected = false;
                try { W04StationGenerator.Generate(); }
                catch (InvalidOperationException error) when (error.Message.Contains("unsaved")) { rejected = true; }
                Check(rejected && manualChild != null && scene.isDirty &&
                    originalScene.SequenceEqual(File.ReadAllBytes(W04StationGenerator.ScenePath)),
                    "Generator did not protect unsaved manual content.");

                Check(EditorSceneManager.SaveScene(scene), "Cannot save verification fixture.");
                var savedProp = GlobalObjectId.GetGlobalObjectIdSlow(manualChild);
                var generatedReference = GlobalObjectId.GetGlobalObjectIdSlow(reference);
                var layout = new SerializedObject(manifest.Layout);
                var bay = layout.FindProperty("bayLength");
                bay.floatValue += bay.floatValue <= 7.75f ? .25f : -.25f;
                layout.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(manifest.Layout);
                var previousPosition = reference.localPosition;
                W04StationGenerator.Generate();
                Check(reference.localPosition != previousPosition, "Config edit did not reposition its anchor.");
                Check(manual != null && manual.Approach == reference && manualChild != null &&
                    manualChild.transform.localPosition == new Vector3(.57f,.36f,3.61f) &&
                    GlobalObjectId.GetGlobalObjectIdSlow(manualChild).Equals(savedProp) &&
                    GlobalObjectId.GetGlobalObjectIdSlow(reference).Equals(generatedReference),
                    "Regeneration changed manual content or its reference.");
                Check(Identities(manifest).SequenceEqual(identities), "Layout update replaced a generated object.");
                ValidateRoute(manifest);

                scene = EditorSceneManager.OpenScene(W04StationGenerator.ScenePath, OpenSceneMode.Single);
                var root = scene.GetRootGameObjects().Single(go => go.name == "Protected manual root");
                Check(root.GetComponent<StationInteractionAnchor>().Approach != null &&
                    root.GetComponent<StationInteractionAnchor>().Approach.name == "anchor.echo" &&
                    GlobalObjectId.GetGlobalObjectIdSlow(root.GetComponent<StationInteractionAnchor>().Approach)
                        .Equals(generatedReference), "Manual reference did not survive save/reopen.");
                Check(W04StationGenerator.FindManifest(scene).transform.Find("Protected manual child") != null,
                    "Manual child was lost after reopening.");
                Debug.Log("W04_PROTECTION_PASS unsaved edits blocked, manual root/child and serialized link preserved.");
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                File.WriteAllBytes(W04StationGenerator.ScenePath, originalScene);
                File.WriteAllBytes(W04StationGenerator.LayoutPath, originalLayout);
                AssetDatabase.ImportAsset(W04StationGenerator.LayoutPath, ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(W04StationGenerator.ScenePath, ImportAssetOptions.ForceSynchronousImport);
            }
            Check(originalScene.SequenceEqual(File.ReadAllBytes(W04StationGenerator.ScenePath)) &&
                originalLayout.SequenceEqual(File.ReadAllBytes(W04StationGenerator.LayoutPath)),
                "Verification fixture was not restored.");
            Debug.Log("W04_VERIFY_PASS");
        }

        private static void ValidateRoute(StationGenerationManifest manifest)
        {
            var roots = manifest.gameObject.scene.GetRootGameObjects();
            var player = roots.Select(root => root.GetComponent<StationFirstPerson>()).SingleOrDefault(item => item != null);
            var body = player != null ? player.GetComponent<CharacterController>() : null;
            bool wasEnabled = body != null && body.enabled;
            // This geometry check assumes the powered route. W06 Play Mode covers closed and unpowered traversal.
            var exteriorGate = roots.Select(root => root.GetComponent<StationExteriorGate>()).SingleOrDefault(gate => gate != null);
            exteriorGate?.Present(true, manifest.Layout.Spawn);
            if (wasEnabled) body.enabled = false;
            try { ValidateClearance(manifest); }
            finally
            {
                if (wasEnabled) body.enabled = true;
                exteriorGate?.Present(false, manifest.Layout.Spawn);
            }
        }

        private static void ValidateClearance(StationGenerationManifest manifest)
        {
            Physics.SyncTransforms();
            var configuration = manifest.Layout;
            var anchors = manifest.GetComponentsInChildren<StationInteractionAnchor>();
            var targets = anchors.Select(anchor => anchor.TargetId).ToArray();
            StableId.ValidateUnique(targets);
            // These are the IDs from the accepted S03 handoff. Scene code adds no policy.
            var expected = new[] { "quest.arrival", "quest.safe-power", "target.power-recovery",
                "quest.echo-evidence", "quest.sampling", "quest.analysis", "quest.report", "quest.ending" };
            Check(expected.OrderBy(id => id).SequenceEqual(targets.Select(id => id.value).OrderBy(id => id)),
                "Scene anchor IDs differ from the S03 handoff.");
            var near = W04StationPlan.LoopNear(configuration);
            var far = W04StationPlan.LoopFar(configuration);
            var entry = W04StationPlan.Entry(configuration);
            float side = W04StationPlan.LoopSide(configuration);
            var route = new[] { configuration.Spawn, new Vector3(0,0,configuration.Spawn.z), entry, near,
                new Vector3(-side,0,near.z), new Vector3(-side,0,far.z), far,
                new Vector3(side,0,far.z), new Vector3(side,0,near.z), near, entry,
                new Vector3(0,0,configuration.BayLength*4.49f) };
            int samples = 0;
            for (int i = 1; i < route.Length; i++) samples += Traverse(route[i - 1], route[i]);
            foreach (var anchor in anchors)
            {
                var approach = anchor.Approach.position;
                if (anchor.TargetId.value != "quest.sampling")
                    samples += Traverse(new Vector3(0,0,approach.z), approach);
                else CheckFloorAndClearance(approach);
                var eye = approach + Vector3.up * 1.55f;
                Check(Vector3.Distance(eye, anchor.transform.position) < 1.5f &&
                    !Physics.Linecast(eye, anchor.transform.position, 1, QueryTriggerInteraction.Ignore),
                    "An interaction anchor is too far or blocked: " + anchor.TargetId);
            }
            var wall = configuration.ExteriorWidth / 2;
            var depth = configuration.ExteriorDepth;
            var end = configuration.InteriorEnd;
            Barrier(new Vector3(-wall+.8f,1.2f,end+3), Vector3.left, "exterior.left-wall");
            Barrier(new Vector3(wall-.8f,1.2f,end+3), Vector3.right, "exterior.right-wall");
            Barrier(new Vector3(-side,1.2f,end+depth-.8f), Vector3.forward, "exterior.back-wall");
            float shoulder = (wall + configuration.InnerWidth / 2) / 2;
            Barrier(new Vector3(-shoulder,1.2f,end+.8f), Vector3.back, "exterior.front-left");
            Barrier(new Vector3(shoulder,1.2f,end+.8f), Vector3.back, "exterior.front-right");
            Debug.Log($"W04_ROUTE_PASS {samples} clearance/floor samples, eight interaction rays, five exterior boundaries.");
        }

        private static int Traverse(Vector3 from, Vector3 to)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(from,to) / .08f));
            for (int i = 0; i <= steps; i++) CheckFloorAndClearance(Vector3.Lerp(from,to,(float)i/steps));
            return steps + 1;
        }

        private static void CheckFloorAndClearance(Vector3 onFloor)
        {
            Check(!Physics.CheckCapsule(onFloor + Vector3.up * .34f, onFloor + Vector3.up * 1.46f,
                .32f, 1, QueryTriggerInteraction.Ignore), $"Route blocked at {onFloor:F3}.");
            Check(Physics.Raycast(onFloor + Vector3.up, Vector3.down, out var ground, 1.01f, 1) &&
                Mathf.Abs(ground.point.y-onFloor.y) < .001f, $"No level floor at {onFloor:F3}.");
        }

        private static void Barrier(Vector3 start, Vector3 direction, string name)
        {
            Check(Physics.Raycast(start, direction, out var hit, 1.4f, 1) && hit.collider.name == name,
                "Exterior boundary does not block the route: " + name);
        }

        private static string[] Ids(StationGenerationManifest manifest) =>
            manifest.Entries.Select(entry => entry.id.value).ToArray();
        private static GlobalObjectId[] Identities(StationGenerationManifest manifest) =>
            manifest.Entries.Select(entry => GlobalObjectId.GetGlobalObjectIdSlow(entry.node)).ToArray();

        public static void RenderPreview()
        {
            Check(Application.isBatchMode, "Capture W04 in batch mode.");
            var manifest = W04StationGenerator.FindManifest(Open());
            var layout = manifest.Layout;
            var position = new Vector3(-layout.ExteriorWidth * .41f, 2.5f, layout.InteriorEnd + 1.05f);
            var lookAt = new Vector3(.4f,.65f,layout.InteriorEnd+layout.ExteriorDepth*.68f);
            var camera = manifest.GetComponentInChildren<Camera>();
            Capture(camera, Environment.GetEnvironmentVariable("LKS_W04_INTERIOR"));
            camera = W04StationGenerator.FindManifest(Open()).GetComponentInChildren<Camera>();
            camera.transform.position = position;
            camera.transform.LookAt(lookAt);
            camera.fieldOfView = 73;
            Capture(camera, Environment.GetEnvironmentVariable("LKS_W04_EXTERIOR"));
            Debug.Log("W04_URP_PREVIEW_PASS");
        }

        internal static void Capture(Camera camera, string path)
        {
            Check(!string.IsNullOrWhiteSpace(path) && !File.Exists(path), "Set a new PNG output path.");
            var target = new RenderTexture(1600, 1000, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                var request = new RenderPipeline.StandardRequest { destination = target };
                Check(RenderPipeline.SupportsRenderRequest(camera, request), "URP render request unavailable.");
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

        private static Scene Open() =>
            EditorSceneManager.OpenScene(W04StationGenerator.ScenePath, OpenSceneMode.Single);
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidDataException(message);
        }
    }
}
