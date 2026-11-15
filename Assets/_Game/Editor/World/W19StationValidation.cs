using System;
using System.Collections.Generic;
using System.Linq;
using LKS.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LKS.Editor.World
{
    public static class W19StationValidation
    {
        private const string BootstrapPath = "Assets/_Game/Scenes/Bootstrap.unity";
        private const string CatalogPath = "Assets/_Game/Data/StationScienceCatalog.asset";

        [MenuItem("LKS/World/Validate Station Bindings")]
        public static void Verify()
        {
            InspectScene(BootstrapPath, AuditReferences);
            InspectScene(StationSceneEntry.Path, VerifyStation);
            Debug.Log("W19_VALIDATION_PASS. Scenes were opened in preview mode and were not saved.");
        }

        private static void VerifyStation(Scene scene)
        {
            AuditReferences(scene);
            var bridge = StationSceneEntry.Resolve(scene);
            var player = Single<StationFirstPerson>(scene);
            var bootstrap = Single<SessionBootstrap>(scene);
            var manifest = Single<StationGenerationManifest>(scene);
            var gate = Single<StationExteriorGate>(scene);
            var power = Single<StationPowerConsole>(scene);
            var shelter = Single<StationShelterSet>(scene);
            var archive = Single<StationArchiveReader>(scene);
            var replay = Single<StationEchoReview>(scene);
            var investigation = Single<StationEchoInvestigation>(scene);

            Check(player.gameObject == bridge.gameObject && player.gameObject == bootstrap.gameObject,
                "Player, session and interaction entry must share the same root.");
            var camera = player.GetComponentsInChildren<Camera>(true).Single();
            Check(camera.isActiveAndEnabled && camera.GetComponent<AudioListener>() != null,
                "The player camera or listener is unavailable.");
            Check(player.GetComponent<CharacterController>() != null && player.GetComponent<FirstPersonMotor>() != null,
                "The station player is missing its controller or motor.");

            var catalog = AssetDatabase.LoadAssetAtPath<StationScienceCatalog>(CatalogPath);
            Check(catalog != null && catalog.TryGet(new StableId("donki.2017-09-04t23-52-00-sep-001"), out var source)
                && source.Kind == EvidenceKind.Observation, "Missing the accepted historical DONKI observation.");
            Bind(bridge, "player", player);
            Bind(bridge, "view", camera);
            Bind(bridge, "bootstrap", bootstrap);
            Bind(bridge, "science", catalog);
            Bind(bridge, "station", manifest);
            Bind(bridge, "gate", gate);
            Bind(bridge, "powerConsole", power);
            Bind(bridge, "shelter", shelter);
            Bind(bridge, "archiveReader", archive);
            Bind(player, "sessionBootstrap", bootstrap);

            Check(manifest.Layout != null, "Station manifest has no layout.");
            manifest.Layout.Validate();
            Check(manifest.Entries.All(entry => entry != null && entry.node != null &&
                entry.node.transform.IsChildOf(manifest.transform)), "Generated manifest contains an out-of-tree node.");
            StableId.ValidateUnique(manifest.Entries.Select(entry => entry.id));
            Check(manifest.Entries.Select(entry => entry.node).Distinct().Count() == manifest.Entries.Count,
                "Two generated IDs point at the same node.");
            var nodes = manifest.Entries.ToDictionary(entry => entry.id.value, entry => entry.node);
            Bind(player, "spawn", Node("station.spawn").transform);
            Bind(gate, "door", gate.GetComponent<AirlockPresentation>());
            Bind(power, "player", player);
            Bind(power, "interaction", bridge);
            Bind(shelter, "player", player);
            Bind(shelter, "interaction", bridge);
            Bind(shelter, "shutter", Node("shelter.shutter").transform);
            Bind(shelter, "closedStop", Node("shelter.shutter-stop").transform);
            Bind(shelter, "shelterStatus", Node("fixture.shelter-status.label").GetComponent<StationLabel>());
            Bind(shelter, "seedlingStatus", Node("fixture.seedling-status.label").GetComponent<StationLabel>());
            Bind(archive, "reconstruction", replay);
            Bind(replay, "investigation", investigation);

            var anchors = Components<StationInteractionAnchor>(scene);
            var ids = anchors.Select(anchor => anchor.TargetId).ToArray();
            StableId.ValidateUnique(ids);
            var errors = StationQuests.ValidateReachability(ids).ToArray();
            Check(errors.Length == 0, string.Join(" ", errors));
            var expected = StationQuests.RequiredStages.Concat(new[] { StationQuests.RecoveryTarget,
                StationShelterSet.ShelterTarget, StationShelterSet.SeedlingTarget })
                .Concat(FictionalSample.All.Select(sample => sample.Id)).ToHashSet();
            Check(anchors.All(anchor => anchor.isActiveAndEnabled && anchor.transform.IsChildOf(manifest.transform)),
                "An authored route anchor is inactive or outside the station manifest.");
            Check(expected.SetEquals(ids), "Station anchors do not match the accepted quest, training and specimen IDs.");
            foreach (var anchor in anchors.OrderBy(anchor => anchor.TargetId.value, StringComparer.Ordinal))
            {
                Check(anchor.name.StartsWith("anchor.", StringComparison.Ordinal), $"Unexpected anchor name {anchor.name}.");
                Bind(anchor, "approach", Node("approach." + anchor.name.Substring("anchor.".Length)).transform);
                Debug.Log($"W19_ANCHOR {anchor.TargetId} -> {anchor.name} -> {anchor.Approach.name}");
            }

            Check(Node("fixture.photo").GetComponent<Renderer>() != null, "The photograph fixture has no renderer.");
            foreach (string resource in new[] { "Farewell", "Arrival", "DmitryPhoto" })
                Check(Resources.Load<Texture2D>("W14/" + resource) != null, $"Missing opening resource W14/{resource}.");
            Check(!Components<StationOpening>(scene).Any() && !Components<StationLaboratory>(scene).Any() &&
                !Components<StationResearchTerminal>(scene).Any() && !Components<StationFinale>(scene).Any(),
                "Runtime scene adapters are also serialized. StationInteraction creates one of each.");

            Debug.Log($"W19_BINDINGS {manifest.Entries.Count} generated IDs, {anchors.Length} interaction anchors; " +
                "runtime adapters: opening, laboratory, report and finale.");

            GameObject Node(string id)
            {
                if (!nodes.TryGetValue(id, out var node))
                    throw new InvalidOperationException($"W19 scene validation: missing generated node {id}.");
                return node;
            }
        }

        private static void AuditReferences(Scene scene)
        {
            var transforms = Components<Transform>(scene);
            int references = 0;
            foreach (var transform in transforms)
            {
                Check(PrefabUtility.GetPrefabInstanceStatus(transform.gameObject) != PrefabInstanceStatus.MissingAsset,
                    $"Missing prefab source at {transform.name}.");
                foreach (var component in transform.GetComponents<Component>())
                {
                    Check(component != null, $"Missing script on {transform.name}.");
                    using var data = new SerializedObject(component);
                    var property = data.GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                        var target = property.objectReferenceValue;
                        if (target == null)
                        {
                            Check(property.objectReferenceEntityIdValue.Equals(EntityId.None),
                                $"Broken reference {transform.name}/{component.GetType().Name}.{property.propertyPath}.");
                            continue;
                        }
                        references++;
                    }
                }
            }
            Debug.Log($"W19_REFERENCES {scene.name}: {transforms.Length} objects, {references} populated references scanned.");
        }

        private static void Bind(Component owner, string field, Object expected)
        {
            using var data = new SerializedObject(owner);
            var property = data.FindProperty(field);
            Check(expected != null && property != null &&
                property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == expected,
                $"Incorrect binding {owner.GetType().Name}.{field}.");
        }

        private static T Single<T>(Scene scene) where T : Component
        {
            var found = Components<T>(scene);
            Check(found.Length == 1, $"{scene.name} requires exactly one {typeof(T).Name}; found {found.Length}.");
            return found[0];
        }

        private static T[] Components<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private static void InspectScene(string path, Action<Scene> inspect)
        {
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try { inspect(scene); }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("W19 scene validation: " + message);
        }
    }
}
