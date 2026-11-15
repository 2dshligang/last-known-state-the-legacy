using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.PackageManager;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace LKS
{
    public static class StationMediaAudit
    {
        private const string Station = "Assets/_Game/Scenes/Station.unity";
        private const string Inventory = "Assets/_Game/Data/Editor/StationMediaInventory.json";
        private const string WorldSources = "ArtSource/Developer1/W15A/sources.json";
        private static readonly HashSet<string> MediaExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".tif", ".tiff", ".tga", ".psd", ".dds", ".exr", ".hdr",
            ".bmp", ".gif", ".webp", ".svg", ".fbx", ".obj", ".blend", ".dae", ".gltf", ".glb",
            ".ttf", ".otf", ".woff", ".woff2", ".wav", ".mp3", ".ogg", ".aif", ".aiff",
            ".flac", ".mod", ".xm", ".it", ".s3m", ".mp4", ".mov", ".webm", ".avi"
        };

        public static void BuildAndVerify()
        {
            string output = Environment.GetEnvironmentVariable("LKS_S19_OUTPUT");
            if (string.IsNullOrWhiteSpace(output))
                throw new InvalidDataException("Set LKS_S19_OUTPUT to a directory outside Assets.");
            output = Path.GetFullPath(output);
            string assetsPath = Path.GetFullPath(Application.dataPath);
            if (output.StartsWith(assetsPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(output, assetsPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Build output must be outside Assets.");
            Directory.CreateDirectory(output);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { Station },
                target = BuildTarget.StandaloneWindows64,
                // Incremental builds can return a successful report with no packed entries.
                options = BuildOptions.DetailedBuildReport | BuildOptions.CleanBuildCache,
                locationPathName = Path.Combine(output, "Station.exe")
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Station Windows build failed: " + report.summary.result);

            var packed = report.packedAssets.SelectMany(group => group.contents).ToArray();
            if (packed.Length == 0)
                throw new InvalidDataException("The Station build produced no detailed packed asset entries.");
            File.WriteAllLines(Path.Combine(output, "packed-assets.tsv"), packed
                .Select(asset => asset.type + "\t" + asset.sourceAssetPath)
                .Distinct().OrderBy(line => line, StringComparer.Ordinal));
            File.WriteAllLines(Path.Combine(output, "build-files.tsv"), report.GetFiles()
                .Select(file => file.role + "\t" + Path.GetFileName(file.path))
                .Distinct().OrderBy(line => line, StringComparer.Ordinal));
            var imported = AssetDatabase.GetAllAssetPaths().Where(IsImportedMedia);
            var packedMedia = packed.Where(asset => IsMedia(asset.type) &&
                asset.sourceAssetPath?.StartsWith("Assets/", StringComparison.Ordinal) == true)
                .Select(asset => asset.sourceAssetPath).ToArray();

            var inventory = JsonUtility.FromJson<MediaInventory>(File.ReadAllText(Inventory));
            var world = JsonUtility.FromJson<WorldInventory>(File.ReadAllText(WorldSources));
            if (inventory?.version != 1 || inventory.original == null || inventory.packages == null ||
                world?.external == null)
                throw new InvalidDataException("Media inventory or W15A source manifest is missing or invalid.");
            var known = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var group in inventory.original)
            {
                if (string.IsNullOrWhiteSpace(group.evidence) || !File.Exists(group.evidence) || group.paths == null)
                    throw new InvalidDataException("Original media requires checked-in authorship evidence.");
                foreach (string path in group.paths)
                    known.Add(path, group.evidence);
            }
            foreach (var row in world.external.Where(row => row.status == "approved-imported"))
            {
                VerifyExternalMedia(row.unityPath, row.author, row.sourceUrl, row.license, row.licenseUrl,
                    row.redistribution, row.importedSourceSha256);
                known.Add(row.unityPath, row.licenseUrl);
            }
            VerifyMediaCoverage(imported.Concat(packedMedia), known.Keys);
            File.WriteAllLines(Path.Combine(output, "media-use.tsv"), known.Keys
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => (packedMedia.Contains(path) ? "packed" : "unpacked") + "\t" + path));
            VerifyPackageEvidence(packed.Select(asset => asset.sourceAssetPath), inventory.packages);
            DonkiSnapshotImporter.VerifyRuntimeCatalog("Automation",
                AssetDatabase.LoadAssetAtPath<StationScienceCatalog>(DonkiSnapshotImporter.AssetPath));
            if (!packed.Any(asset => asset.sourceAssetPath == DonkiSnapshotImporter.AssetPath))
                throw new InvalidDataException("Station's approved DONKI catalog is absent from the Windows player.");
            Debug.Log("S19_MEDIA_AUDIT_PASS");
        }

        public static void VerifyMediaCoverage(IEnumerable<string> assets, IEnumerable<string> approvedPaths)
        {
            var approved = new HashSet<string>(approvedPaths, StringComparer.Ordinal);
            var unknown = assets.Distinct(StringComparer.Ordinal).Where(path => !approved.Contains(path))
                .OrderBy(path => path, StringComparer.Ordinal).ToArray();
            if (unknown.Length != 0)
                throw new InvalidDataException("Unmanifested imported or packed media: " + string.Join(", ", unknown));
        }

        public static bool IsImportedMedia(string path) =>
            path.StartsWith("Assets/", StringComparison.Ordinal) && !AssetDatabase.IsValidFolder(path) &&
            (MediaExtensions.Contains(Path.GetExtension(path)) || IsMedia(AssetDatabase.GetMainAssetTypeAtPath(path)));

        public static void VerifyExternalMedia(string path, string author, string sourceUrl, string license,
            string licenseUrl, string redistribution, string expectedHash)
        {
            if (string.IsNullOrWhiteSpace(author) || !IsHttps(sourceUrl) || string.IsNullOrWhiteSpace(license) ||
                !IsHttps(licenseUrl) || string.IsNullOrWhiteSpace(redistribution))
                throw new InvalidDataException("External media lacks attribution or redistribution evidence: " + path);
            if (FileHash(path) != expectedHash)
                throw new InvalidDataException("External media SHA-256 mismatch: " + path);
        }

        private static void VerifyPackageEvidence(IEnumerable<string> packed, PackageRow[] manifest)
        {
            var byId = manifest.ToDictionary(row => row.id, StringComparer.Ordinal);
            var packedIds = packed.Where(path => path?.StartsWith("Packages/", StringComparison.Ordinal) == true)
                .Select(path => path.Split('/')[1]).Distinct(StringComparer.Ordinal);
            foreach (string id in packedIds)
            {
                if (!byId.ContainsKey(id))
                    throw new InvalidDataException("Unmanifested package assets in the Windows player: " + id);
            }
            foreach (var row in manifest)
            {
                var package = PackageInfo.FindForAssetPath("Packages/" + row.id + "/package.json");
                if (package == null || package.version != row.version || !IsHttps(row.evidence))
                    throw new InvalidDataException("Unreviewed package version or rights: " + row.id);
                if (row.id == "com.unity.burst" && package.source == PackageSource.BuiltIn)
                    continue; // Unity 6.6 ships Burst as an engine module.
                if (FileHash(Path.Combine(package.resolvedPath, "LICENSE.md")) != row.licenseSha256)
                    throw new InvalidDataException("Package license SHA-256 mismatch: " + row.id);
            }
        }

        private static string FileHash(string path)
        {
            using var sha = SHA256.Create();
            using var bytes = File.OpenRead(path);
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        private static bool IsHttps(string value) =>
            Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

        private static bool IsMedia(Type type) => type != null &&
            (typeof(Texture).IsAssignableFrom(type) || type == typeof(Mesh) || type == typeof(Font) ||
             type == typeof(Sprite) || type == typeof(AudioClip) || type == typeof(UnityEngine.Video.VideoClip));

        [Serializable] private sealed class MediaInventory
        {
            public int version;
            public OriginalGroup[] original;
            public PackageRow[] packages;
        }
        [Serializable] private sealed class OriginalGroup { public string evidence; public string[] paths; }
        [Serializable] private sealed class PackageRow { public string id, version, evidence, licenseSha256; }
        [Serializable] private sealed class WorldInventory { public ExternalRow[] external; }
        [Serializable] private sealed class ExternalRow
        {
            public string status, unityPath, author, sourceUrl, license, licenseUrl, redistribution, importedSourceSha256;
        }
    }
}
