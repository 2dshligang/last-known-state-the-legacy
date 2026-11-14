using UnityEditor;

namespace LKS.Editor.World
{
    internal sealed class W03ModelImport : AssetPostprocessor
    {
        public override uint GetVersion() => 1;

        private void OnPreprocessModel()
        {
            if (assetPath != W03StationKit.ModelPath("AirlockFrame") &&
                assetPath != W03StationKit.ModelPath("AirlockLeaf") &&
                assetPath != W03StationKit.ModelPath("EchoHousing"))
                return;

            var model = (ModelImporter)assetImporter;
            model.globalScale = 1;
            model.useFileScale = false;
            model.bakeAxisConversion = false;
            model.importNormals = ModelImporterNormals.Import;
            model.importTangents = ModelImporterTangents.CalculateMikk;
            model.generateSecondaryUV = true;
            model.addCollider = false;
            model.importAnimation = false;
            model.importCameras = false;
            model.importLights = false;
            model.materialImportMode = ModelImporterMaterialImportMode.None;
        }
    }
}
