using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace LKS.Tests
{
    public sealed class DonkiImporterTests
    {
        [Test]
        public void ReimportPreservesGuidAndBytes()
        {
            string path = "Assets/_Game/Data/DonkiTest-" + Guid.NewGuid().ToString("N") + ".asset";
            try
            {
                DonkiSnapshotImporter.Import("Automation", path);
                string guid = AssetDatabase.AssetPathToGUID(path);
                byte[] bytes = File.ReadAllBytes(path);
                DonkiSnapshotImporter.Import("Automation", path);
                DonkiSnapshotImporter.Import("Automation", path);
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes));
                var catalog = AssetDatabase.LoadAssetAtPath<StationScienceCatalog>(path);
                Assert.That(catalog.Events.Count, Is.EqualTo(9));
                Assert.That(catalog.Events.Count(entry => entry.Record.Kind == EvidenceKind.EngineeringModel), Is.EqualTo(1));
                Assert.That(catalog.Events.All(entry => entry.Dose == null), Is.True);
            }
            finally { AssetDatabase.DeleteAsset(path); }
        }

        [TestCase("\"dose\": null", "\"dose\": 0")]
        [TestCase("ModelOutput", "ObservedEvent")]
        [TestCase("2017-09-04T22:56Z", "2017-13-04T22:56Z")]
        [TestCase("donki-sep-v1", "donki-sep-v0")]
        public void RejectsMalformedAndReclassifiedRecords(string original, string replacement)
        {
            string json = File.ReadAllText("Automation/" + DonkiSnapshotImporter.NormalizedPath);
            Assert.Throws<InvalidDataException>(() =>
                DonkiSnapshotImporter.ParseNormalized(json.Replace(original, replacement), "No dose supplied"));
        }
    }
}
