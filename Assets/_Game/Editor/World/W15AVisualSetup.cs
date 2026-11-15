using System;
using System.Collections.Generic;
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
    public static partial class W15AVisualSetup
    {
        private const string Art = "Assets/_Game/Art/W15A";
        private const string Prefabs = "Assets/_Game/Prefabs/W15A";
        private const string RootName = "W15A Visual Set";
        private static readonly string[] Models =
        {
            "WallA", "WallB", "WallC", "Portal", "Deck", "PowerStack", "EchoStack", "LabBench", "ReportDesk",
            "Recovery", "EntranceTerminal", "SeedlingRack", "ShelterHeader", "Overlook", "FieldCrate",
            "Ridge", "Regolith", "Island", "TerrainBed", "LabBlank"
        };
        private static readonly string[] ExteriorDressedIds =
            { "exterior.left-wall", "exterior.right-wall", "exterior.back-wall", "exterior.sample-island" };

        [MenuItem("LKS/World/W15A/Preview Changes")]
        public static void Preview()
        {
            WithStation(manifest => Debug.Log($"W15A_PREVIEW {Plan(manifest).Count} visual placements, " +
                $"{manifest.Entries.Count} registered station roots, {Models.Length} original models."));
        }

        [MenuItem("LKS/World/W15A/Apply Visuals")]
        public static void Apply()
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (scene.isDirty || (!Application.isBatchMode && string.IsNullOrEmpty(scene.path)))
                    throw new InvalidOperationException("Save open scenes before applying W15A.");
            }
            WithStation(manifest =>
            {
                var placements = Plan(manifest);
                var root = FindRoot(manifest);
                var prefabs = PreparePrefabs();
                ValidateExisting(root, placements, prefabs);
                SetBaseMaterials();
                bool changed = root == null;
                if (root == null)
                {
                    root = new GameObject(RootName).transform;
                    root.SetParent(Node(manifest, "station.spawn").transform, false);
                    root.gameObject.AddComponent<StationVisualSet>();
                }
                changed |= Place(manifest, root, placements, prefabs);
                if (changed)
                {
                    var scene = manifest.gameObject.scene;
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene))
                        throw new InvalidOperationException("Could not save the station visuals.");
                }
                AssetDatabase.SaveAssets();
                Debug.Log($"W15A_APPLY_PASS {placements.Count} visual placements. Scene changed: {changed}.");
            });
        }

        internal static bool RefreshInstalled(StationGenerationManifest manifest)
        {
            var root = FindRoot(manifest);
            if (root == null) return false;
            var placements = Plan(manifest);
            var prefabs = Models.ToDictionary(name => name, LoadPrefab);
            ValidateExisting(root, placements, prefabs);
            return Place(manifest, root, placements, prefabs);
        }

        [MenuItem("LKS/World/W15A/Verify Visuals")]
        public static void Verify()
        {
            WithStation(manifest =>
            {
                var root = FindRoot(manifest);
                var placements = Plan(manifest);
                var prefabs = Models.ToDictionary(name => name, LoadPrefab);
                ValidateExisting(root, placements, prefabs);
                if (root == null) throw new InvalidOperationException("W15A has not been applied.");
                foreach (var item in placements)
                {
                    var visual = root.Find(item.Id);
                    if (visual == null || !MatchesPlacement(visual, item, Quaternion.LookRotation(item.Facing)) ||
                        visual.GetComponentsInChildren<Collider>(true).Length != 0 ||
                        visual.GetComponentsInChildren<StationInteractionAnchor>(true).Length != 0)
                        throw new InvalidOperationException("Missing, moved or interactive W15A geometry: " + item.Id);
                    var renderer = visual.GetComponent<MeshRenderer>();
                    if (renderer == null || !renderer.enabled ||
                        renderer.sharedMaterials.Any(material => material == null ||
                            material.shader.name != "Universal Render Pipeline/Lit" || !material.shader.isSupported))
                        throw new InvalidOperationException("An applied visual renderer or material is unsupported: " + item.Id);
                }
                foreach (string boundary in ExteriorDressedIds)
                {
                    var node = Node(manifest, boundary);
                    if (!node.GetComponent<BoxCollider>().enabled || node.GetComponent<MeshRenderer>().enabled)
                        throw new InvalidOperationException("Exterior boundary collision or visual state changed: " + boundary);
                }
                Debug.Log($"W15A_VERIFY_PASS {placements.Count} visual renderers and {prefabs.Count} collider-free prefabs.");
            });
        }

        private static void WithStation(Action<StationGenerationManifest> work)
        {
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(W04StationGenerator.ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            bool blank = Application.isBatchMode && SceneManager.sceneCount == 1 &&
                string.IsNullOrEmpty(previous.path) && previous.rootCount == 0;
            if (opened)
                scene = EditorSceneManager.OpenScene(W04StationGenerator.ScenePath,
                    blank ? OpenSceneMode.Single : OpenSceneMode.Additive);
            try
            {
                var manifest = W04StationGenerator.FindManifest(scene) ??
                    throw new InvalidOperationException("The accepted W04 scene manifest is required.");
                StableId.ValidateUnique(manifest.Entries.Select(entry => entry.id));
                StableId.ValidateUnique(manifest.GetComponentsInChildren<StationInteractionAnchor>()
                    .Select(anchor => anchor.TargetId));
                work(manifest);
            }
            finally
            {
                if (opened && previous.IsValid() && previous.isLoaded && previous != scene && !scene.isDirty)
                {
                    SceneManager.SetActiveScene(previous);
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static Transform FindRoot(StationGenerationManifest manifest)
        {
            var spawn = Node(manifest, "station.spawn").transform;
            var roots = spawn.Cast<Transform>().Where(child => child.name == RootName).ToArray();
            if (roots.Length == 0) return null;
            if (roots.Length != 1 || !roots[0].TryGetComponent<StationVisualSet>(out _) ||
                roots[0].localPosition != Vector3.zero || roots[0].localRotation != Quaternion.identity ||
                roots[0].localScale != Vector3.one)
                throw new InvalidOperationException("An unowned or moved W15A root requires review.");
            return roots[0];
        }

        private static void ValidateExisting(Transform root, IReadOnlyList<Placement> placements,
            IReadOnlyDictionary<string, GameObject> prefabs)
        {
            if (root == null) return;
            var planned = placements.ToDictionary(item => item.Id);
            var found = new HashSet<string>();
            foreach (Transform child in root)
            {
                if (!planned.TryGetValue(child.name, out var item)) continue; // Manual content.
                if (!found.Add(child.name) ||
                    PrefabUtility.GetCorrespondingObjectFromOriginalSource(child.gameObject) != prefabs[item.Model])
                    throw new InvalidOperationException("Duplicate or unowned visual placement: " + child.name);
            }
        }

        private static bool Place(StationGenerationManifest manifest, Transform root, IReadOnlyList<Placement> placements,
            IReadOnlyDictionary<string, GameObject> prefabs)
        {
            bool changed = false;
            foreach (var item in placements)
            {
                var transform = root.Find(item.Id);
                if (transform == null)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[item.Model], root);
                    go.name = item.Id;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(go);
                    transform = go.transform;
                    changed = true;
                }
                var rotation = Quaternion.LookRotation(item.Facing);
                if (MatchesPlacement(transform, item, rotation))
                    continue;
                transform.SetPositionAndRotation(item.Position, rotation);
                transform.localScale = item.Scale;
                PrefabUtility.RecordPrefabInstancePropertyModifications(transform);
                changed = true;
            }
            foreach (string id in ExteriorDressedIds)
            {
                var renderer = Node(manifest, id).GetComponent<MeshRenderer>();
                if (!renderer.enabled) continue;
                renderer.enabled = false;
                changed = true;
            }
            return changed;
        }

        private static bool MatchesPlacement(Transform transform, Placement item, Quaternion rotation) =>
            transform.position == item.Position && transform.rotation == rotation && transform.localScale == item.Scale;

        private static GameObject Node(StationGenerationManifest manifest, string id) =>
            manifest.Entries.Single(entry => entry.id.value == id).node ??
                throw new InvalidOperationException("Missing station node " + id);
    }
}
