using System;
using System.IO;
using System.Linq;
using LKS.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LKS.Editor.World
{
    public static class W05StationPlayerSetup
    {
        internal const string ScenePath = "Assets/_Game/Scenes/Station.unity";
        internal const string PlayerRoot = "First Person";
        internal const string PrefabPath = "Assets/_Game/Prefabs/W05/StationFirstPerson.prefab";

        public static void BuildStationPreview()
        {
            string output = Environment.GetEnvironmentVariable("LKS_W05_PLAYER");
            if (!Application.isBatchMode || string.IsNullOrEmpty(output) ||
                !Path.IsPathRooted(output) || File.Exists(output))
                throw new InvalidOperationException("Set LKS_W05_PLAYER to a new absolute executable path in batch mode.");
            var report = BuildPipeline.BuildPlayer(new[] { ScenePath }, output, BuildTarget.StandaloneWindows64,
                BuildOptions.None);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("Station preview build failed.");
            Debug.Log("W05_BUILD_PASS Windows x64 station preview.");
        }

        [MenuItem("LKS/World/Install W05 First Person")]
        public static void Install()
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (scene.isDirty || (!Application.isBatchMode && string.IsNullOrEmpty(scene.path)))
                    throw new InvalidOperationException("Save open scenes before installing W05.");
            }
            var active = SceneManager.GetActiveScene();
            var station = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !station.IsValid() || !station.isLoaded;
            bool blank = Application.isBatchMode && SceneManager.sceneCount == 1 &&
                string.IsNullOrEmpty(active.path) && active.rootCount == 0;
            if (opened) station = EditorSceneManager.OpenScene(ScenePath, blank ? OpenSceneMode.Single : OpenSceneMode.Additive);
            try
            {
                var roots = station.GetRootGameObjects();
                var manifest = roots.Select(root => root.GetComponent<StationGenerationManifest>())
                    .Single(item => item != null);
                var spawn = manifest.Entries.Single(entry => entry.id.value == "station.spawn").node.transform;
                var preview = manifest.Entries.Single(entry => entry.id.value == "station.preview-camera").node;
                var existing = roots.SingleOrDefault(root => root.name == PlayerRoot);
                var prefab = LoadOrCreatePrefab();
                bool changed = false;
                if (existing == null)
                {
                    existing = (GameObject)PrefabUtility.InstantiatePrefab(prefab, station);
                    existing.name = PlayerRoot;
                    existing.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
                    changed = true;
                }
                if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(existing) != PrefabPath)
                    throw new InvalidOperationException("The existing First Person object is not owned by this installer.");
                var player = existing.GetComponent<StationFirstPerson>();
                var binding = new SerializedObject(player);
                binding.FindProperty("spawn").objectReferenceValue = spawn;
                var bootstrap = roots.Select(root => root.GetComponent<SessionBootstrap>()).SingleOrDefault(item => item != null);
                binding.FindProperty("sessionBootstrap").objectReferenceValue = bootstrap != null
                    ? bootstrap : existing.GetComponent<SessionBootstrap>();
                changed |= binding.ApplyModifiedPropertiesWithoutUndo();
                if (preview.activeSelf) { preview.SetActive(false); changed = true; }
                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(station);
                    if (!EditorSceneManager.SaveScene(station)) throw new IOException("Failed to save W05 scene wiring.");
                }
                Debug.Log("W05_INSTALL_PASS: Station player prefab and generated spawn are connected.");
            }
            finally
            {
                if (opened && active.IsValid() && active.isLoaded && active != station)
                {
                    SceneManager.SetActiveScene(active);
                    if (!station.isDirty) EditorSceneManager.CloseScene(station, true);
                }
            }
        }

        private static GameObject LoadOrCreatePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null) return existing;
            if (File.Exists(PrefabPath) || File.Exists(PrefabPath + ".meta"))
                throw new IOException("Refusing to overwrite an unknown first-person prefab.");
            if (!AssetDatabase.IsValidFolder("Assets/_Game/Prefabs/W05"))
                AssetDatabase.CreateFolder("Assets/_Game/Prefabs", "W05");
            var root = new GameObject(PlayerRoot);
            try
            {
                var controller = root.AddComponent<CharacterController>();
                controller.height = 1.8f;
                controller.center = new Vector3(0, .9f, 0);
                controller.radius = .32f;
                controller.skinWidth = .035f;
                controller.stepOffset = .28f;
                controller.slopeLimit = 48;
                controller.minMoveDistance = 0;

                var eye = new GameObject("Eye").transform;
                eye.SetParent(root.transform, false);
                eye.localPosition = new Vector3(0, 1.62f, 0);
                var camera = eye.gameObject.AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.fieldOfView = 65;
                camera.nearClipPlane = .055f;
                camera.farClipPlane = 150;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.29f,.32f,.35f);
                eye.gameObject.AddComponent<AudioListener>();
                var motor = new SerializedObject(root.AddComponent<FirstPersonMotor>());
                motor.FindProperty("eye").objectReferenceValue = eye;
                motor.ApplyModifiedPropertiesWithoutUndo();
                var bootstrap = root.AddComponent<SessionBootstrap>();
                var input = new SerializedObject(root.AddComponent<StationFirstPerson>());
                input.FindProperty("sessionBootstrap").objectReferenceValue = bootstrap;
                input.ApplyModifiedPropertiesWithoutUndo();
                root.AddComponent<StationInputOverlay>();
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath)
                    ?? throw new IOException("Failed to save the first-person prefab.");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
