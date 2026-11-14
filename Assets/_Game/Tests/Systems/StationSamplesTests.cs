using System;
using NUnit.Framework;
using UnityEngine;

namespace LKS.Tests
{
    public sealed class StationSamplesTests
    {
        private GameObject host;
        private SessionBootstrap bootstrap;
        private Clock clock;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Sample tests");
            bootstrap = host.AddComponent<SessionBootstrap>();
            clock = new Clock();
            Start();
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(host);

        [TestCase(0, 1, 2)]
        [TestCase(0, 2, 1)]
        [TestCase(1, 0, 2)]
        [TestCase(1, 2, 0)]
        [TestCase(2, 0, 1)]
        [TestCase(2, 1, 0)]
        public void CollectionAllowsAnyOrderAndRejectsDuplicates(int first, int second, int third)
        {
            var samples = bootstrap.Current.Samples;
            foreach (int index in new[] { first, second, third })
            {
                var id = FictionalSample.All[index].Id;
                Assert.That(samples.Collect(id).Code, Is.EqualTo(ResultCode.PrerequisiteMissing));
                Collect(id);
                Assert.That(samples.Collect(id).Code, Is.EqualTo(ResultCode.AlreadyApplied));
            }
            Assert.That(samples.AllCollected, Is.True);
        }

        [Test]
        public void InvalidBlankLimitsEvidenceAndCleanRetrySupportsReview()
        {
            var samples = bootstrap.Current.Samples;
            var rock = FictionalSample.All[0].Id;
            foreach (var specimen in FictionalSample.All)
                Collect(specimen.Id);
            Assert.That(samples.Analyze(rock).Code, Is.EqualTo(ResultCode.PrerequisiteMissing));
            Analyze(StationSamples.Blank);
            Assert.That(samples.BlankControl, Is.EqualTo(ControlStatus.Invalid));
            Analyze(rock);
            var limited = samples.Readings[rock];
            Assert.That(limited.Kind, Is.EqualTo(EvidenceKind.Fiction));
            Assert.That(limited.Definition.Reference, Is.Not.Null);
            Assert.That(limited.InterpretationKey.ToString(), Is.EqualTo("sample.interpretation.limited"));
            Assert.That(samples.CleanForRetry().Succeeded, Is.True);
            Assert.That(limited.Phase, Is.EqualTo(SamplePhase.EvidenceAvailable));
            Assert.That(samples.Readings[rock].Phase, Is.EqualTo(SamplePhase.Collected));

            Analyze(StationSamples.Blank);
            Assert.That(samples.BlankControl, Is.EqualTo(ControlStatus.Valid));
            Assert.That(samples.Review(StationSamples.Blank).Succeeded, Is.True);
            foreach (var specimen in FictionalSample.All)
            {
                if (specimen.Id == StationSamples.Blank) continue;
                Analyze(specimen.Id);
                Assert.That(samples.Review(specimen.Id).Succeeded, Is.True);
            }
            Assert.That(samples.AllReviewed, Is.True);
        }

        [Test]
        public void PendingRunSurvivesOutageAndReloadAndCanBeCancelled()
        {
            var run = bootstrap.Current;
            var blank = StationSamples.Blank;
            Collect(blank);
            Assert.That(run.Samples.Analyze(blank).Succeeded, Is.True);
            Advance(.2);
            Assert.That(run.Power.RequestLoad(StationPower.Lab, false).Succeeded, Is.True);
            Advance(2);
            var saved = run.Samples.Capture();
            Assert.That(saved.RemainingSeconds, Is.EqualTo(3));
            Start(run.Power.Capture(), saved);
            run = bootstrap.Current;
            Advance(1);
            Assert.That(run.Samples.Capture().RemainingSeconds, Is.EqualTo(3));
            Assert.That(run.Samples.Cancel().Succeeded, Is.True);
            Assert.That(run.Samples.Readings[blank].Phase, Is.EqualTo(SamplePhase.Collected));
            Assert.That(run.Samples.Analyze(blank).Code, Is.EqualTo(ResultCode.InsufficientResources));
            Assert.That(run.Power.RequestLoad(StationPower.Lab, true).Succeeded, Is.True);
            Analyze(blank);
            Assert.That(run.Samples.BlankControl, Is.EqualTo(ControlStatus.Invalid));
        }

        private void Collect(StableId id)
        {
            Assert.That(bootstrap.Current.Samples.RecordContext(id).Succeeded, Is.True);
            Assert.That(bootstrap.Current.Samples.Collect(id).Succeeded, Is.True);
        }

        private void Analyze(StableId id)
        {
            Assert.That(bootstrap.Current.Samples.Analyze(id).Succeeded, Is.True);
            Advance(.5);
            Assert.That(bootstrap.Current.Samples.Readings[id].Phase, Is.EqualTo(SamplePhase.EvidenceAvailable));
        }

        private void Advance(double elapsed)
        {
            clock.Seconds += elapsed;
            bootstrap.Current.Tick();
        }

        private void Start(PowerCheckpoint power = null, SampleCheckpoint sample = null)
        {
            power ??= new PowerCheckpoint(0m, 0m, new[] { StationPower.Lab });
            Assert.That(bootstrap.RestartWithPower(new Catalog(), Array.Empty<StableId>(),
                () => Array.Empty<IInteractable>(), _ => ResultCode.Unavailable,
                powerCheckpoint: power, clock: clock, sampleCheckpoint: sample), Is.Empty);
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
