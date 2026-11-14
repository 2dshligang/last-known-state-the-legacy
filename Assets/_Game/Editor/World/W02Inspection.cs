using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LKS.Editor.World
{
    public static class W02Inspection
    {
        internal const string MaterialsFolder = "Assets/_Game/Art/W02/Materials";
        internal const string PrefabsFolder = "Assets/_Game/Prefabs/W02";
        internal const string ScenePath = "Assets/_Game/Scenes/W02AssetInspection.unity";
        internal const string InventoryPath = "ArtSource/Developer1/W01/inventory.json";
        internal const float MeterTolerance = .001f;

        [Serializable] internal sealed class Inventory { public Entry[] entries; }
        [Serializable] internal sealed class Entry
        {
            public string id;
            public string path;
            public float[] dimensionsMetersUnityXYZ;
            public Vector3 Size => new Vector3(dimensionsMetersUnityXYZ[0],
                dimensionsMetersUnityXYZ[1], dimensionsMetersUnityXYZ[2]);
            public string Stem => Path.GetFileNameWithoutExtension(path);
            public string PrefabPath => $"{PrefabsFolder}/{Stem}.prefab";
            public string MaterialName => Stem.StartsWith("rock-", StringComparison.Ordinal) ? "Rock" : Stem switch
            {
                "wall-straight-4m" => "Wall",
                "floor-4m" => "Floor",
                "equipment-rack" or "lab-bench" => "Equipment",
                "solar-panel" => "SolarPanel",
                _ => throw new InvalidDataException("No inspection material for " + path)
            };
        }

        private static readonly Vector3[] Positions =
        {
            new Vector3(2, 0, 2.6f), new Vector3(-2.8f, .2f, 4),
            new Vector3(2.9f, 0, 2.2f), new Vector3(4.6f, 0, 2.1f),
            new Vector3(-2.1f, 0, -.8f), new Vector3(.2f, 0, -.9f),
            new Vector3(1.6f, 0, -.9f), new Vector3(3.1f, 0, -.9f)
        };

        [MenuItem("LKS/World/Create W02 Asset Inspection")]
        public static void Generate()
        {
            var entries = ReadEntries();
            foreach (var entry in entries)
                GetModel(entry);

            EnsureFolder(W02ImportRules.TextureFolder);
            EnsureFolder(MaterialsFolder);
            EnsureFolder(PrefabsFolder);
            EnsurePanelTextures();
            CreateMaterial("Wall", new Color(.47f, .49f, .50f), .32f, .2f);
            CreateMaterial("Floor", new Color(.25f, .28f, .30f), .30f, .1f);
            CreateMaterial("Equipment", new Color(.42f, .44f, .42f), .35f, .15f);
            CreateMaterial("SolarPanel", Color.white, .6f, 1);
            CreateMaterial("Rock", new Color(.42f, .25f, .16f), .18f, 0);
            CreateMaterial("Reference", new Color(.85f, .36f, .075f), .25f, 0);
            CreateMaterial("Ground", new Color(.17f, .19f, .22f), .2f, 0);

            foreach (var entry in entries)
                if (AssetDatabase.LoadAssetAtPath<GameObject>(entry.PrefabPath) == null)
                {
                    RequireVacantPath(entry.PrefabPath);
                    CreatePrefab(entry);
                }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                RequireVacantPath(ScenePath);
                CreateScene(entries);
            }
            Debug.Log("W02_GENERATE_PASS materials, wrappers and inspection scene available.");
        }

        internal static Entry[] ReadEntries()
        {
            var inventory = JsonUtility.FromJson<Inventory>(File.ReadAllText(InventoryPath));
            if (inventory?.entries?.Length != Positions.Length)
                throw new InvalidDataException("Expected the eight W01 inspection assets.");
            foreach (var entry in inventory.entries)
            {
                string extension = Path.GetExtension(entry.path);
                if (!entry.path.StartsWith(W02ImportRules.ModelFolder, StringComparison.Ordinal) ||
                    (extension != ".fbx" && extension != ".obj") ||
                    string.IsNullOrEmpty(entry.id) || entry.dimensionsMetersUnityXYZ?.Length != 3)
                    throw new InvalidDataException("Unsupported W01 inventory entry: " + entry.path);
                _ = entry.MaterialName;
            }
            return inventory.entries;
        }

        private static GameObject GetModel(Entry entry)
        {
            if (AssetImporter.GetAtPath(entry.path) is not ModelImporter)
                throw new InvalidDataException("Expected a Unity model importer: " + entry.path);
            return AssetDatabase.LoadAssetAtPath<GameObject>(entry.path)
                ?? throw new InvalidDataException("Unable to load model: " + entry.path);
        }

        internal static Bounds ModelBounds(GameObject visual, Transform frame, Vector3 expected)
        {
            bool initialized = false;
            var result = new Bounds();
            foreach (var filter in visual.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh;
                if (mesh == null || mesh.vertexCount == 0 || mesh.uv.Length != mesh.vertexCount ||
                    mesh.uv2.Length != mesh.vertexCount || mesh.normals.Length != mesh.vertexCount)
                    throw new InvalidDataException("Expected geometry, normals, UV0 and lightmap UV2 on " + filter.name);

                foreach (var vertex in mesh.vertices)
                {
                    var point = frame.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                    if (initialized) result.Encapsulate(point);
                    else { result = new Bounds(point, Vector3.zero); initialized = true; }
                }
            }
            if (!initialized || Vector3.Distance(result.size, expected) > MeterTolerance)
                throw new InvalidDataException($"Imported bounds {result.size:F4} differ from {expected:F4}.");
            return result;
        }

        private static void CreatePrefab(Entry entry)
        {
            var root = new GameObject(entry.id);
            try
            {
                var visual = Child(root.transform, "Visual", Vector3.zero);
                var imported = (GameObject)PrefabUtility.InstantiatePrefab(GetModel(entry), visual);
                var bounds = ModelBounds(imported, root.transform, entry.Size);
                AssignMaterial(imported, LoadMaterial(entry.MaterialName));

                var anchors = Child(root.transform, "Anchors", Vector3.zero);
                Child(anchors, "SnapStart", Vector3.zero);
                if (entry.Stem == "wall-straight-4m")
                {
                    if (Vector3.Distance(bounds.max, new Vector3(0, 3, .1f)) > MeterTolerance ||
                        Vector3.Distance(bounds.min, new Vector3(-4, 0, -.1f)) > MeterTolerance)
                        throw new InvalidDataException("Wall import pivot or orientation changed.");
                    Child(anchors, "SnapEnd", new Vector3(-4, 0, 0));
                }
                Child(anchors, "InspectPoint", new Vector3(bounds.center.x, bounds.center.y, bounds.min.z - .04f));
                CreateCollision(Child(root.transform, "Collision", Vector3.zero), entry.Stem, bounds);

                if (PrefabUtility.SaveAsPrefabAsset(root, entry.PrefabPath) == null)
                    throw new IOException("Unable to save " + entry.PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void CreateCollision(Transform parent, string stem, Bounds bounds)
        {
            if (stem == "equipment-rack")
            {
                Box(parent, "Left upright", new Vector3(-.36f, 1, 0), new Vector3(.08f, 2, .6f));
                Box(parent, "Right upright", new Vector3(.36f, 1, 0), new Vector3(.08f, 2, .6f));
                for (int shelf = 0; shelf < 4; shelf++)
                    Box(parent, $"Shelf {shelf + 1}", new Vector3(0, .04f + shelf * .64f, 0),
                        new Vector3(.64f, .08f, .6f));
            }
            else if (stem == "lab-bench")
            {
                Box(parent, "Top", new Vector3(0, .85f, 0), new Vector3(2, .1f, .8f));
                Box(parent, "Left leg", new Vector3(-.84f, .4f, 0), new Vector3(.12f, .8f, .65f));
                Box(parent, "Right leg", new Vector3(.84f, .4f, 0), new Vector3(.12f, .8f, .65f));
            }
            else
                Box(parent, "Bounds", bounds.center, bounds.size);
        }

        private static void Box(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var collider = Child(parent, name, center).gameObject.AddComponent<BoxCollider>();
            collider.size = size;
        }

        private static Transform Child(Transform parent, string name, Vector3 localPosition)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = localPosition;
            return child;
        }

        private static void CreateScene(Entry[] entries)
        {
            var previous = SceneManager.GetActiveScene();
            var mode = NewSceneMode.Additive;
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                var openScene = SceneManager.GetSceneAt(index);
                if (!string.IsNullOrEmpty(openScene.path)) continue;
                if (!Application.isBatchMode || openScene.isDirty || SceneManager.sceneCount != 1)
                    throw new InvalidOperationException("Save the untitled scene before creating the W02 inspection scene.");
                mode = NewSceneMode.Single;
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
            try
            {
                SceneManager.SetActiveScene(scene);
                var samples = new GameObject("W02 Asset Samples");
                for (int index = 0; index < entries.Length; index++)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entries[index].PrefabPath);
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, samples.transform);
                    instance.name = entries[index].id;
                    instance.transform.localPosition = Positions[index];
                }
                Primitive("One meter reference", new Vector3(-4.55f, .5f, -.65f), Vector3.one, "Reference");
                Primitive("Inspection ground", new Vector3(0, -.125f, 1), new Vector3(28, .25f, 18), "Ground");
                Light("Key light", new Vector3(43, -28, 0), 1.5f, LightShadows.Soft);
                Light("Fill light", new Vector3(25, 151, 0), .50f, LightShadows.None);

                var camera = new GameObject("Inspection Camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.transform.position = new Vector3(5.4f, 6.8f, -9.7f);
                camera.transform.LookAt(new Vector3(-.2f, 1, 1.1f));
                camera.orthographic = true;
                camera.orthographicSize = 4.9f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.11f, .14f, .18f);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new IOException("Unable to save inspection scene.");
            }
            finally
            {
                if (mode == NewSceneMode.Additive)
                {
                    if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void Light(string name, Vector3 euler, float intensity, LightShadows shadows)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(euler);
            light.intensity = intensity;
            light.shadows = shadows;
        }

        private static void Primitive(string name, Vector3 position, Vector3 scale, string material)
        {
            var sample = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sample.name = name;
            sample.transform.position = position;
            sample.transform.localScale = scale;
            AssignMaterial(sample, LoadMaterial(material));
        }

        private static void AssignMaterial(GameObject root, Material material)
        {
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                var slots = renderer.sharedMaterials;
                for (int index = 0; index < slots.Length; index++)
                    slots[index] = material;
                renderer.sharedMaterials = slots;
            }
        }

        internal static Material LoadMaterial(string name) =>
            AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/{name}.mat")
            ?? throw new InvalidDataException("Inspection material missing: " + name);

        private static void CreateMaterial(string name, Color color, float smoothness, float metallic)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            RequireVacantPath(path);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is unavailable.");
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            if (name == "SolarPanel")
            {
                material.SetTexture("_BaseMap", RequireTexture(W02ImportRules.PanelColor));
                material.SetTexture("_BumpMap", RequireTexture(W02ImportRules.PanelNormal));
                material.SetTexture("_MetallicGlossMap", RequireTexture(W02ImportRules.PanelMask));
                material.SetFloat("_SmoothnessTextureChannel", 0);
                material.SetFloat("_BumpScale", .35f);
                material.EnableKeyword("_NORMALMAP");
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            AssetDatabase.CreateAsset(material, path);
        }

        private static Texture2D RequireTexture(string path) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>(path)
            ?? throw new InvalidDataException("Inspection texture missing: " + path);

        private static void EnsurePanelTextures()
        {
            CreateTexture(W02ImportRules.PanelColor, (x, y) =>
            {
                bool frame = x % 64 < 3 || y % 64 < 3;
                byte trace = (byte)(y % 8 == 4 ? 13 : 0);
                return frame ? new Color32(126, 132, 141, 255)
                    : new Color32((byte)(24 + trace), (byte)(49 + trace), (byte)(81 + trace), 255);
            });
            CreateTexture(W02ImportRules.PanelNormal, (x, y) => new Color32(
                (byte)(x % 64 == 3 ? 104 : x % 64 == 63 ? 152 : 128),
                (byte)(y % 64 == 3 ? 104 : y % 64 == 63 ? 152 : 128), 253, 255));
            CreateTexture(W02ImportRules.PanelMask, (x, y) =>
            {
                bool frame = x % 64 < 3 || y % 64 < 3;
                byte metallic = frame ? (byte)205 : (byte)12;
                byte roughness = frame ? (byte)102 : (byte)54;
                return new Color32(metallic, 0, 0, (byte)(255 - roughness));
            });
        }

        private static void CreateTexture(string path, Func<int, int, Color32> pixel)
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null) return;
            RequireVacantPath(path);
            const int size = 256;
            var image = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            try
            {
                var colors = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                        colors[y * size + x] = pixel(x, y);
                image.SetPixels32(colors);
                image.Apply();
                using var stream = new FileStream(path, FileMode.CreateNew);
                var png = image.EncodeToPNG();
                stream.Write(png, 0, png.Length);
            }
            finally { Object.DestroyImmediate(image); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path[..path.LastIndexOf('/')];
            EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(path))))
                throw new IOException("Unable to create " + path);
        }

        private static void RequireVacantPath(string path)
        {
            if (File.Exists(path) || Directory.Exists(path) || File.Exists(path + ".meta"))
                throw new IOException("Refusing to overwrite an existing asset or metadata: " + path);
        }
    }
}
