using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LKS.Tests
{
    public sealed class StationRoutePrerequisiteTests
    {
        [Test]
        public void AcceptedCatalogStartsAReachableRouteWithoutGivingAwayEvidenceOrEnding()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<StationScienceCatalog>("Assets/_Game/Data/StationScienceCatalog.asset");
            var source = new StableId("donki.2017-09-04t23-52-00-sep-001");
            Assert.That(catalog, Is.Not.Null, "The imported station catalog is required.");
            var host = new GameObject("Route prerequisite tests");
            try
            {
                var bootstrap = host.AddComponent<SessionBootstrap>();
                Assert.That(bootstrap.RestartWithPower(catalog, new[] { source },
                    () => Array.Empty<IInteractable>(), _ => ResultCode.EvidenceInsufficient), Is.Empty);
                var run = bootstrap.Current;
                Assert.That(catalog.TryGet(source, out var record), Is.True);
                Assert.That(record.Kind, Is.EqualTo(EvidenceKind.Observation));
                Assert.That(FictionalSample.All.All(item => item.Kind == EvidenceKind.Fiction), Is.True);
                Assert.That(EchoInvestigation.FreshnessRuleKind, Is.EqualTo(EvidenceKind.EngineeringModel));
                foreach (var id in StationQuests.RequiredStages.Prepend(StationQuests.RecoveryTarget))
                    Assert.That(run.TryGetTarget(id, out _), Is.True, "Missing target " + id);
                Assert.That(run.TryGetTarget(StationEnding.Photograph, out var ending), Is.True);
                Assert.That(ending.Read(StationQuests.Player).Available, Is.False);
                Assert.That(ending.Request(StationQuests.Player, StationQuests.Confirm).Succeeded, Is.False);
                Assert.That(run.Ending.QueueAcceptedReport().MessageKey.value, Is.EqualTo("ending.result.accept-report"));
                Assert.That(run.CaptureQuestCheckpoint(), Is.Empty);
                Assert.That(run.Ending.TryGetFinalCue(out _), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }
    }
}
