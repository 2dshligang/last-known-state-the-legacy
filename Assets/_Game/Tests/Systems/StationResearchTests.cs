using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace LKS.Tests
{
    public sealed class StationResearchTests
    {
        private static readonly StableId ArchiveId = new StableId("archive.donki-sep");
        private static readonly ReportObservation[] Observations =
        {
            ReportObservation.LayersVisible, ReportObservation.IronMineralSigns, ReportObservation.BlankPassed
        };

        private GameObject host;
        private SessionBootstrap bootstrap;
        private Clock clock;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Research tests");
            bootstrap = host.AddComponent<SessionBootstrap>();
            clock = new Clock();
            Start();
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(host);

        [Test]
        public void ResearchCompletesOnceAndRestoresWithoutDuplicateEvent()
        {
            var run = bootstrap.Current;
            ReviewValidResults();
            int completions = 0;
            using var subscription = run.Research.Subscribe(change =>
            {
                Assert.That(change.Subject, Is.EqualTo(StationResearch.ReportId));
                Assert.That(change.Cause, Is.EqualTo(StationResearch.CompletedCause));
                Assert.That(change.Snapshot, Is.EqualTo(ReportStatus.Accepted));
                completions++;
            });

            Assert.That(run.Research.Snapshot, Is.EqualTo(ReportStatus.Incomplete));
            Assert.That(run.Narrative.TryGetResearchContext(out _), Is.False);
            Assert.That(run.Research.Evaluate(Draft()).Code, Is.EqualTo(ResultCode.Applied));
            Assert.That(run.Research.Snapshot, Is.EqualTo(ReportStatus.Incomplete));
            Assert.That(run.Research.Submit(Draft()).Code, Is.EqualTo(ResultCode.Applied));
            Assert.That(run.Narrative.TryGetResearchContext(out var context), Is.True);
            Assert.That(context.Portrait, Is.EqualTo(new StableId("portrait.dmitry-photograph")));
            Assert.That(run.Archive.Content.Resolve(context.TextKey, false), Is.Not.Empty);
            Assert.That(run.Research.Submit(Draft()).Code, Is.EqualTo(ResultCode.AlreadyApplied));
            run.Dispatch();
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(run.Research.ReadEvidence().Count, Is.EqualTo(3));
            Assert.That(run.Research.ReadEvidence().All(item =>
                item.StationKind == EvidenceKind.Fiction && item.ReferenceSource.IsAbsoluteUri), Is.True);

            Start(run.Samples.Capture(), run.Research.CaptureAccepted());
            run = bootstrap.Current;
            using var restored = run.Research.Subscribe(_ => completions++);
            Assert.That(run.Research.Snapshot, Is.EqualTo(ReportStatus.Accepted));
            Assert.That(run.Research.Submit(Draft()).Code, Is.EqualTo(ResultCode.AlreadyApplied));
            run.Dispatch();
            Assert.That(completions, Is.EqualTo(1));
        }

        [Test]
        public void ReportQuestRequiresAcceptanceAndKeepsItsNextStageAvailableAfterReload()
        {
            ReviewValidResults();
            var progress = StationQuests.RequiredStages.Take(5).ToArray();
            Start(bootstrap.Current.Samples.Capture(), progress: progress);
            var run = bootstrap.Current;
            var reportStage = StationQuests.RequiredStages[5];
            Assert.That(run.TryGetTarget(reportStage, out var target), Is.True);
            Assert.That(target.Request(StationQuests.Player, StationQuests.Confirm).Code,
                Is.EqualTo(ResultCode.EvidenceInsufficient));
            Assert.That(run.Research.Submit(Draft()).Code, Is.EqualTo(ResultCode.Applied));
            Assert.That(target.Request(StationQuests.Player, StationQuests.Confirm).Code,
                Is.EqualTo(ResultCode.Applied));

            Start(run.Samples.Capture(), run.Research.CaptureAccepted(), run.CaptureQuestCheckpoint().ToArray());
            Assert.That(bootstrap.Current.Quests.Snapshot[StationQuests.RequiredStages[6]],
                Is.EqualTo(QuestStatus.Available));
        }

        [Test]
        public void MissingEvidenceAndContaminationBlockAcceptance()
        {
            var run = bootstrap.Current;
            using var subscription = run.Research.Subscribe(_ => Assert.Fail("A rejected report published completion."));
            AssertKey(run.Research.Submit(Draft()), "valid-blank");
            CollectAll();
            Analyze(StationSamples.Blank);
            Assert.That(run.Samples.BlankControl, Is.EqualTo(ControlStatus.Invalid));
            AssertKey(run.Research.Submit(Draft()), "valid-blank");
            Assert.That(run.Samples.CleanForRetry().Succeeded, Is.True);
            Analyze(StationSamples.Blank);
            AssertKey(run.Research.Submit(Draft()), "cite-reviewed-samples");
            Assert.That(run.Research.CaptureAccepted(), Is.Null);
            run.Dispatch();
        }

        [Test]
        public void RejectedClaimsCanBeRevisedWithoutLosingEvidence()
        {
            var run = bootstrap.Current;
            ReviewValidResults();
            AssertKey(run.Research.Submit(Draft(interpretation: ReportInterpretation.LifeConfirmed)), "life-unsupported");
            AssertKey(run.Research.Submit(Draft(Observations.Take(2).ToArray())), "cite-blank");
            AssertKey(run.Research.Submit(Draft(Observations.Append(ReportObservation.BlankShowedBackground).ToArray())),
                "contradictory-blank");
            AssertKey(run.Research.Submit(Draft(followUp: ReportFollowUp.None)), "follow-up-needed");
            Assert.That(run.Samples.AllReviewed, Is.True);
            Assert.That(run.Research.CaptureAccepted(), Is.Null);
            Assert.That(run.Research.Submit(new ResearchDraft(Observations,
                ReportInterpretation.FormationRemainsUncertain, ReportAlternative.LaterMineralChange,
                ReportUncertainty.MoreEvidenceNeededForLife, ReportFollowUp.CheckMineralsWithFreshBlank)).Succeeded, Is.True);
        }

        private static void AssertKey(CommandResult result, string key)
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.MessageKey.ToString(), Is.EqualTo("report.result." + key));
        }

        private static ResearchDraft Draft(ReportObservation[] observations = null,
            ReportInterpretation interpretation = ReportInterpretation.SedimentMayHaveSettled,
            ReportFollowUp followUp = ReportFollowUp.CompareNearbyLayersWithBlank) =>
            new ResearchDraft(observations ?? Observations, interpretation, ReportAlternative.WindDeposition,
                ReportUncertainty.OriginUncertain, followUp);

        private void CollectAll()
        {
            foreach (var definition in FictionalSample.All)
            {
                Assert.That(bootstrap.Current.Samples.RecordContext(definition.Id).Succeeded, Is.True);
                Assert.That(bootstrap.Current.Samples.Collect(definition.Id).Succeeded, Is.True);
            }
        }

        private void Analyze(StableId id)
        {
            Assert.That(bootstrap.Current.Samples.Analyze(id).Succeeded, Is.True);
            clock.Seconds += .5;
            bootstrap.Current.Tick();
            Assert.That(bootstrap.Current.Samples.Review(id).Succeeded, Is.True);
        }

        private void ReviewValidResults()
        {
            CollectAll();
            Analyze(StationSamples.Blank);
            Assert.That(bootstrap.Current.Samples.CleanForRetry().Succeeded, Is.True);
            Analyze(StationSamples.Blank);
            foreach (var specimen in FictionalSample.All.Where(item => item.Id != StationSamples.Blank))
                Analyze(specimen.Id);
            Assert.That(bootstrap.Current.Samples.AllReviewed, Is.True);
        }

        private void Start(SampleCheckpoint samples = null, ResearchDraft report = null, StableId[] progress = null)
        {
            var clues = EchoInvestigation.RequiredClues;
            Assert.That(bootstrap.RestartWithPower(new Catalog(), Array.Empty<StableId>(),
                () => Array.Empty<IInteractable>(), _ => ResultCode.Unavailable,
                powerCheckpoint: new PowerCheckpoint(0, 0, new[] { StationPower.Lab }), clock: clock,
                questCheckpoint: progress,
                archiveCheckpoint: new[] { ArchiveId }, investigationCheckpoint: new EchoInvestigationCheckpoint(clues, clues),
                sampleCheckpoint: samples, researchCheckpoint: report), Is.Empty);
        }

        private sealed class Clock : ISessionClock { public double Seconds { get; set; } }
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
