using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LKS.Editor.World
{
    public static class W02Verification
    {
        private const string InspectionRoot = "W02 Asset Samples";
        private const string WallStem = "wall-straight-4m";
        private const ImportAssetOptions ForceImport =
            ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport;

        public static void Verify()
        {
            RequireBatch();
            var entries = W02Inspection.ReadEntries();
            var original = Snapshot();
            string references = ValidateScene(entries);
            for (int pass = 1; pass <= 2; pass++)
            {
                foreach (var entry in entries) AssetDatabase.ImportAsset(entry.path, ForceImport);
                AssetDatabase.ImportAsset(W02ImportRules.PanelColor, ForceImport);
                AssetDatabase.ImportAsset(W02ImportRules.PanelNormal, ForceImport);
                AssetDatabase.ImportAsset(W02ImportRules.PanelMask, ForceImport);
                W02Inspection.Generate();
                RequireUnchanged(original, "Reimport " + pass);
                Require(ValidateScene(entries) == references, "Asset references changed on reimport.");
                Debug.Log($"W02_REIMPORT_PASS {pass}: serialized assets and references are identical.");
            }

            string revision = Environment.GetEnvironmentVariable("LKS_W02_WALL_REVISION");
            if (!string.IsNullOrEmpty(revision))
            {
                var wall = entries.Single(entry => entry.Stem == WallStem);
                byte[] originalFbx = File.ReadAllBytes(wall.path);
                Vector2[] originalUV = GetWallUV(wall);
                byte[] revisedFbx = File.ReadAllBytes(revision);
                Require(!originalFbx.SequenceEqual(revisedFbx), "Expected distinct FBX revision bytes.");
                try
                {
                    File.WriteAllBytes(wall.path, revisedFbx);
                    AssetDatabase.ImportAsset(wall.path, ForceImport);
                    Require(!originalUV.SequenceEqual(GetWallUV(wall)), "Revised UVs did not reach Unity.");
                    Require(ValidateScene(entries) == references,
                        "FBX revision broke model, material, anchor or collider references.");
                    RequireUnchanged(original, "Temporary FBX revision");
                    Debug.Log("W02_FBX_REVISION_PASS: same-path Blender revision preserved all bindings.");
                }
                finally
                {
                    File.WriteAllBytes(wall.path, originalFbx);
                    AssetDatabase.ImportAsset(wall.path, ForceImport);
                }
                Require(originalUV.SequenceEqual(GetWallUV(wall)), "Original FBX UVs were not restored.");
                Require(ValidateScene(entries) == references, "Original FBX bindings did not recover.");
                RequireUnchanged(original, "Original FBX restore");
            }
            Debug.Log("W02_VERIFY_PASS");
        }

        private static Vector2[] GetWallUV(W02Inspection.Entry wall)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(wall.PrefabPath);
            return prefab.GetComponentInChildren<MeshFilter>().sharedMesh.uv;
        }

        private static string ValidateScene(W02Inspection.Entry[] entries)
        {
            var scene = EditorSceneManager.OpenScene(W02Inspection.ScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var samples = roots.Single(root => root.name == InspectionRoot).transform;
            var reference = roots.Single(root => root.name == "One meter reference").GetComponent<BoxCollider>();
            Require(samples.childCount == 8, "Expected eight inspection samples.");
            Require(reference.bounds.size == Vector3.one, "Reference cube is not one meter.");
            var identifiers = new List<string>();
            Physics.SyncTransforms();
            foreach (var entry in entries)
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(entry.path);
                Require(importer.globalScale == 1 && !importer.useFileScale &&
                    !importer.bakeAxisConversion && !importer.addCollider && importer.generateSecondaryUV,
                    "Unexpected model settings: " + entry.path);

                var placed = samples.Find(entry.id);
                Require(placed != null && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(placed) == entry.PrefabPath,
                    "Scene wrapper reference missing: " + entry.id);
                var visual = placed.Find("Visual");
                Require(visual != null && visual.childCount == 1 &&
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(visual.GetChild(0)) == entry.path,
                    "Nested model reference missing: " + entry.id);
                var bounds = W02Inspection.ModelBounds(visual.gameObject, placed, entry.Size);
                Require(visual.GetComponentsInChildren<Collider>().Length == 0, "Unexpected model collider.");
                var anchors = placed.Find("Anchors");
                var inspect = anchors?.Find("InspectPoint");
                Require(anchors?.Find("SnapStart")?.localPosition == Vector3.zero && inspect != null,
                    "Wrapper anchors missing: " + entry.id);
                var hits = placed.Find("Collision").GetComponentsInChildren<BoxCollider>();
                int expectedCount = entry.Stem switch { "equipment-rack" => 6, "lab-bench" => 3, _ => 1 };
                Require(hits.Length == expectedCount && hits.All(box => box.enabled && !box.isTrigger) &&
                    placed.GetComponentsInChildren<Collider>().Length == expectedCount,
                    "Incorrect collider layout: " + entry.id);
                var material = W02Inspection.LoadMaterial(entry.MaterialName);
                Require(material.shader.name == "Universal Render Pipeline/Lit" &&
                    visual.GetComponentsInChildren<MeshRenderer>().All(renderer =>
                        renderer.sharedMaterials.Length > 0 && renderer.sharedMaterials.All(slot => slot == material)),
                    "Broken material slot: " + entry.id);

                identifiers.Add(AssetDatabase.AssetPathToGUID(entry.PrefabPath));
                var filter = visual.GetComponentInChildren<MeshFilter>();
                Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(filter.sharedMesh, out string guid, out long fileId),
                    "Missing mesh asset reference.");
                identifiers.Add($"{guid}/{fileId}/{inspect.localPosition:F4}");
                identifiers.AddRange(hits.Select(box => $"{box.transform.localPosition:F4}/{box.center:F4}/{box.size:F4}"));
                if (entry.Stem == WallStem)
                {
                    Require(anchors.Find("SnapEnd").localPosition == new Vector3(-4, 0, 0),
                        "Wall snap point changed.");
                    Require(Vector3.Distance(bounds.min, new Vector3(-4, 0, -.1f)) < W02Inspection.MeterTolerance &&
                        Vector3.Distance(bounds.max, new Vector3(0, 3, .1f)) < W02Inspection.MeterTolerance,
                        "Wall orientation changed.");
                    Require(Vector3.Distance(hits[0].size, bounds.size) < W02Inspection.MeterTolerance &&
                        Vector3.Distance(hits[0].transform.localPosition, bounds.center) < W02Inspection.MeterTolerance,
                        "Wall box does not match its visual bounds.");
                    Vector3 rayStart = placed.TransformPoint(new Vector3(-2, 1.5f, -2));
                    Require(Physics.Raycast(rayStart, placed.forward, out var hit, 3) &&
                        hit.collider == hits[0] && Mathf.Abs(hit.distance - 1.9f) < W02Inspection.MeterTolerance,
                        "Wall collider did not block its expected ray.");
                }
                else if (entry.Stem == "lab-bench")
                {
                    Vector3 rayStart = placed.TransformPoint(new Vector3(0, .55f, -.7f));
                    Require(!Physics.Raycast(rayStart, placed.forward, .95f), "Bench opening is blocked.");
                }
            }
            ValidatePanel();
            return string.Join("\n", identifiers);
        }

        private static void ValidatePanel()
        {
            var color = (TextureImporter)AssetImporter.GetAtPath(W02ImportRules.PanelColor);
            var normal = (TextureImporter)AssetImporter.GetAtPath(W02ImportRules.PanelNormal);
            var mask = (TextureImporter)AssetImporter.GetAtPath(W02ImportRules.PanelMask);
            var panel = W02Inspection.LoadMaterial("SolarPanel");
            Require(color.sRGBTexture && !normal.sRGBTexture && !mask.sRGBTexture &&
                normal.textureType == TextureImporterType.NormalMap &&
                mask.alphaSource == TextureImporterAlphaSource.FromInput, "Unexpected panel color spaces or alpha source.");
            Require(AssetDatabase.GetAssetPath(panel.GetTexture("_BaseMap")) == W02ImportRules.PanelColor &&
                AssetDatabase.GetAssetPath(panel.GetTexture("_BumpMap")) == W02ImportRules.PanelNormal &&
                AssetDatabase.GetAssetPath(panel.GetTexture("_MetallicGlossMap")) == W02ImportRules.PanelMask &&
                panel.IsKeywordEnabled("_NORMALMAP") && panel.IsKeywordEnabled("_METALLICSPECGLOSSMAP") &&
                panel.GetFloat("_SmoothnessTextureChannel") == 0, "Panel material slots or keywords changed.");
            var raw = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                Require(raw.LoadImage(File.ReadAllBytes(W02ImportRules.PanelMask)), "Panel mask PNG cannot be read.");
                var pixels = raw.GetPixels32();
                Require(pixels[0].Equals(new Color32(205, 0, 0, 153)) &&
                    pixels[8 + 8 * raw.width].Equals(new Color32(12, 0, 0, 201)),
                    "Packed metallic or inverted roughness differs from the source values.");
            }
            finally { Object.DestroyImmediate(raw); }
        }

        private static Dictionary<string, string> Snapshot()
        {
            var paths = Directory.GetFiles(W02Inspection.PrefabsFolder, "*", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(W02Inspection.MaterialsFolder, "*", SearchOption.AllDirectories))
                .Concat(Directory.GetFiles(W02ImportRules.TextureFolder, "*", SearchOption.AllDirectories))
                .Concat(new[] { W02Inspection.ScenePath, W02Inspection.ScenePath + ".meta" })
                .Concat(W02Inspection.ReadEntries().Select(entry => entry.path + ".meta"));
            using var sha = SHA256.Create();
            return paths.ToDictionary(path => path, path => Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path))));
        }

        private static void RequireUnchanged(Dictionary<string, string> before, string step)
        {
            var after = Snapshot();
            Require(before.Count == after.Count && before.All(item =>
                after.TryGetValue(item.Key, out string hash) && hash == item.Value),
                step + " added or rewrote an inspection asset.");
        }

        public static void RenderPreview()
        {
            RequireBatch();
            string output = Environment.GetEnvironmentVariable("LKS_W02_PREVIEW");
            Require(!string.IsNullOrWhiteSpace(output) && !File.Exists(output), "Set LKS_W02_PREVIEW to a new PNG path.");
            ValidateScene(W02Inspection.ReadEntries());
            var camera = Camera.main;
            Require(camera != null, "Inspection camera is missing.");
            var target = new RenderTexture(1600, 1000, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                var request = new RenderPipeline.StandardRequest { destination = target };
                Require(RenderPipeline.SupportsRenderRequest(camera, request), "URP render request is unavailable.");
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                pixels.Apply();
                using var file = new FileStream(output, FileMode.CreateNew);
                byte[] png = pixels.EncodeToPNG();
                file.Write(png, 0, png.Length);
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(target);
            }
            Debug.Log("W02_URP_PREVIEW_PASS");
        }

        private static void RequireBatch() =>
            Require(Application.isBatchMode, "Run verification in batch mode on an isolated checkout.");

        private static void Require(bool condition, string error)
        {
            if (!condition) throw new InvalidDataException(error);
        }
    }
}
