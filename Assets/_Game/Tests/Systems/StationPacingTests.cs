using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace LKS.Tests
{
    public sealed class StationPacingTests
    {
        private GameObject host;
        private SessionBootstrap bootstrap;
        private readonly Clock clock = new Clock();
        private static readonly StableId Sampling = new StableId("quest.sampling");

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Pacing tests");
            bootstrap = host.AddComponent<SessionBootstrap>();
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(host);

        [Test]
        public void EquivalentFrameSchedulesPreservePowerAndSeparateReadingAndWaiting()
        {
            var single = RunScenario(false);
            var dense = RunScenario(true);
            Assert.That(dense.Power.BatteryKwh, Is.EqualTo(single.Power.BatteryKwh));
            Assert.That(dense.Power.PendingSeconds, Is.EqualTo(single.Power.PendingSeconds));
            Assert.That(dense.Power.Requested, Is.EqualTo(single.Power.Requested));
            Assert.That(dense.Air.AirReserve, Is.EqualTo(single.Air.AirReserve));
            Assert.That(dense.Times, Is.EqualTo(single.Times));
            Assert.That(single.Times[0].ActiveSeconds, Is.EqualTo(2m));
            Assert.That(single.Times[0].ReadingSeconds, Is.Zero);
            Assert.That(single.Times[1].ActiveSeconds, Is.EqualTo(2.5m));
            Assert.That(single.Times[1].ReadingSeconds, Is.EqualTo(6m));
            Assert.That(single.Times[1].WaitingSeconds, Is.EqualTo(3m));
        }

        [Test]
        public void RepeatedBlockedActionsOfferHintsWithoutChangingTheQuestRoute()
        {
            var run = Start();
            Assert.That(run.TryGetTarget(Sampling, out var target), Is.True);
            var before = run.CaptureQuestCheckpoint();
            var previousPower = run.Power.Snapshot;
            for (int attempt = 1; attempt <= StationPacing.HintThreshold; attempt++)
            {
                var state = target.Read(StationQuests.Player);
                Assert.That(state.Available, Is.False);
                Assert.That(target.Request(StationQuests.Player, StationQuests.Confirm).Code,
                    Is.EqualTo(ResultCode.PrerequisiteMissing));
                Assert.That(run.Pacing.TryGetHint(Sampling, out _),
                    Is.EqualTo(attempt == StationPacing.HintThreshold));
            }
            Assert.That(run.Pacing.TryGetHint(Sampling, out var hint), Is.True);
            Assert.That(run.Archive.Content.Resolve(hint, false), Does.Contain("current objective"));
            Assert.That(run.CaptureQuestCheckpoint(), Is.EqualTo(before));
            Assert.That(run.Power.Snapshot, Is.SameAs(previousPower));

            var next = Start();
            Assert.That(next.Pacing.TryGetHint(Sampling, out _), Is.False);
            Assert.That(next.Pacing.CaptureEvents(), Is.Empty);
            Assert.That(run.Pacing.TryGetHint(Sampling, out _), Is.False);
        }

        [Test]
        public void DirectClueAndSampleCommandsUseTheSameReadOnlyHintRules()
        {
            var run = Start();
            var clue = EchoInvestigation.Timing;
            var specimen = FictionalSample.All[0].Id;
            for (int i = 0; i < StationPacing.HintThreshold; i++)
                Assert.That(run.Investigation.Collect(clue).Code, Is.EqualTo(ResultCode.PrerequisiteMissing));
            Assert.That(run.Pacing.TryGetHint(clue, out var hint), Is.True);
            Assert.That(hint.value, Is.EqualTo("hint.pacing.archive"));
            Assert.That(run.Investigation.Capture().Collected, Is.Empty);

            for (int i = 0; i < StationPacing.HintThreshold; i++)
                Assert.That(run.Samples.Collect(specimen).Code, Is.EqualTo(ResultCode.PrerequisiteMissing));
            Assert.That(run.Pacing.TryGetHint(specimen, out hint), Is.True);
            Assert.That(hint.value, Is.EqualTo("hint.pacing.samples"));
            Assert.That(run.Samples.Readings[specimen].Phase, Is.EqualTo(SamplePhase.Unseen));
            Assert.That(run.Samples.RecordContext(specimen).Succeeded, Is.True);
            Assert.That(run.Pacing.TryGetHint(specimen, out _), Is.False);
        }

        [Test]
        public void RecordsDecisionsAndReportRetriesWithoutUnboundedLogsOrPreviewNoise()
        {
            var run = Start();
            var essentials = new[] { StationPower.Air, StationPower.Water };
            Assert.That(run.Power.Preview(essentials, out _).Succeeded, Is.True);
            Assert.That(run.Pacing.CaptureEvents(), Is.Empty);
            Assert.That(run.Power.Commit(essentials).Succeeded, Is.True);
            Assert.That(run.Logistics.Choose(FoodSchedule.PreserveSeedlings).Succeeded, Is.True);
            var entries = run.Pacing.CaptureEvents();
            Assert.That(entries.Select(x => x.Kind), Is.EqualTo(new[] { PacingEventKind.PowerDecision,
                PacingEventKind.PowerDecision, PacingEventKind.FoodDecision }));
            Assert.That(entries[0].Selection, Is.EqualTo("load.air,load.water"));
            Assert.That(entries[2].Selection, Is.EqualTo(nameof(FoodSchedule.PreserveSeedlings)));

            var unsupported = new ResearchDraft(Array.Empty<ReportObservation>(), ReportInterpretation.LifeConfirmed,
                default, default, default);
            Assert.That(run.Research.Evaluate(unsupported).Succeeded, Is.False);
            Assert.That(run.Pacing.CaptureEvents(), Has.Count.EqualTo(3));
            for (int i = 0; i < StationPacing.EventCapacity + 3; i++)
                Assert.That(run.Research.Submit(unsupported).Succeeded, Is.False);
            Assert.That(run.Pacing.ReportRetries, Is.EqualTo(StationPacing.EventCapacity + 2));
            Assert.That(run.Pacing.CaptureEvents(), Has.Count.EqualTo(StationPacing.EventCapacity));
            Assert.That(run.Pacing.CaptureEvents().All(x => x.Kind == PacingEventKind.ReportSubmission), Is.True);
            Assert.That(run.Pacing.TryGetHint(StationResearch.ReportId, out var hint), Is.True);
            Assert.That(run.Archive.Content.Resolve(hint, false), Does.Contain("life was present"));
            Assert.That(run.Research.CaptureAccepted(), Is.Null);
        }

        private (PowerCheckpoint Power, LifeSupportCheckpoint Air, ChapterTime[] Times) RunScenario(bool dense)
        {
            var run = Start();
            void TickTo(int quarter)
            {
                if (dense)
                    while (clock.Seconds < (quarter - 1) / 4.0)
                    {
                        clock.Seconds += .25;
                        run.Tick();
                    }
                clock.Seconds = quarter / 4.0;
                run.Tick();
            }

            TickTo(8);
            Assert.That(run.TryGetTarget(StationQuests.RequiredStages[0], out var arrival), Is.True);
            Assert.That(arrival.Request(StationQuests.Player, StationQuests.Confirm).Succeeded, Is.True);
            Assert.That(run.Power.Commit(new[] { StationPower.Air, StationPower.Water }).Succeeded, Is.True);
            TickTo(12);
            var before = run.Power.Capture();
            using (run.Archive.Open(new StableId("archive.donki-sep")))
            {
                TickTo(36);
                Assert.That(run.Power.Capture().BatteryKwh, Is.EqualTo(before.BatteryKwh));
                Assert.That(run.Power.Capture().PendingSeconds, Is.EqualTo(before.PendingSeconds));
            }

            run.SetPacingWaiting(true);
            TickTo(48);
            run.SetPacingWaiting(false);
            TickTo(54);
            return (run.Power.Capture(), run.LifeSupport.Capture(), run.Pacing.CaptureChapters().ToArray());
        }

        private GameplaySession Start()
        {
            clock.Seconds = 0;
            Assert.That(bootstrap.RestartWithPower(new Catalog(), Array.Empty<StableId>(),
                () => Array.Empty<IInteractable>(), _ => ResultCode.Applied,
                powerCheckpoint: new PowerCheckpoint(0m, 0m, Array.Empty<StableId>()), clock: clock), Is.Empty);
            return bootstrap.Current;
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
