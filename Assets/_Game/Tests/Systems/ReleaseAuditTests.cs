using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LKS.Tests
{
    public sealed class ReleaseAuditTests
    {
        [Test]
        public void MissingKeyFailsReleaseLookup()
        {
            var catalog = EnglishContentCatalog.CreateDefault();
            Assert.That(StationReleaseAudit.KeyProblem(catalog, "fixture.missing"), Does.Contain("Missing"));
            Assert.That(StationReleaseAudit.KeyProblem(catalog, "ending.final"), Is.Null);
        }

        [Test]
        public void InactivePrefabTextFailsAudit()
        {
            string path = "Assets/_Game/Tests/Systems/AuditFixture-" + Guid.NewGuid().ToString("N") + ".prefab";
            var root = new GameObject("Audit fixture");
            try
            {
                var child = new GameObject("Hidden label", typeof(TextMesh));
                child.transform.SetParent(root.transform);
                child.GetComponent<TextMesh>().text = "New Text";
                child.SetActive(false);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                Assert.That(prefab, Is.Not.Null);

                var issues = StationReleaseAudit.InspectSerializedText(prefab, path);
                Assert.That(issues, Has.Count.EqualTo(1));
                Assert.That(issues[0], Does.Contain("Hidden label").And.Contain("Untranslated default"));
                Assert.That(issues[0], Does.Contain(AssetDatabase.AssetPathToGUID(path)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                AssetDatabase.DeleteAsset(path);
            }
        }

        [Test]
        public void RuntimeCaptureRejectsCyrillicAndRequiresTheExactEnding()
        {
            var issues = StationReleaseAudit.RuntimeProblems(new[]
            {
                Capture("menu.main", "\u0410"),
                Capture("ending.final", "Dad… I did it.")
            }, new[] { "menu.main", "ending.final", "credits.team" });
            Assert.That(issues, Has.Count.EqualTo(3));
            Assert.That(issues[0], Does.Contain("Cyrillic"));
            Assert.That(issues[1], Does.Contain("punctuation"));
            Assert.That(issues[2], Does.Contain("credits.team"));
            Assert.That(StationReleaseAudit.RuntimeProblems(
                new[] { Capture("ending.final", "Dad... I did it.") }, new[] { "ending.final" }), Is.Empty);
        }

        [Test]
        public void AudioRequiresProvenancePurposeAndListeningForTheExactBytes()
        {
            const string guid = "fixture-guid";
            Assert.That(StationReleaseAudit.MediaProblem(guid, "bytes", null), Does.Contain("one review"));
            var review = new StationReleaseAudit.MediaReview
            {
                guid = guid, sha256 = "bytes", source = "Original recording",
                license = "Fixture license", reviewer = "Fixture reviewer", listened = true, speechFree = true
            };
            Assert.That(StationReleaseAudit.MediaProblem(guid, "bytes", new[] { review }), Does.Contain("purpose"));
            review.purpose = "ambience";
            Assert.That(StationReleaseAudit.MediaProblem(guid, "bytes", new[] { review }), Is.Null);
            Assert.That(StationReleaseAudit.MediaProblem(guid, "changed", new[] { review }), Does.Contain("different bytes"));
            review.listened = false;
            Assert.That(StationReleaseAudit.MediaProblem(guid, "bytes", new[] { review }), Does.Contain("Human"));
        }

        private static string Capture(string surface, string text) => PlayerTextAudit.LogPrefix +
            JsonUtility.ToJson(new PlayerTextAudit.Entry { surface = surface, text = text });
    }
}
