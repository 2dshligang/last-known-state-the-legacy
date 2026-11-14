using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace LKS
{
    public static class DonkiSnapshotImporter
    {
        public const string AssetPath = "Assets/_Game/Data/StationScienceCatalog.asset";
        public const string NormalizedPath = "samples/science/donki-events.json";
        private const string TransformPath = "tools/normalize-donki.mjs";
        private const string RawPath = "sources/raw/donki-sep-2017.json";
        private const string RawHash = "f36c8854e40b0429299450815fb74a7eaae1df3573c58c0d7efc269f6c92cb24";
        private const string NormalizedHash = "64e6ebda0d1d0f1a30e45d8ef35ca7ead69de5891cad82954ce1d121d848fd36";
        private const string TransformHash = "9eb949570da3cc664f86746fead9bfd179a23dfdaac7b7617e0a154d263204c1";
        private const string License = "LicenseRef-NASA-CCMC-public-data-usage";

        [MenuItem("LKS/Science/Import approved DONKI snapshot")]
        public static void Import() => Import("Automation", AssetPath);

        public static void Import(string automationRoot, string assetPath)
        {
            var source = ReadApprovedSource(automationRoot);
            VerifyChecksum(automationRoot, RawPath, RawHash);
            VerifyChecksum(automationRoot, TransformPath, TransformHash);
            VerifyChecksum(automationRoot, NormalizedPath, NormalizedHash);

            string normalized = File.ReadAllText(Path.Combine(automationRoot, NormalizedPath));
            var records = ParseNormalized(normalized, source.units);
            var catalog = AssetDatabase.LoadAssetAtPath<StationScienceCatalog>(assetPath);
            if (catalog == null && (File.Exists(assetPath) || File.Exists(assetPath + ".meta")))
                throw new InvalidDataException("The catalog path is occupied.");

            var candidate = ScriptableObject.CreateInstance<StationScienceCatalog>();
            candidate.name = catalog == null ? Path.GetFileNameWithoutExtension(assetPath) : catalog.name;
            candidate.ReplaceSnapshot(source.attribution, source.license, "donki-sep-v1",
                RawHash, NormalizedHash, TransformHash, records);
            if (catalog == null)
            {
                AssetDatabase.CreateAsset(candidate, assetPath);
                AssetDatabase.SaveAssetIfDirty(candidate);
                return;
            }

            try
            {
                if (EditorJsonUtility.ToJson(catalog) == EditorJsonUtility.ToJson(candidate))
                    return;
                EditorUtility.CopySerialized(candidate, catalog);
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssetIfDirty(catalog);
            }
            finally { UnityEngine.Object.DestroyImmediate(candidate); }
        }

        private static Source ReadApprovedSource(string root)
        {
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(
                Path.Combine(root, "sources/downloads.lock.json")));
            if (manifest?.schemaVersion != 1 || manifest.entries == null)
                throw new InvalidDataException("Unsupported source manifest.");
            var sources = manifest.entries.Where(source => source.id == StationScienceCatalog.DonkiSource.value).ToArray();
            if (sources.Length != 1)
                throw new InvalidDataException("Expected exactly one approved DONKI source.");
            var source = sources[0];
            if (source.approval != "approved" || source.license != License ||
                source.licenseUrl != "https://ccmc.gsfc.nasa.gov/tools/DONKI/#donki-rules-of-the-road" ||
                string.IsNullOrWhiteSpace(source.attribution) || string.IsNullOrWhiteSpace(source.units) ||
                source.sha256 != RawHash || source.path != RawPath || source.transform != TransformPath ||
                source.runtimeOutput != NormalizedPath)
                throw new InvalidDataException("DONKI source approval, attribution, units or provenance is missing or invalid.");
            return source;
        }

        public static ArchivedSepEvent[] ParseNormalized(string json, string units)
        {
            // The accepted format explicitly records missing dose and units. JsonUtility alone
            // cannot distinguish a JSON null from an absent or numeric field.
            var doseFields = System.Text.RegularExpressions.Regex.Matches(json, @"""dose""\s*:\s*([^,}\s]+)");
            var unitFields = System.Text.RegularExpressions.Regex.Matches(json, @"""doseUnit""\s*:\s*([^,}\s]+)");
            var snapshot = JsonUtility.FromJson<Snapshot>(json);
            if (snapshot?.schemaVersion != StationScienceCatalog.SchemaVersion ||
                snapshot.transformVersion != "donki-sep-v1" || snapshot.sourceSha256 != RawHash ||
                snapshot.events == null || snapshot.events.Length == 0 || string.IsNullOrWhiteSpace(units) ||
                doseFields.Count != snapshot.events.Length || unitFields.Count != snapshot.events.Length ||
                doseFields.Cast<System.Text.RegularExpressions.Match>().Any(match => match.Groups[1].Value != "null") ||
                unitFields.Cast<System.Text.RegularExpressions.Match>().Any(match => match.Groups[1].Value != "null"))
                throw new InvalidDataException("Unsupported snapshot schema, provenance or dose fields.");

            var result = snapshot.events.Select(row =>
            {
                if (row == null || !System.Text.RegularExpressions.Regex.IsMatch(row.id ?? "",
                        @"\A[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}-SEP-[0-9]+\z") ||
                    row.sourceId != StationScienceCatalog.DonkiSource.value ||
                    !DateTimeOffset.TryParseExact(row.observedAtUtc, "yyyy-MM-dd'T'HH:mm'Z'",
                        CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _) ||
                    !Uri.TryCreate(row.sourceRecordUrl, UriKind.Absolute, out var url) ||
                    url.Scheme != "https" || url.Host != "ccmc.gsfc.nasa.gov" ||
                    row.instruments == null || row.instruments.Length == 0 || row.instruments.Any(string.IsNullOrWhiteSpace))
                    throw new InvalidDataException("Malformed DONKI event.");
                bool isModel = row.instruments.Any(value => value.StartsWith("MODEL:", StringComparison.Ordinal));
                if (row.evidenceKind != (isModel ? "ModelOutput" : "ObservedEvent"))
                    throw new InvalidDataException("DONKI evidence category does not match instrument provenance.");
                return new ArchivedSepEvent(row.id, row.observedAtUtc, row.sourceRecordUrl, units,
                    row.instruments, isModel ? EvidenceKind.EngineeringModel : EvidenceKind.Observation);
            }).ToArray();
            StableId.ValidateUnique(result.Select(row => row.Record.Id));
            return result;
        }

        private static void VerifyChecksum(string root, string relativePath, string expected)
        {
            using var sha = SHA256.Create();
            using var input = File.OpenRead(Path.Combine(root, relativePath));
            string actual = BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
            if (actual != expected)
                throw new InvalidDataException($"Checksum mismatch for '{relativePath}'. Review the provenance before importing.");
        }

        [Serializable] private sealed class Manifest { public int schemaVersion; public Source[] entries; }
        [Serializable] private sealed class Source
        {
            public string id, approval, license, licenseUrl, attribution, units, sha256, path, transform, runtimeOutput;
        }
        [Serializable] private sealed class Snapshot
        {
            public int schemaVersion;
            public string transformVersion, sourceSha256;
            public Row[] events;
        }
        [Serializable] private sealed class Row
        {
            public string id, observedAtUtc, sourceId, sourceRecordUrl, evidenceKind;
            public string[] instruments;
        }
    }
}
