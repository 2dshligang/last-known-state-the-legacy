using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LKS.Editor.World
{
    public static partial class W15AVisualSetup
    {
        private const string MetalMap = "metal_plate_nor_gl_1k.jpg";
        private const string GroundMap = "rough_concrete_nor_gl_1k.jpg";
        private const string MetalDigest = "994f851a84d52b51e97906021a1b7070ee6eac9a789fef041132268de47d8e6b";
        private const string GroundDigest = "54c85854ad5cafb16840df91a605c857bdf3d07fa79f11921c8327fccf6423ae";

        private static readonly (string Name, Color Color, float Metal, float Smooth)[] SurfacePalette =
        {
            ("Porcelain", new Color(.73f,.72f,.65f), .02f, .26f),
            ("Graphite", new Color(.22f,.28f,.30f), .18f, .26f),
            ("Alloy", new Color(.43f,.51f,.51f), .33f, .31f),
            ("Seal", new Color(.11f,.14f,.15f), 0, .11f),
            ("Ochre", new Color(.82f,.45f,.18f), .06f, .23f),
            ("Petrol", new Color(.18f,.47f,.51f), .10f, .26f),
            ("Diffuser", new Color(.69f,.80f,.77f), 0, .21f),
            ("Tray", new Color(.30f,.38f,.36f), 0, .27f),
            ("Leaf", new Color(.36f,.54f,.28f), 0, .19f),
            ("Stone", new Color(.56f,.32f,.23f), 0, .16f),
            ("Stratum", new Color(.65f,.40f,.27f), 0, .16f),
            ("Rind", new Color(.44f,.26f,.20f), 0, .12f),
            ("Dust", new Color(.58f,.35f,.25f), 0, .14f)
        };

        private static Dictionary<string, GameObject> PreparePrefabs()
        {
            EnsureFolder(Prefabs);
            EnsureFolder(Art + "/Materials");
            var shader = Shader.Find("Universal Render Pipeline/Lit") ??
                throw new InvalidOperationException("URP Lit is required for W15A visuals.");
            var palette = SurfacePalette.ToDictionary(entry => entry.Name, entry =>
            {
                string path = $"{Art}/Materials/{entry.Name}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material != null) return material;
                RequireVacant(path);
                material = new Material(shader) { name = entry.Name, color = entry.Color };
                material.SetFloat("_Metallic", entry.Metal);
                material.SetFloat("_Smoothness", entry.Smooth);
                if (entry.Name == "Diffuser")
                {
                    material.SetColor("_EmissionColor", new Color(.19f, .26f, .23f));
                    material.EnableKeyword("_EMISSION");
                }
                AssetDatabase.CreateAsset(material, path);
                return material;
            });
            var prefabs = new Dictionary<string, GameObject>();
            foreach (string name in Models)
            {
                var path = $"{Art}/Original/{name}.fbx";
                if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
                    throw new InvalidOperationException("Missing original W15A FBX: " + path);
                if (!importer.bakeAxisConversion || importer.importAnimation || importer.addCollider)
                {
                    importer.bakeAxisConversion = true;
                    importer.importAnimation = false;
                    importer.addCollider = false;
                    importer.SaveAndReimport();
                }

                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var renderer = model?.GetComponentInChildren<MeshRenderer>();
                var mesh = renderer?.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null || mesh.bounds.size.y <= 0 ||
                    !mesh.HasVertexAttribute(VertexAttribute.TexCoord0) ||
                    !mesh.HasVertexAttribute(VertexAttribute.Normal) ||
                    renderer.sharedMaterials.Length != mesh.subMeshCount ||
                    (name.StartsWith("Wall", StringComparison.Ordinal) && Mathf.Abs(mesh.bounds.size.y - 2.98f) > .25f))
                    throw new InvalidOperationException($"Unexpected mesh units, normals, UVs or material slots: {name}.");
                string prefabPath = $"{Prefabs}/{name}.prefab";
                var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (existing == null)
                {
                    RequireVacant(prefabPath);
                    var temp = new GameObject(name);
                    try
                    {
                        temp.AddComponent<MeshFilter>().sharedMesh = mesh;
                        var visual = temp.AddComponent<MeshRenderer>();
                        visual.sharedMaterials = renderer.sharedMaterials.Select(source => palette[source.name]).ToArray();
                        visual.shadowCastingMode = ShadowCastingMode.Off;
                        existing = PrefabUtility.SaveAsPrefabAsset(temp, prefabPath);
                    }
                    finally { Object.DestroyImmediate(temp); }
                }
                prefabs.Add(name, LoadPrefab(name));
            }
            return prefabs;
        }

        private static GameObject LoadPrefab(string name)
        {
            string path = $"{Prefabs}/{name}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var mesh = prefab != null ? prefab.GetComponent<MeshFilter>()?.sharedMesh : null;
            if (mesh == null || !prefab.TryGetComponent<MeshRenderer>(out var renderer) ||
                renderer.sharedMaterials.Length != mesh.subMeshCount ||
                prefab.GetComponentsInChildren<Collider>(true).Length != 0 ||
                prefab.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                throw new InvalidOperationException("Missing or unexpected visual-only prefab: " + path);
            return prefab;
        }

        private static void SetBaseMaterials()
        {
            var floorNormal = PrepareNormal(MetalMap, MetalDigest);
            var groundNormal = PrepareNormal(GroundMap, GroundDigest);
            UpdateMaterial("Floor", new Color(.33f, .37f, .37f), .20f, .29f, floorNormal, .41f, 7);
            UpdateMaterial("Dust", new Color(.58f, .34f, .23f), .0f, .17f, groundNormal, .48f, 8);
            UpdateMaterial("Shell", new Color(.64f, .61f, .54f), .02f, .23f, null, 0, 1);
            UpdateMaterial("Metal", new Color(.26f, .31f, .33f), .12f, .26f, null, 0, 1);
            UpdateMaterial("Ochre", new Color(.83f, .48f, .19f), .03f, .22f, null, 0, 1);
        }

        private static Texture2D PrepareNormal(string name, string digest)
        {
            string path = $"{Art}/External/{name}";
            if (!File.Exists(path))
            {
                Debug.LogWarning("Using the original solid surface because the approved normal map is absent: " + name);
                return null;
            }
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                var actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                if (actual != digest) throw new InvalidOperationException("Unapproved external texture bytes: " + path);
            }
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                throw new InvalidOperationException("Import the approved texture first: " + path);
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            if (width != 1024 || height != 1024) throw new InvalidOperationException("Expected a 1K normal map: " + path);
            if (importer.textureType != TextureImporterType.NormalMap || importer.maxTextureSize != 1024)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void UpdateMaterial(string name, Color color, float metal, float smooth,
            Texture2D normal, float strength, float tiles)
        {
            var path = $"Assets/_Game/Art/W04/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path) ??
                throw new InvalidOperationException("Missing known W04 material: " + path);
            var tiling = new Vector2(tiles, tiles);
            bool mapped = normal != null;
            float bump = mapped ? strength : 0;
            if (material.color == color && Mathf.Approximately(material.GetFloat("_Metallic"), metal) &&
                Mathf.Approximately(material.GetFloat("_Smoothness"), smooth) &&
                material.GetTexture("_BumpMap") == normal && material.GetTextureScale("_BaseMap") == tiling &&
                Mathf.Approximately(material.GetFloat("_BumpScale"), bump) &&
                material.IsKeywordEnabled("_NORMALMAP") == mapped) return;
            material.color = color;
            material.SetFloat("_Metallic", metal);
            material.SetFloat("_Smoothness", smooth);
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", bump);
            material.SetTextureScale("_BaseMap", tiling);
            if (mapped) material.EnableKeyword("_NORMALMAP");
            else material.DisableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(material);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        private static void RequireVacant(string path)
        {
            if (File.Exists(path) || File.Exists(path + ".meta") || Directory.Exists(path))
                throw new InvalidOperationException("Unrecognized file at visual asset path: " + path);
        }
    }
}
