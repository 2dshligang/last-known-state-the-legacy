using System;
using NUnit.Framework;
using UnityEngine;

namespace LKS.Tests
{
    public sealed class StationLogisticsTests
    {
        private sealed class Clock : ISessionClock { public double Seconds { get; set; } }
        private sealed class Catalog : IScienceCatalog
        {
            public bool TryGet(StableId id, out ScienceRecord record)
            {
                record = default;
                return false;
            }
        }

        [Test]
        public void FoodChoiceCanBeReversedAfterAnOverload()
        {
            var host = new GameObject("Logistics test");
            try
            {
                var bootstrap = host.AddComponent<SessionBootstrap>();
                Start(bootstrap, new Clock(), new PowerCheckpoint(0m, 0m,
                    new[] { StationPower.Air, StationPower.Water, StationPower.Archive }));
                var run = bootstrap.Current;
                Assert.That(run.Logistics.Choose(FoodSchedule.PreserveSeedlings).Succeeded, Is.True);
                Assert.That(run.Logistics.SeedlingPlanPowered, Is.True);
                Assert.That(run.Logistics.Choose(FoodSchedule.RunLab).Code, Is.EqualTo(ResultCode.InsufficientResources));
                Assert.That(run.Logistics.Capture().Schedule, Is.EqualTo(FoodSchedule.PreserveSeedlings));
                Assert.That(run.Power.RequestLoad(StationPower.Archive, false).Succeeded, Is.True);
                Assert.That(run.Logistics.Choose(FoodSchedule.RunLab).Succeeded, Is.True);
                Assert.That(run.Logistics.SeedlingPlanPowered, Is.False);
                Assert.That(run.Logistics.Choose(FoodSchedule.PreserveSeedlings).Succeeded, Is.True);
                Assert.That(run.Logistics.SeedlingPlanPowered, Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [Test]
        public void QueuedReportSurvivesPauseOutageAndReloadWithoutDuplicateDelivery()
        {
            var host = new GameObject("Logistics test");
            try
            {
                var bootstrap = host.AddComponent<SessionBootstrap>();
                var clock = new Clock();
                Start(bootstrap, clock, new PowerCheckpoint(0m, 0m, Array.Empty<StableId>()));
                var run = bootstrap.Current;
                var message = StationLogistics.ReportMessage;
                Assert.That(run.Logistics.QueueReport(message).Succeeded, Is.True);
                clock.Seconds = 2;
                run.Tick();
                Assert.That(run.Logistics.Capture().RemainingSeconds, Is.EqualTo(30));
                Assert.That(run.Power.RequestLoad(StationPower.Transmit, true).Succeeded, Is.True);
                clock.Seconds = 3;
                run.Tick();
                Assert.That(run.Logistics.Capture().RemainingSeconds, Is.EqualTo(20));
                run.SetPowerReadingPaused(true);
                clock.Seconds = 10;
                run.Tick();
                Assert.That(run.Logistics.Capture().RemainingSeconds, Is.EqualTo(20));
                var saved = run.Logistics.Capture();
                Assert.That(saved.MessageId, Is.EqualTo(message));
                Start(bootstrap, clock, run.Power.Capture(), saved);
                run = bootstrap.Current;
                Assert.That(run.Logistics.QueueReport(message).Code, Is.EqualTo(ResultCode.AlreadyApplied));
                clock.Seconds = 12;
                run.Tick();
                Assert.That(run.Logistics.Capture().Delivery, Is.EqualTo(DeliveryState.Delivered));
                Start(bootstrap, clock, run.Power.Capture(), run.Logistics.Capture());
                run = bootstrap.Current;
                Assert.That(run.Logistics.QueueReport(message).Code, Is.EqualTo(ResultCode.AlreadyApplied));
                Assert.That(run.Logistics.Capture().RemainingSeconds, Is.Zero);
                Assert.That(run.Logistics.Capture().Delivery, Is.EqualTo(DeliveryState.Delivered));
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        private static void Start(SessionBootstrap bootstrap, Clock clock, PowerCheckpoint power,
            LogisticsCheckpoint logistics = null)
        {
            var errors = bootstrap.RestartWithPower(new Catalog(), Array.Empty<StableId>(),
                () => Array.Empty<IInteractable>(), _ => ResultCode.Unavailable, power,
                clock: clock, logisticsCheckpoint: logistics);
            Assert.That(errors, Is.Empty);
        }
    }
}
