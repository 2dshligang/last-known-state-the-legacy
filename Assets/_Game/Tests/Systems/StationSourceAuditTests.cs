using System;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LKS.Tests
{
    public sealed class StationSourceAuditTests
    {
        [Test]
        public void ImportedUnmanifestedImageBlocksTheAudit()
        {
            string path = "Assets/S19-test-" + Guid.NewGuid().ToString("N") + ".png";
            var texture = new Texture2D(1, 1);
            try
            {
                File.WriteAllBytes(path, texture.EncodeToPNG());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(path), Is.Not.Null);
                var error = Assert.Throws<InvalidDataException>(() =>
                    StationMediaAudit.VerifyMediaCoverage(
                        AssetDatabase.GetAllAssetPaths().Where(StationMediaAudit.IsImportedMedia),
                        AssetDatabase.GetAllAssetPaths().Where(approved => approved != path)));
                Assert.That(error.Message, Does.Contain(path));
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void MediaWithoutRedistributionRightsBlocksTheAudit()
        {
            const string path = "Assets/test.png";
            var error = Assert.Throws<InvalidDataException>(() => StationMediaAudit.VerifyExternalMedia(
                path, "Fixture artist", "https://example.org/file", "", "", "", ""));
            Assert.That(error.Message, Does.Contain("redistribution").And.Contain(path));
        }

        [Test]
        public void StaleTransformOrRelabeledRuntimeEventBlocksTheAudit()
        {
            var official = AssetDatabase.LoadAssetAtPath<StationScienceCatalog>(DonkiSnapshotImporter.AssetPath);
            Assert.That(official, Is.Not.Null);
            Assert.DoesNotThrow(() => DonkiSnapshotImporter.VerifyRuntimeCatalog("Automation", official));
            var fixture = UnityEngine.Object.Instantiate(official);
            try
            {
                ApplyFixture(fixture, official, "donki-sep-v0", official.Events.ToArray());
                Assert.Throws<InvalidDataException>(() =>
                    DonkiSnapshotImporter.VerifyRuntimeCatalog("Automation", fixture));

                var rewritten = official.Events.Select(entry => entry.Record.Kind == EvidenceKind.EngineeringModel
                    ? new ArchivedSepEvent(entry.OriginalId,
                        entry.Record.RecordedAt.Value.ToString("yyyy-MM-dd'T'HH:mm'Z'", CultureInfo.InvariantCulture),
                        entry.Record.Source.AbsoluteUri, entry.Units, entry.Instruments.ToArray(), EvidenceKind.Observation)
                    : entry).ToArray();
                ApplyFixture(fixture, official, official.TransformVersion, rewritten);
                Assert.Throws<InvalidDataException>(() =>
                    DonkiSnapshotImporter.VerifyRuntimeCatalog("Automation", fixture));
            }
            finally { UnityEngine.Object.DestroyImmediate(fixture); }
        }

        private static void ApplyFixture(StationScienceCatalog fixture, StationScienceCatalog official,
            string version, ArchivedSepEvent[] events)
        {
            fixture.ReplaceSnapshot(official.Attribution, official.License, version, official.RawSha256,
                official.NormalizedSha256, official.TransformSha256, events);
        }
    }
}
