using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LKS.Editor.World
{
    public static class W17FontInventory
    {
        public static void Verify()
        {
            var assets = AssetDatabase.GetAllAssetPaths().Where(path =>
                path.StartsWith("Assets/", StringComparison.Ordinal)).ToArray();
            int tmp = 0, worldLabels = 0;
            foreach (string path in assets)
            {
                if (path.EndsWith(".prefab", StringComparison.Ordinal))
                    Count(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                else if (path.EndsWith(".unity", StringComparison.Ordinal))
                {
                    var scene = EditorSceneManager.OpenPreviewScene(path);
                    try { foreach (var root in scene.GetRootGameObjects()) Count(root); }
                    finally { EditorSceneManager.ClosePreviewScene(scene); }
                }
            }
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var catalog = EnglishContentCatalog.CreateDefault();
            var glyphs = string.Concat(catalog.Keys.Select(key => catalog.Resolve(key, false))).
                Where(character => !char.IsWhiteSpace(character)).Distinct().ToArray();
            if (font == null || glyphs.Any(character => !font.HasCharacter(character)))
                throw new InvalidOperationException("A required English glyph is missing from the station font.");
            Debug.Log($"W17_FONT_PASS {glyphs.Length} catalog glyphs; {worldLabels} serialized TextMesh components; {tmp} serialized TMP components.");

            void Count(GameObject root)
            {
                if (root == null) return;
                worldLabels += root.GetComponentsInChildren<TextMesh>(true).Length;
                tmp += root.GetComponentsInChildren<Component>(true).Count(component =>
                    component != null && IsTmp(component.GetType()));
            }
        }

        private static bool IsTmp(Type type)
        {
            for (var current = type; current != null; current = current.BaseType)
                if (current.FullName == "TMPro.TMP_Text") return true;
            return false;
        }
    }
}
