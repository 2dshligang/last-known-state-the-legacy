using System;
using System.IO;
using System.Linq;
using LKS.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LKS.Editor.World
{
    public static class W06StationSetup
    {
        private const string DoorPrefab = "Assets/_Game/Prefabs/W03/Airlock.prefab";
        private const string SciencePath = "Assets/_Game/Data/StationScienceCatalog.asset";

        public static void RenderPreview()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Capture W06 in batch mode.");
            var output = Environment.GetEnvironmentVariable("LKS_W06_PREVIEWS");
            if (string.IsNullOrEmpty(output) || !Path.IsPathRooted(output) || !Directory.Exists(output))
                throw new InvalidOperationException("Set LKS_W06_PREVIEWS to an existing absolute directory.");
            var scene = EditorSceneManager.OpenScene(W05StationPlayerSetup.ScenePath, OpenSceneMode.Single);
            var gate = scene.GetRootGameObjects().Select(root => root.GetComponent<StationExteriorGate>())
                .Single(item => item != null);
            var camera = scene.GetRootGameObjects().Select(root => root.GetComponent<StationFirstPerson>())
                .Single(item => item != null).GetComponentInChildren<Camera>();
            var eye = new Vector3(0, 1.62f, gate.transform.position.z - 2.55f);
            camera.transform.SetPositionAndRotation(eye, Quaternion.Euler(5, 0, 0));
            gate.Present(false, eye);
            W04Verification.Capture(camera, Path.Combine(output, "W06-airlock-closed.png"));
            gate.Present(true, eye);
            W04Verification.Capture(camera, Path.Combine(output, "W06-airlock-powered.png"));
            Debug.Log("W06_URP_PREVIEW_PASS");
        }

        [MenuItem("LKS/World/Install W06 Station Interaction")]
        public static void Install()
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (scene.isDirty || (!Application.isBatchMode && string.IsNullOrEmpty(scene.path)))
                    throw new InvalidOperationException("Save open scenes before installing W06.");
            }
            var active = SceneManager.GetActiveScene();
            var station = SceneManager.GetSceneByPath(W05StationPlayerSetup.ScenePath);
            bool opened = !station.IsValid() || !station.isLoaded;
            bool blank = Application.isBatchMode && SceneManager.sceneCount == 1 &&
                string.IsNullOrEmpty(active.path) && active.rootCount == 0;
            if (opened)
                station = EditorSceneManager.OpenScene(W05StationPlayerSetup.ScenePath,
                    blank ? OpenSceneMode.Single : OpenSceneMode.Additive);
            try
            {
                var roots = station.GetRootGameObjects();
                var manifest = roots.Select(root => root.GetComponent<StationGenerationManifest>())
                    .Single(item => item != null);
                var player = roots.Select(root => root.GetComponent<StationFirstPerson>()).Single(item => item != null);
                var camera = player.GetComponentInChildren<Camera>();
                var bootstrap = roots.Select(root => root.GetComponent<SessionBootstrap>()).Single(item => item != null);
                var catalog = AssetDatabase.LoadAssetAtPath<StationScienceCatalog>(SciencePath);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefab);
                if (catalog == null || prefab == null || camera == null || prefab.GetComponent<AirlockPresentation>() == null)
                    throw new InvalidOperationException("W06 requires the W03 airlock, player camera and S10 catalog.");

                var anchors = manifest.GetComponentsInChildren<StationInteractionAnchor>();
                StableId.ValidateUnique(anchors.Select(anchor => anchor.TargetId));
                var missing = StationQuests.ValidateReachability(anchors.Select(anchor => anchor.TargetId)).ToArray();
                if (missing.Length > 0) throw new InvalidOperationException(string.Join("\n", missing));

                bool changed = false;
                var gate = roots.Select(root => root.GetComponent<StationExteriorGate>()).SingleOrDefault(item => item != null);
                if (gate == null)
                {
                    var door = (GameObject)PrefabUtility.InstantiatePrefab(prefab, station);
                    door.name = StationExteriorGate.Id.value;
                    gate = door.AddComponent<StationExteriorGate>();
                    changed = true;
                }
                if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gate.gameObject) != DoorPrefab)
                    throw new InvalidOperationException("W06 exterior gate is not the station airlock prefab.");
                var gatePosition = new Vector3(.35f, 0, manifest.Layout.InteriorEnd - .6f);
                if (gate.transform.position != gatePosition || gate.transform.rotation != Quaternion.identity)
                {
                    gate.transform.SetPositionAndRotation(gatePosition, Quaternion.identity);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(gate.transform);
                    changed = true;
                }
                var doorBinding = new SerializedObject(gate);
                doorBinding.FindProperty("door").objectReferenceValue = gate.GetComponent<AirlockPresentation>();
                changed |= doorBinding.ApplyModifiedPropertiesWithoutUndo();

                var bridge = player.GetComponent<StationInteraction>();
                if (bridge == null)
                {
                    bridge = player.gameObject.AddComponent<StationInteraction>();
                    changed = true;
                }
                var console = player.GetComponent<StationPowerConsole>();
                if (console == null)
                {
                    console = player.gameObject.AddComponent<StationPowerConsole>();
                    changed = true;
                }
                var consoleBinding = new SerializedObject(console);
                consoleBinding.FindProperty("player").objectReferenceValue = player;
                consoleBinding.FindProperty("interaction").objectReferenceValue = bridge;
                changed |= consoleBinding.ApplyModifiedPropertiesWithoutUndo();
                var shelter = player.GetComponent<StationShelterSet>();
                if (anchors.Any(anchor => anchor.TargetId == StationShelterSet.ShelterTarget))
                {
                    if (shelter == null)
                    {
                        shelter = player.gameObject.AddComponent<StationShelterSet>();
                        changed = true;
                    }
                    var nodes = manifest.Entries.ToDictionary(entry => entry.id.value, entry => entry.node);
                    var shelterBinding = new SerializedObject(shelter);
                    shelterBinding.FindProperty("player").objectReferenceValue = player;
                    shelterBinding.FindProperty("interaction").objectReferenceValue = bridge;
                    shelterBinding.FindProperty("shutter").objectReferenceValue = nodes["shelter.shutter"].transform;
                    shelterBinding.FindProperty("closedStop").objectReferenceValue = nodes["shelter.shutter-stop"].transform;
                    shelterBinding.FindProperty("shelterStatus").objectReferenceValue =
                        nodes["fixture.shelter-status.label"].GetComponent<StationLabel>();
                    shelterBinding.FindProperty("seedlingStatus").objectReferenceValue =
                        nodes["fixture.seedling-status.label"].GetComponent<StationLabel>();
                    changed |= shelterBinding.ApplyModifiedPropertiesWithoutUndo();
                }
                var archiveReader = player.GetComponent<StationArchiveReader>();
                if (archiveReader == null && anchors.Any(anchor => anchor.TargetId == StationArchiveReader.Target))
                {
                    archiveReader = player.gameObject.AddComponent<StationArchiveReader>();
                    changed = true;
                }

                var binding = new SerializedObject(bridge);
                binding.FindProperty("player").objectReferenceValue = player;
                binding.FindProperty("view").objectReferenceValue = camera;
                binding.FindProperty("bootstrap").objectReferenceValue = bootstrap;
                binding.FindProperty("science").objectReferenceValue = catalog;
                binding.FindProperty("station").objectReferenceValue = manifest;
                binding.FindProperty("gate").objectReferenceValue = gate;
                binding.FindProperty("powerConsole").objectReferenceValue = console;
                binding.FindProperty("shelter").objectReferenceValue = shelter;
                binding.FindProperty("archiveReader").objectReferenceValue = archiveReader;
                changed |= binding.ApplyModifiedPropertiesWithoutUndo();
                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(station);
                    if (!EditorSceneManager.SaveScene(station)) throw new IOException("Failed to save W06 scene wiring.");
                }
                Debug.Log($"W08_INSTALL_PASS {anchors.Length} interaction anchors. Changed: {changed}.");
            }
            finally
            {
                if (opened && active.IsValid() && active.isLoaded && active != station && !station.isDirty)
                {
                    SceneManager.SetActiveScene(active);
                    EditorSceneManager.CloseScene(station, true);
                }
            }
        }
    }
}
