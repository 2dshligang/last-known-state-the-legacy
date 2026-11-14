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
    public static class W03StationKit
    {
        internal const string Prefabs = "Assets/_Game/Prefabs/W03";
        internal const string Materials = "Assets/_Game/Art/W03/Materials";
        internal const string ScenePath = "Assets/_Game/Scenes/W03CorridorKit.unity";
        private const string Source = "ArtSource/Developer1/W03/";
        private const float LeafClosedX = -.35f;
        private const float LeafOpenX = 1.20f;
        private const float LeafTrackZ = -.232f;

        [Serializable] internal sealed class ModelSpec { public Model[] assets; }
        [Serializable] internal sealed class Model { public string name; public Part[] parts; }
        [Serializable] internal sealed class Part
        {
            public string name, surface;
            public float[] center, size;
            public Vector3 Center => ToVector(center);
            public Vector3 Size => ToVector(size);
            private static Vector3 ToVector(float[] value) => new Vector3(value[0], value[1], value[2]);
        }

        private sealed class Module
        {
            public readonly string Name, Id, Use;
            public readonly Vector3 Size, Start, End, Inspect;
            public readonly Action<GameObject> Build;
            public Module(string name, string id, Vector3 size, Vector3 start, Vector3 end,
                Vector3 inspect, string use, Action<GameObject> build)
            {
                Name = name; Id = id; Size = size; Start = start; End = end;
                Inspect = inspect; Use = use; Build = build;
            }
        }

        private static readonly Module[] Modules =
        {
            new Module("Corridor", "module.corridor", V(4.4f,3.4f,4), V(0,0,0), V(0,0,4),
                V(0,1,2), "Level 4 m bay with floor, ceiling and two side walls.", BuildCorridor),
            new Module("Airlock", "module.airlock", V(4,3,.52f), V(0,0,0), V(0,0,0),
                V(-.35f,1.05f,-.62f), "1.4 x 2.2 m clear passage when open; +X sliding leaf.", BuildAirlock),
            new Module("UtilityCabinet", "module.utility-cabinet", V(.8f,1.35f,.50f),
                V(0,0,0), V(0,0,0), V(0,1.04f,-.31f), "Wall-side power service location.", BuildCabinet),
            new Module("LabWorkstation", "module.lab-workstation", V(1.8f,1.17f,.66f),
                V(0,0,0), V(0,0,0), V(0,1.02f,-.42f), "Open-leg bench with recessed equipment top.", BuildLab),
            new Module("EchoTerminal", "module.echo-terminal", V(.78f,1.605f,.507f),
                V(0,0,0), V(0,0,0), V(0,1.33f,-.34f), "Archive and ECHO terminal; screen faces local -Z.", BuildEcho),
            new Module("Shelter", "module.shelter", V(1.12f,1.62f,.66f),
                V(0,0,0), V(0,0,0), V(0,.82f,-.43f), "Recessed bench and canopy within an existing bay.", BuildShelter)
        };

        internal static string ModelPath(string model) => $"Assets/_Game/Art/W03/Models/{model}.fbx";
        internal static string PrefabPath(string module) => $"{Prefabs}/{module}.prefab";
        internal static Model[] ReadModels() =>
            JsonUtility.FromJson<ModelSpec>(File.ReadAllText(Source + "mesh-spec.json")).assets;

        [MenuItem("LKS/World/Create W03 Station Kit")]
        public static void Generate()
        {
            EnsureFolder(Prefabs);
            EnsureFolder(Materials);
            CreateMaterial("Shell", new Color(.51f,.49f,.43f), .24f, .05f);
            CreateMaterial("Metal", new Color(.22f,.25f,.25f), .31f, .22f);
            CreateMaterial("Ochre", new Color(.66f,.31f,.09f), .22f, .04f);
            CreateMaterial("Stone", new Color(.27f,.24f,.20f), .12f, 0);
            CreateMaterial("Screen", new Color(.052f,.09f,.10f), .28f, .04f);
            CreateMaterial("Status", new Color(.10f,.38f,.42f), .18f, 0);
            CreateMaterial("Label", new Color(.055f,.070f,.076f), .16f, 0);
            CreateLabelMaterial();

            StableId.ValidateUnique(Modules.Select(item => new StableId(item.Id)));
            foreach (var module in Modules)
            {
                var path = PrefabPath(module.Name);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) continue;
                RequireVacant(path);
                BuildModule(module);
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                RequireVacant(ScenePath);
                BuildScene();
            }
            WriteInventory();
            Debug.Log("W03_GENERATE_PASS six modules and corridor sample available.");
        }

        private static void BuildModule(Module module)
        {
            var root = new GameObject(module.Name);
            try
            {
                Child(root.transform, "Visual", Vector3.zero);
                Child(root.transform, "Collision", Vector3.zero);
                var anchors = Child(root.transform, "Anchors", Vector3.zero);
                var start = Child(anchors, "SnapStart", module.Start);
                var end = Child(anchors, "SnapEnd", module.End);
                var inspect = Child(anchors, "InspectPoint", module.Inspect);
                module.Build(root);
                var serialized = new SerializedObject(root.AddComponent<StationModule>());
                serialized.FindProperty("moduleId").FindPropertyRelative("value").stringValue = module.Id;
                Set(serialized, "snapStart", start);
                Set(serialized, "snapEnd", end);
                Set(serialized, "inspectPoint", inspect);
                var transforms = root.GetComponentsInChildren<Transform>(true);
                SetVisuals(serialized, "authoredVisuals", transforms.Where(item => item.name == "Authored"));
                SetVisuals(serialized, "fallbackVisuals", transforms.Where(item => item.name == "Fallback"));
                serialized.ApplyModifiedPropertiesWithoutUndo();
                if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(module.Name)) == null)
                    throw new IOException("Unable to save " + module.Name);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void SetVisuals(SerializedObject target, string name, System.Collections.Generic.IEnumerable<Transform> items)
        {
            var objects = items.Select(item => item.gameObject).ToArray();
            var property = target.FindProperty(name);
            property.arraySize = objects.Length;
            for (int i = 0; i < objects.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = objects[i];
        }

        private static void Set(SerializedObject target, string field, Object value) =>
            target.FindProperty(field).objectReferenceValue = value;

        private static void BuildCorridor(GameObject root)
        {
            var visual = root.transform.Find("Visual");
            var collision = root.transform.Find("Collision");
            Geometry(visual, collision, "Floor", V(0,-.1f,2), V(4.4f,.2f,4), "Stone");
            Geometry(visual, collision, "Ceiling", V(0,3.1f,2), V(4.4f,.2f,4), "Metal");
            foreach (int side in new[] {-1, 1})
            {
                var prefix = side < 0 ? "Left" : "Right";
                Geometry(visual, collision, prefix+" wall", V(side*2.1f,1.5f,2), V(.2f,3,4), "Shell");
                Cube(visual, prefix+" kick strip", V(side*1.989f,.19f,2), V(.022f,.31f,4), "Metal");
                Cube(visual, prefix+" guide stripe", V(side*1.986f,1.94f,2), V(.018f,.07f,4), "Ochre");
                Cube(visual, prefix+" conduit", V(side*1.90f,2.78f,2), V(.14f,.105f,4), "Metal");
                for (int i = 0; i < 2; i++)
                {
                    Cube(visual, $"{prefix} seam {i+1}", V(side*1.986f,1.2f,.035f+i*2), V(.022f,2.40f,.03f), "Metal");
                }
            }
            Cube(visual, "Floor guide strip", V(-.35f,.001f,2), V(.055f,.002f,4), "Ochre");
        }

        private static void BuildAirlock(GameObject root)
        {
            var visual = root.transform.Find("Visual");
            var collision = root.transform.Find("Collision");
            Variants(visual, "AirlockFrame");
            Box(collision, "Left jamb", V(-1.525f,1.1f,0), V(.95f,2.2f,.2f));
            Box(collision, "Right jamb", V(1.175f,1.1f,0), V(1.65f,2.2f,.2f));
            Box(collision, "Lintel", V(0,2.6f,0), V(4,.8f,.2f));
            Cube(visual, "Track", V(.44f,2.31f,-.232f), V(3.2f,.08f,.18f), "Metal");
            Cube(visual, "Panel surround", V(-1.62f,1.20f,-.145f), V(.28f,.31f,.08f), "Metal");
            Cube(visual, "Panel status", V(-1.62f,1.24f,-.189f), V(.21f,.11f,.02f), "Status");
            Cube(visual, "Status plate", V(-.35f,2.56f,-.125f), V(1.7f,.29f,.04f), "Label");
            var status = Label(visual, "Status", "AIRLOCK  /  CLOSED", V(-.35f,2.56f,-.149f), .25f);
            var closed = Child(root.transform, "ClosedPose", V(LeafClosedX,0,LeafTrackZ));
            var open = Child(root.transform, "OpenPose", V(LeafOpenX,0,LeafTrackZ));
            var leaf = Child(root.transform, "SlidingLeaf", closed.localPosition);
            Variants(Child(leaf, "Visual", Vector3.zero), "AirlockLeaf");
            Box(leaf, "Leaf collider", V(0,1.11f,0), V(1.48f,2.18f,.12f));
            var serialized = new SerializedObject(root.AddComponent<AirlockPresentation>());
            Set(serialized, "leaf", leaf);
            Set(serialized, "closedPose", closed);
            Set(serialized, "openPose", open);
            Set(serialized, "statusLabel", status);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildCabinet(GameObject root)
        {
            var visual = root.transform.Find("Visual");
            var collision = root.transform.Find("Collision");
            Geometry(visual, collision, "Cabinet shell", V(0,.675f,.025f), V(.80f,1.35f,.45f), "Metal");
            Cube(visual,"Recessed face",V(0,.68f,-.212f),V(.71f,1.18f,.024f),"Shell");
            Cube(visual,"Handle",V(.24f,.62f,-.241f),V(.065f,.31f,.034f),"Ochre");
            Cube(visual,"Utility plate",V(0,1.10f,-.226f),V(.61f,.20f,.02f),"Label");
            Label(visual,"Utility label","UTILITY",V(0,1.10f,-.239f),.20f);
        }

        private static void BuildLab(GameObject root)
        {
            var v = root.transform.Find("Visual");
            var c = root.transform.Find("Collision");
            Geometry(v,c,"Bench top",V(0,.88f,0),V(1.8f,.12f,.66f),"Metal");
            Geometry(v,c,"Left leg",V(-.78f,.41f,0),V(.13f,.82f,.56f),"Shell");
            Geometry(v,c,"Right leg",V(.78f,.41f,0),V(.13f,.82f,.56f),"Shell");
            Geometry(v,c,"Rear console",V(0,1.055f,.215f),V(1.78f,.23f,.19f),"Shell");
            Cube(v,"Bench trim",V(0,.886f,-.338f),V(1.72f,.042f,.014f),"Ochre");
            Cube(v,"Lab plate",V(0,1.054f,.113f),V(.97f,.134f,.014f),"Label");
            Label(v,"Lab label","LAB WORKSTATION",V(0,1.054f,.103f),.135f);
        }

        private static void BuildEcho(GameObject root)
        {
            var visual = root.transform.Find("Visual");
            var collision = root.transform.Find("Collision");
            Variants(visual, "EchoHousing");
            Box(collision, "Foot", V(0,.08f,0), V(.78f,.16f,.5f));
            Box(collision, "Stand", V(0,.51f,.064f), V(.30f,.85f,.30f));
            Box(collision, "Housing", V(0,1.27f,-.024f), V(.78f,.67f,.468f));
            Label(visual,"Screen label","ECHO\nARCHIVE",V(0,1.34f,-.262f),.19f);
        }

        private static void BuildShelter(GameObject root)
        {
            var v = root.transform.Find("Visual");
            var c = root.transform.Find("Collision");
            Geometry(v,c,"Bench",V(0,.25f,.035f),V(1.12f,.50f,.59f),"Metal");
            Cube(v,"Seat surface",V(0,.516f,.035f),V(1.04f,.032f,.55f),"Stone");
            Geometry(v,c,"Back",V(0,1.05f,.29f),V(1.12f,1.1f,.08f),"Shell");
            Geometry(v,c,"Canopy",V(0,1.565f,.035f),V(1.12f,.11f,.59f),"Metal");
            Cube(v,"Shelter plate",V(0,1.56f,-.274f),V(.83f,.13f,.024f),"Label");
            Label(v,"Shelter label","SHELTER",V(0,1.56f,-.291f),.15f);
        }

        private static void Variants(Transform parent, string name)
        {
            var spec = ReadModels().Single(model => model.name == name);
            var authored = Child(parent, "Authored", Vector3.zero);
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(name))
                ?? throw new InvalidDataException("Missing Blender export " + name);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, authored);
            foreach (var part in spec.parts)
            {
                var filter = visual.GetComponentsInChildren<MeshFilter>().Single(item => item.name == part.name);
                var mesh = filter.sharedMesh;
                if (mesh.uv.Length != mesh.vertexCount || mesh.uv2.Length != mesh.vertexCount ||
                    mesh.normals.Length != mesh.vertexCount)
                    throw new InvalidDataException("Missing model UVs or normals for " + part.name);
                filter.GetComponent<MeshRenderer>().sharedMaterial = Material(part.surface);
            }
            var fallback = Child(parent, "Fallback", Vector3.zero);
            foreach (var part in spec.parts) Cube(fallback, part.name, part.Center, part.Size, part.surface);
            fallback.gameObject.SetActive(false);
        }

        private static void BuildScene()
        {
            var previous = SceneManager.GetActiveScene();
            bool untitled = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt).Any(scene => string.IsNullOrEmpty(scene.path));
            if (untitled && (!Application.isBatchMode || SceneManager.sceneCount != 1 || previous.isDirty))
                throw new InvalidOperationException("Save the untitled scene before creating W03.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                untitled ? NewSceneMode.Single : NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var samples = new GameObject("W03 Samples").transform;
                Place(samples, "Corridor", "Corridor A", Vector3.zero, 0);
                Place(samples, "Corridor", "Corridor B", V(0,0,4), 0);
                var door = Place(samples, "Airlock", "Airlock", V(0,0,4), 0);
                door.GetComponent<AirlockPresentation>().ApplyOpen(true);
                Place(samples, "UtilityCabinet", "Utility Cabinet", V(-1.60f,0,1.28f), -90);
                Place(samples, "EchoTerminal", "ECHO Terminal", V(1.58f,0,2.27f), 90);
                Place(samples, "LabWorkstation", "Lab Workstation", V(1.52f,0,6.36f), 90);
                Place(samples, "Shelter", "Shelter", V(-1.53f,0,6.27f), -90);
                var finish = new GameObject("Sample end wall").transform;
                Geometry(finish, finish, "Bulkhead", V(0,1.5f,8.10f), V(4.4f,3,.2f), "Shell");
                Cube(finish, "End guide stripe", V(0,1.94f,7.989f), V(4,.07f,.018f), "Ochre");
                Light("Entry soft light", V(0,2.38f,1.35f), 3.7f);
                Light("Airlock soft light", V(-.50f,2.40f,3.25f), 1.9f);
                Light("Far bay soft light", V(0,2.4f,6.12f), 4.5f);
                var key = new GameObject("Ambient directional").AddComponent<Light>();
                key.type = LightType.Directional;
                key.intensity = .39f;
                key.transform.rotation = Quaternion.Euler(24,-34,0);
                var camera = new GameObject("Kit Camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.transform.position = V(-.34f,1.55f,.23f);
                camera.transform.LookAt(V(-.23f,1.34f,4.4f));
                camera.fieldOfView = 66;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.16f,.19f,.20f);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new IOException("Unable to save the corridor kit scene.");
            }
            finally
            {
                if (!untitled)
                {
                    if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static GameObject Place(Transform parent, string module, string name, Vector3 position, float angle)
        {
            var item = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(module)), parent);
            item.name = name;
            item.transform.SetLocalPositionAndRotation(position, Quaternion.Euler(0, angle, 0));
            return item;
        }

        private static void Light(string name, Vector3 position, float intensity)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.type = LightType.Point;
            light.transform.position = position;
            light.intensity = intensity;
            light.range = 5;
            light.shadows = LightShadows.Soft;
        }

        private static void WriteInventory()
        {
            string path = Source + "module-inventory.json";
            if (File.Exists(path)) return;
            var rows = Modules.Select(m => new InventoryRow
            {
                id = m.Id, prefab = PrefabPath(m.Name), width = m.Size.x,
                height = m.Size.y, depth = m.Size.z, snapStart = m.Start,
                snapEnd = m.End, inspectPoint = m.Inspect, usage = m.Use
            }).ToArray();
            var inventory = new Inventory { modules = rows };
            using var output = new StreamWriter(new FileStream(path, FileMode.CreateNew));
            output.WriteLine(JsonUtility.ToJson(inventory, true));
        }

        [Serializable] private sealed class Inventory { public string units = "meters"; public InventoryRow[] modules; }
        [Serializable] private sealed class InventoryRow
        {
            public string id, prefab, usage;
            public float width, height, depth;
            public Vector3 snapStart, snapEnd, inspectPoint;
        }

        private static void Geometry(Transform visual, Transform collision, string name, Vector3 center, Vector3 size, string material)
        {
            Cube(visual, name, center, size, material);
            Box(collision, name, center, size);
        }

        private static void Cube(Transform parent, string name, Vector3 center, Vector3 size, string material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(cube.GetComponent<Collider>());
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = center;
            cube.transform.localScale = size;
            cube.GetComponent<MeshRenderer>().sharedMaterial = Material(material);
        }

        private static void Box(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var box = Child(parent, name, center).gameObject.AddComponent<BoxCollider>();
            box.size = size;
        }

        private static StationLabel Label(Transform parent, string name, string text, Vector3 position, float height)
        {
            var result = Child(parent, name, position).gameObject.AddComponent<TextMesh>();
            result.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            result.GetComponent<MeshRenderer>().sharedMaterial = Material("WorldText");
            result.text = text;
            result.anchor = TextAnchor.MiddleCenter;
            result.alignment = TextAlignment.Center;
            result.fontSize = 64;
            result.characterSize = height / 10;
            result.color = new Color(.91f,.94f,.92f);
            return result.gameObject.AddComponent<StationLabel>();
        }

        private static Transform Child(Transform parent, string name, Vector3 position)
        {
            var item = new GameObject(name).transform;
            item.SetParent(parent, false);
            item.localPosition = position;
            return item;
        }

        private static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        private static Material Material(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{Materials}/{name}.mat")
            ?? throw new IOException("Missing W03 material: " + name);

        private static void CreateMaterial(string name, Color color, float smoothness, float metallic)
        {
            var path = $"{Materials}/{name}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            RequireVacant(path);
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? throw new InvalidOperationException("URP Lit is not available.");
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            AssetDatabase.CreateAsset(material, path);
        }

        private static void CreateLabelMaterial()
        {
            string path = Materials + "/WorldText.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            RequireVacant(path);
            var shader = Shader.Find("LKS/Station/Inspection Label");
            if (shader == null) throw new InvalidOperationException("W03 label shader is missing.");
            AssetDatabase.CreateAsset(new Material(shader) { name = "WorldText" }, path);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = path[..path.LastIndexOf('/')];
            EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(path))))
                throw new IOException("Unable to create " + path);
        }

        private static void RequireVacant(string path)
        {
            if (File.Exists(path) || Directory.Exists(path) || File.Exists(path + ".meta"))
                throw new IOException("Refusing to overwrite " + path);
        }
    }
}
