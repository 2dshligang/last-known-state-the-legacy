using System;
using System.Collections.Generic;
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
    public static class W04StationGenerator
    {
        internal const string ScenePath = "Assets/_Game/Scenes/Station.unity";
        internal const string LayoutPath = "Assets/_Game/World/W04/StationLayout.asset";
        private const string ArtFolder = "Assets/_Game/Art/W04";
        internal const string RootName = "LKS_Generated";

        [MenuItem("LKS/World/Preview W04 Station")]
        public static void Preview()
        {
            var layout = AssetDatabase.LoadAssetAtPath<StationLayout>(LayoutPath);
            bool temporary = layout == null;
            if (temporary) layout = ScriptableObject.CreateInstance<StationLayout>();
            try
            {
                var nodes = W04StationPlan.Build(layout);
                var scene = SceneManager.GetSceneByPath(ScenePath);
                var manifest = scene.IsValid() && scene.isLoaded ? FindManifest(scene) : null;
                Debug.Log($"W04 preview: {nodes.Count} nodes, " +
                    $"{nodes.Count(node => node.Type == W04StationPlan.Kind.Anchor)} interaction anchors, " +
                    $"{manifest?.Entries.Count ?? 0} known nodes in the open station scene.");
            }
            finally { if (temporary) Object.DestroyImmediate(layout); }
        }

        [MenuItem("LKS/World/Generate W04 Station")]
        public static void Generate()
        {
            RejectUnsavedScenes();
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            bool existing = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null;
            if (!existing) RequireVacant(ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened)
            {
                bool blankBatch = Application.isBatchMode && SceneManager.sceneCount == 1 &&
                    string.IsNullOrEmpty(previous.path) && !previous.isDirty;
                if (existing)
                    scene = EditorSceneManager.OpenScene(ScenePath, blankBatch ? OpenSceneMode.Single : OpenSceneMode.Additive);
                else
                    scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                        blankBatch ? NewSceneMode.Single : NewSceneMode.Additive);
            }
            try
            {
                // Scene opening can unload unreferenced assets. Load the layout in the target scene context.
                var layout = AssetDatabase.LoadAssetAtPath<StationLayout>(LayoutPath);
                if (layout == null)
                {
                    RequireVacant(LayoutPath);
                    layout = ScriptableObject.CreateInstance<StationLayout>();
                    AssetDatabase.CreateAsset(layout, LayoutPath);
                }
                var plan = W04StationPlan.Build(layout);
                ValidateIds(plan);
                var manifest = FindManifest(scene);
                var owned = ReadOwned(manifest);
                var desired = plan.Select(node => node.Id).ToHashSet();
                if (owned.Keys.Any(id => !desired.Contains(id)))
                    throw new InvalidOperationException("The station plan removed a generated ID. Review and migrate the recorded object first.");

                var palette = LoadPalette();
                var labelMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/W03/Materials/WorldText.mat");
                if (labelMaterial == null) throw new InvalidOperationException("Accepted W03 world label material is missing.");
                // Existing nodes are edited in place. Only a missing ID creates a new root child.
                if (manifest == null)
                {
                    var root = new GameObject(RootName);
                    SceneManager.MoveGameObjectToScene(root, scene);
                    manifest = root.AddComponent<StationGenerationManifest>();
                }
                foreach (var node in plan)
                {
                    if (owned.ContainsKey(node.Id)) continue;
                    var item = CreateNode(node, manifest.transform);
                    owned.Add(node.Id, item);
                }
                bool changed = false;
                foreach (var node in plan)
                    changed |= ApplyNode(node, owned[node.Id], owned, palette, labelMaterial);
                changed |= WriteManifest(manifest, layout, plan, owned);
                changed |= W15AVisualSetup.RefreshInstalled(manifest);
                changed |= W16StationLighting.ApplyAtmosphere(scene, Required<Light>(owned[new StableId("exterior.sun")]));
                if (changed)
                    EditorSceneManager.MarkSceneDirty(scene);

                if (!existing || scene.isDirty)
                {
                    if (!EditorSceneManager.SaveScene(scene, ScenePath))
                        throw new IOException("Unable to save the station scene.");
                }
                Debug.Log($"W04_GENERATE_PASS {owned.Count} registered objects.");
            }
            finally
            {
                if (opened && previous.IsValid() && previous.isLoaded && previous != scene)
                {
                    SceneManager.SetActiveScene(previous);
                    if (!scene.isDirty) EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void ValidateIds(List<W04StationPlan.Node> plan)
        {
            StableId.ValidateUnique(plan.Select(node => node.Id));
            StableId.ValidateUnique(plan.Where(node => node.Type == W04StationPlan.Kind.Anchor).Select(node => node.Target));
        }

        private static Dictionary<StableId, GameObject> ReadOwned(StationGenerationManifest manifest)
        {
            var owned = new Dictionary<StableId, GameObject>();
            if (manifest == null) return owned;
            var assigned = new HashSet<GameObject>();
            foreach (var entry in manifest.Entries)
            {
                if (entry == null || !entry.id.IsValid || entry.node == null ||
                    entry.node.transform.parent != manifest.transform || !assigned.Add(entry.node) ||
                    !owned.TryAdd(entry.id, entry.node))
                    throw new InvalidOperationException("Invalid or duplicate station manifest entry. Repair it before generating.");
            }
            return owned;
        }

        internal static StationGenerationManifest FindManifest(Scene scene)
        {
            var roots = scene.GetRootGameObjects().Where(root => root.name == RootName).ToArray();
            if (roots.Length == 0) return null;
            if (roots.Length != 1 || !roots[0].TryGetComponent(out StationGenerationManifest manifest) ||
                roots[0].transform.position != Vector3.zero || roots[0].transform.rotation != Quaternion.identity ||
                roots[0].transform.localScale != Vector3.one)
                throw new InvalidOperationException("An unowned, duplicate or moved LKS_Generated root blocks W04 generation.");
            return manifest;
        }

        private static GameObject CreateNode(W04StationPlan.Node node, Transform parent)
        {
            var item = node.Type == W04StationPlan.Kind.Box
                ? GameObject.CreatePrimitive(PrimitiveType.Cube) : new GameObject();
            item.name = node.Id.value;
            item.transform.SetParent(parent, false);
            switch (node.Type)
            {
                case W04StationPlan.Kind.Box:
                    if (!node.Collides) Object.DestroyImmediate(item.GetComponent<BoxCollider>());
                    break;
                case W04StationPlan.Kind.Anchor: item.AddComponent<StationInteractionAnchor>(); break;
                case W04StationPlan.Kind.Label: item.AddComponent<TextMesh>(); break;
                case W04StationPlan.Kind.Camera: item.AddComponent<Camera>(); break;
                case W04StationPlan.Kind.Light:
                case W04StationPlan.Kind.DirectionalLight: item.AddComponent<Light>(); break;
            }
            return item;
        }

        private static bool ApplyNode(W04StationPlan.Node node, GameObject item,
            IReadOnlyDictionary<StableId, GameObject> owned, IReadOnlyDictionary<string, Material> palette,
            Material labelMaterial)
        {
            bool changed = false;
            changed |= Assign(item.name, node.Id.value, value => item.name = value);
            var transform = item.transform;
            changed |= Assign(transform.localPosition, node.Position, value => transform.localPosition = value);
            var scale = node.Type == W04StationPlan.Kind.Box ? node.Size : Vector3.one;
            changed |= Assign(transform.localScale, scale, value => transform.localScale = value);
            var rotation = node.Facing == Vector3.zero ? Quaternion.identity : Quaternion.LookRotation(node.Facing);
            // Unity can renormalize serialized rotations on load.
            if (transform.localRotation != rotation)
            {
                transform.localRotation = rotation;
                changed = true;
            }
            switch (node.Type)
            {
                case W04StationPlan.Kind.Box:
                    var renderer = Required<MeshRenderer>(item);
                    changed |= Assign(renderer.sharedMaterial, palette[node.Material], value => renderer.sharedMaterial = value);
                    if (!node.CastsShadows)
                        changed |= Assign(renderer.shadowCastingMode, ShadowCastingMode.Off, value => renderer.shadowCastingMode = value);
                    if (node.Collides && !Required<BoxCollider>(item).enabled)
                        throw new InvalidOperationException("Generated collider is disabled: " + node.Id);
                    break;
                case W04StationPlan.Kind.Anchor:
                    var anchor = new SerializedObject(Required<StationInteractionAnchor>(item));
                    anchor.FindProperty("targetId").FindPropertyRelative("value").stringValue = node.Target.value;
                    var approach = new StableId("approach." + node.Id.value.Substring("anchor.".Length));
                    anchor.FindProperty("approach").objectReferenceValue = owned[approach].transform;
                    changed |= anchor.ApplyModifiedPropertiesWithoutUndo();
                    break;
                case W04StationPlan.Kind.Label:
                    changed |= ApplyLabel(Required<TextMesh>(item), node, labelMaterial);
                    break;
                case W04StationPlan.Kind.Camera:
                    var camera = Required<Camera>(item);
                    changed |= Assign(camera.fieldOfView, 65f, value => camera.fieldOfView = value);
                    changed |= Assign(camera.clearFlags, CameraClearFlags.Skybox, value => camera.clearFlags = value);
                    var sky = new Color(.29f,.32f,.35f);
                    changed |= Assign(camera.backgroundColor, sky, value => camera.backgroundColor = value);
                    break;
                case W04StationPlan.Kind.Light:
                case W04StationPlan.Kind.DirectionalLight:
                    var light = Required<Light>(item);
                    bool directional = node.Type == W04StationPlan.Kind.DirectionalLight;
                    changed |= Assign(light.type, directional ? LightType.Directional : LightType.Spot, value => light.type = value);
                    changed |= Assign(light.color, node.LightColor, value => light.color = value);
                    changed |= Assign(light.intensity, node.Size.x, value => light.intensity = value);
                    changed |= Assign(light.shadows, directional ? LightShadows.Soft : LightShadows.None, value => light.shadows = value);
                    if (directional)
                        changed |= Assign(light.shadowStrength, .69f, value => light.shadowStrength = value);
                    else
                    {
                        changed |= Assign(light.range, node.Size.y, value => light.range = value);
                        changed |= Assign(light.spotAngle, 128f, value => light.spotAngle = value);
                        changed |= Assign(light.innerSpotAngle, 64f, value => light.innerSpotAngle = value);
                    }
                    break;
            }
            return changed;
        }

        private static bool Assign<T>(T current, T next, Action<T> setter)
        {
            if (EqualityComparer<T>.Default.Equals(current, next)) return false;
            setter(next);
            return true;
        }

        private static T Required<T>(GameObject item) where T : Component =>
            item.GetComponent<T>() ?? throw new InvalidOperationException($"Generated node {item.name} lost {typeof(T).Name}.");

        private static bool ApplyLabel(TextMesh label, W04StationPlan.Node node, Material material)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            bool changed = Assign(label.font, font, value => label.font = value);
            var renderer = Required<MeshRenderer>(label.gameObject);
            changed |= Assign(renderer.sharedMaterial, material, value => renderer.sharedMaterial = value);
            changed |= Assign(label.text, node.Text, value => label.text = value);
            changed |= Assign(label.anchor, TextAnchor.MiddleCenter, value => label.anchor = value);
            changed |= Assign(label.alignment, TextAlignment.Center, value => label.alignment = value);
            changed |= Assign(label.fontSize, 64, value => label.fontSize = value);
            changed |= Assign(label.characterSize, node.Size.x / 10, value => label.characterSize = value);
            changed |= Assign(label.color, Color.white, value => label.color = value);
            if (label.GetComponent<StationLabel>() == null)
            {
                label.gameObject.AddComponent<StationLabel>();
                changed = true;
            }
            return changed;
        }

        private static bool WriteManifest(StationGenerationManifest manifest, StationLayout layout,
            List<W04StationPlan.Node> plan, IReadOnlyDictionary<StableId, GameObject> owned)
        {
            var edit = new SerializedObject(manifest);
            edit.FindProperty("layout").objectReferenceValue = layout;
            var entries = edit.FindProperty("entries");
            entries.arraySize = plan.Count;
            for (int i = 0; i < plan.Count; i++)
            {
                var id = plan[i].Id;
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("id").FindPropertyRelative("value").stringValue = id.value;
                entry.FindPropertyRelative("node").objectReferenceValue = owned[id];
            }
            return edit.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Dictionary<string, Material> LoadPalette()
        {
            if (!AssetDatabase.IsValidFolder(ArtFolder))
                AssetDatabase.CreateFolder("Assets/_Game/Art", "W04");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit is unavailable.");
            var colors = new Dictionary<string, Color>
            {
                ["Shell"] = new Color(.46f,.44f,.40f), ["Metal"] = new Color(.16f,.19f,.20f),
                ["Floor"] = new Color(.24f,.23f,.21f), ["Ochre"] = new Color(.61f,.30f,.09f),
                ["Dust"] = new Color(.43f,.26f,.16f)
            };
            var palette = new Dictionary<string, Material>();
            foreach (var pair in colors)
            {
                var path = $"{ArtFolder}/{pair.Key}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    RequireVacant(path);
                    material = new Material(shader) { name = pair.Key };
                    material.SetColor("_BaseColor", pair.Value);
                    material.SetFloat("_Smoothness", .13f);
                    AssetDatabase.CreateAsset(material, path);
                }
                palette.Add(pair.Key, material);
            }
            return palette;
        }

        private static void RequireVacant(string path)
        {
            if (File.Exists(path) || Directory.Exists(path) || File.Exists(path + ".meta"))
                throw new IOException("Refusing to overwrite an unrecognized asset at " + path);
        }

        private static void RejectUnsavedScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var open = SceneManager.GetSceneAt(i);
                if (open.isDirty || (string.IsNullOrEmpty(open.path) &&
                    (!Application.isBatchMode || open.rootCount != 0)))
                    throw new InvalidOperationException("Save or close scenes with unsaved edits before generating the station.");
            }
        }
    }
}
