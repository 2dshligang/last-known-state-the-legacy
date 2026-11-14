using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace LKS.Tests
{
    public sealed class StationEndingTests
    {
        private static readonly StableId ArchiveId = new StableId("archive.donki-sep");
        private static readonly PowerCheckpoint SafePower = new PowerCheckpoint(1m, 0m,
            new[] { StationPower.Air, StationPower.Water });
        private static readonly SampleCheckpoint ReviewedSamples = new SampleCheckpoint(
            FictionalSample.All.ToDictionary(sample => sample.Id, _ => SamplePhase.Reviewed),
            ControlStatus.Valid, default, 0, true);
        private static readonly ResearchDraft Report = new ResearchDraft(
            new[] { ReportObservation.LayersVisible, ReportObservation.IronMineralSigns, ReportObservation.BlankPassed },
            ReportInterpretation.SedimentMayHaveSettled, ReportAlternative.WindDeposition,
            ReportUncertainty.OriginUncertain, ReportFollowUp.CompareNearbyLayersWithBlank);

        private GameObject host;
        private SessionBootstrap bootstrap;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Ending tests");
            bootstrap = host.AddComponent<SessionBootstrap>();
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(host);

        [TestCase("restore-air")]
        [TestCase("confirm-echo")]
        [TestCase("review-samples")]
        [TestCase("accept-report")]
        [TestCase("queue-report")]
        [TestCase("finish-objectives")]
        public void PhotographReturnsTheFirstUnmetRequirement(string missing)
        {
            var samples = missing == "review-samples" ? null : ReviewedSamples;
            bool confirmed = missing != "confirm-echo";
            var report = confirmed && samples != null && missing != "accept-report" ? Report : null;
            var progress = missing == "queue-report" ? StationQuests.RequiredStages.Take(6).ToArray() : null;
            var run = Start(missing == "restore-air" ? null : SafePower, confirmed,
                samples, report, progress, missing == "finish-objectives" ? Queued() : null);

            var target = GetPhotograph(run);
            var key = new StableId("ending.result." + missing);
            Assert.That(run.Ending.Readiness().MessageKey, Is.EqualTo(key));
            Assert.That(target.Read(StationQuests.Player).Available, Is.False);
            Assert.That(target.Read(StationQuests.Player).PromptKey, Is.EqualTo(key));
            Assert.That(Inspect(target).MessageKey, Is.EqualTo(key));
            Assert.That(run.Archive.Content.Resolve(key, false), Is.Not.Empty);
            Assert.That(run.Ending.Completion.Snapshot, Is.False);
            Assert.That(run.Ending.TryGetFinalCue(out _), Is.False);
            if (missing == "accept-report")
            {
                Assert.That(run.Ending.QueueAcceptedReport().Succeeded, Is.False);
                Assert.That(run.Logistics.Delivery, Is.EqualTo(DeliveryState.NotQueued));
            }
        }

        [TestCase(DeliveryState.Queued)]
        [TestCase(DeliveryState.Delivered)]
        public void InspectionPersistsBeforeDispatchAndReplaysWithoutCompletingAgain(DeliveryState delivery)
        {
            var progress = StationQuests.RequiredStages.Take(6).ToArray();
            var run = Start(SafePower, true, ReviewedSamples, Report, progress,
                delivery == DeliveryState.Delivered ? new LogisticsCheckpoint(FoodSchedule.Unselected,
                    StationLogistics.ReportMessage, delivery, 0) : null);
            if (delivery == DeliveryState.Queued)
                Assert.That(run.Ending.QueueAcceptedReport().Code, Is.EqualTo(ResultCode.Applied));
            Assert.That(run.Ending.QueueAcceptedReport().Code, Is.EqualTo(ResultCode.AlreadyApplied));
            var photo = GetPhotograph(run);
            int events = 0;
            using var listener = run.Ending.Completion.Subscribe(change =>
            {
                Assert.That(change.Subject, Is.EqualTo(StationEnding.Photograph));
                Assert.That(change.Cause, Is.EqualTo(StationEnding.PhotographInspected));
                Assert.That(change.Snapshot, Is.True);
                events++;
            });
            Assert.That(photo.Read(StationQuests.Player).Available, Is.True);
            Assert.That(photo.Request(default, StationQuests.Confirm).Succeeded, Is.False);
            Assert.That(photo.Request(StationQuests.Player, StationQuests.Recover).Succeeded, Is.False);
            Assert.That(Inspect(photo).Code, Is.EqualTo(ResultCode.Applied));
            Assert.That(Inspect(photo).Code, Is.EqualTo(ResultCode.AlreadyApplied));
            Assert.That(events, Is.Zero);
            var checkpoint = run.CaptureQuestCheckpoint().ToArray();
            Assert.That(checkpoint.Last(), Is.EqualTo(StationEnding.Photograph));
            run.Dispatch();
            run.Dispatch();
            Assert.That(events, Is.EqualTo(1));

            // Restore the checkpoint captured during the transition with live air now degraded.
            run = Start(null, true, ReviewedSamples, Report, checkpoint, run.Logistics.Capture());
            using var restoredListener = run.Ending.Completion.Subscribe(_ => events++);
            Assert.That(run.Ending.Completion.Snapshot, Is.True);
            Assert.That(Inspect(GetPhotograph(run)).Code, Is.EqualTo(ResultCode.AlreadyApplied));
            for (int replay = 0; replay < 2; replay++)
            {
                Assert.That(run.Ending.TryGetFinalCue(out var cue), Is.True);
                Assert.That(run.Archive.Content.Resolve(cue.TextKey, false), Is.EqualTo("Dad... I did it."));
                Assert.That(cue.CameraCue, Is.EqualTo(new StableId("camera.final-photograph")));
            }
            run.Dispatch();
            Assert.That(events, Is.EqualTo(1));

            var errors = Restart(SafePower, true, ReviewedSamples, Report, checkpoint, null);
            Assert.That(errors, Is.Not.Empty);
            Assert.That(bootstrap.Current, Is.SameAs(run));
            Assert.That(Start().Ending.Completion.Snapshot, Is.False);
            Assert.That(bootstrap.Current.Ending.TryGetFinalCue(out _), Is.False);
        }

        private static LogisticsCheckpoint Queued() => new LogisticsCheckpoint(FoodSchedule.Unselected,
            StationLogistics.ReportMessage, DeliveryState.Queued, 15);

        private static IInteractable GetPhotograph(GameplaySession run)
        {
            Assert.That(run.TryGetTarget(StationEnding.Photograph, out var target), Is.True);
            return target;
        }

        private static CommandResult Inspect(IInteractable target) =>
            target.Request(StationQuests.Player, StationQuests.Confirm);

        private GameplaySession Start(PowerCheckpoint power = null, bool confirmed = false,
            SampleCheckpoint samples = null, ResearchDraft research = null,
            StableId[] quests = null, LogisticsCheckpoint logistics = null)
        {
            Assert.That(Restart(power, confirmed, samples, research, quests, logistics), Is.Empty);
            return bootstrap.Current;
        }

        private System.Collections.Generic.IReadOnlyList<string> Restart(PowerCheckpoint power, bool confirmed,
            SampleCheckpoint samples, ResearchDraft research, StableId[] quests, LogisticsCheckpoint logistics)
        {
            var clues = EchoInvestigation.RequiredClues;
            return bootstrap.RestartWithPower(new Catalog(), Array.Empty<StableId>(),
                () => Array.Empty<IInteractable>(), _ => ResultCode.Unavailable,
                powerCheckpoint: power, questCheckpoint: quests, clock: new Clock(), logisticsCheckpoint: logistics,
                archiveCheckpoint: new[] { ArchiveId }, investigationCheckpoint: confirmed
                    ? new EchoInvestigationCheckpoint(clues, clues) : null,
                sampleCheckpoint: samples, researchCheckpoint: research);
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
