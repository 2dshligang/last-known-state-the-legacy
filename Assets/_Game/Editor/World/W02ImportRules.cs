using System;
using System.IO;
using UnityEditor;

namespace LKS.Editor.World
{
    internal sealed class W02ImportRules : AssetPostprocessor
    {
        internal const string ModelFolder = "Assets/_Game/Art/W01/";
        internal const string TextureFolder = "Assets/_Game/Art/W02/Textures";
        internal const string PanelColor = TextureFolder + "/PanelBaseColor.png";
        internal const string PanelNormal = TextureFolder + "/PanelNormal.png";
        internal const string PanelMask = TextureFolder + "/PanelMetallicSmoothness.png";

        public override uint GetVersion() => 1;

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ModelFolder, StringComparison.Ordinal) ||
                Path.GetDirectoryName(assetPath)?.Replace('\\', '/') != ModelFolder.TrimEnd('/') ||
                assetImporter is not ModelImporter importer ||
                importer.userData != "LKS.W01.OriginalPlaceholder")
                return;

            string format = Path.GetExtension(assetPath);
            if (format != ".fbx" && format != ".obj")
                return;

            importer.globalScale = 1;
            importer.useFileScale = false;
            importer.bakeAxisConversion = false;
            importer.addCollider = false;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importNormals = format == ".fbx"
                ? ModelImporterNormals.Import : ModelImporterNormals.Calculate;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.generateSecondaryUV = true;
        }

        private void OnPreprocessTexture()
        {
            if (assetPath != PanelColor && assetPath != PanelNormal && assetPath != PanelMask)
                return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = assetPath == PanelNormal
                ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = assetPath == PanelColor;
            importer.alphaSource = assetPath == PanelMask
                ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 256;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = UnityEngine.TextureWrapMode.Repeat;
        }
    }
}
