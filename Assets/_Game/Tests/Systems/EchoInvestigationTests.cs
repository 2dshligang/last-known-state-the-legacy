using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace LKS.Tests
{
    public sealed class EchoInvestigationTests
    {
        private static readonly StableId ArchiveId = new StableId("archive.donki-sep");
        private GameObject host;
        private SessionBootstrap bootstrap;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Investigation test");
            bootstrap = host.AddComponent<SessionBootstrap>();
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(host);

        [TestCase(0, 1, 2, true)]
        [TestCase(0, 2, 1, false)]
        [TestCase(1, 0, 2, false)]
        [TestCase(1, 2, 0, false)]
        [TestCase(2, 0, 1, false)]
        [TestCase(2, 1, 0, false)]
        public void OnlyTheCausalOrderConfirms(int first, int second, int third, bool accepted)
        {
            Start();
            var investigation = bootstrap.Current.Investigation;
            var clues = EchoInvestigation.RequiredClues;
            foreach (var clue in clues)
                Assert.That(investigation.Collect(clue).Code, Is.EqualTo(ResultCode.Applied));

            var result = investigation.SubmitSequence(new[] { clues[first], clues[second], clues[third] });
            Assert.That(result.Succeeded, Is.EqualTo(accepted));
            Assert.That(investigation.Conclusion, Is.EqualTo(accepted ? EchoConclusion.Confirmed : EchoConclusion.Tentative));
            Assert.That(investigation.Capture().Collected, Is.EqualTo(clues));
            if (accepted)
                Assert.That(investigation.SubmitSequence(clues).Code, Is.EqualTo(ResultCode.AlreadyApplied));
        }

        [Test]
        public void TentativeReloadStaysBlockedAndConfirmedReloadEnablesSampling()
        {
            var progress = StationQuests.RequiredStages.Take(2).ToArray();
            Start(quest: progress);
            var run = bootstrap.Current;
            var clues = EchoInvestigation.RequiredClues;
            Assert.That(run.Investigation.Collect(clues[0]).Succeeded, Is.True);
            Assert.That(run.Investigation.SubmitSequence(clues).Code, Is.EqualTo(ResultCode.EvidenceInsufficient));
            Start(run.Investigation.Capture(), progress);

            run = bootstrap.Current;
            Assert.That(run.Investigation.Conclusion, Is.EqualTo(EchoConclusion.Tentative));
            Assert.That(run.TryGetTarget(StationQuests.RequiredStages[2], out var target), Is.True);
            Assert.That(target.Request(StationQuests.Player, StationQuests.Confirm).Code,
                Is.EqualTo(ResultCode.EvidenceInsufficient));
            foreach (var clue in clues.Skip(1))
                run.Investigation.Collect(clue);
            Assert.That(run.Investigation.SubmitSequence(clues).Succeeded, Is.True);
            Assert.That(target.Request(StationQuests.Player, StationQuests.Confirm).Code, Is.EqualTo(ResultCode.Applied));
            Start(run.Investigation.Capture(), run.CaptureQuestCheckpoint());
            Assert.That(bootstrap.Current.Investigation.Conclusion, Is.EqualTo(EchoConclusion.Confirmed));
            Assert.That(bootstrap.Current.Quests.Snapshot[StationQuests.RequiredStages[3]], Is.EqualTo(QuestStatus.Available));
        }

        [TestCase(45, ReadingFreshness.Fresh)]
        [TestCase(60, ReadingFreshness.Uncertain)]
        [TestCase(75, ReadingFreshness.Uncertain)]
        [TestCase(76, ReadingFreshness.Stale)]
        [TestCase(-1, ReadingFreshness.Unknown)]
        public void FreshnessIncludesTimestampUncertainty(int age, ReadingFreshness expected)
        {
            var reading = DateTimeOffset.UnixEpoch;
            Assert.That(EchoInvestigation.EvaluateFreshness(reading, reading.AddSeconds(age)), Is.EqualTo(expected));
            Assert.That(EchoInvestigation.EvaluateFreshness(null, reading), Is.EqualTo(ReadingFreshness.Unknown));
        }

        private void Start(EchoInvestigationCheckpoint investigation = null,
            System.Collections.Generic.IEnumerable<StableId> quest = null)
        {
            var errors = bootstrap.RestartWithPower(new Catalog(), Array.Empty<StableId>(),
                () => Array.Empty<IInteractable>(), _ => ResultCode.Unavailable,
                questCheckpoint: quest, clock: new Clock(), archiveCheckpoint: new[] { ArchiveId },
                investigationCheckpoint: investigation);
            Assert.That(errors, Is.Empty);
        }

        private sealed class Clock : ISessionClock { public double Seconds => 0; }
        private sealed class Catalog : IScienceCatalog
        {
            public bool TryGet(StableId id, out ScienceRecord record)
            {
                record = default;
                return false;
            }
        }
    }
}
