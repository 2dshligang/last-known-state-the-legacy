using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace LKS.Tests
{
    public sealed class StationPowerTests
    {
        private GameObject host;
        private SessionBootstrap bootstrap;
        private Clock clock;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Power test");
            bootstrap = host.AddComponent<SessionBootstrap>();
            clock = new Clock();
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(host);

        [Test]
        public void PreviewAndRejectedCommitLeaveAllocationUnchanged()
        {
            var power = Start(0m);
            var before = power.Snapshot;
            var selection = new[] { StationPower.Lab, StationPower.Archive, StationPower.Transmit };
            Assert.That(power.Preview(selection, out var preview).Code, Is.EqualTo(ResultCode.InsufficientResources));
            Assert.That(preview.Loads.Where(x => x.Supplied).Select(x => x.Id),
                Is.EqualTo(new[] { StationPower.Archive, StationPower.Lab }));
            Assert.That(power.Commit(selection).Code, Is.EqualTo(ResultCode.InsufficientResources));
            Assert.That(power.Commit(null).Code, Is.EqualTo(ResultCode.InvalidRequest));
            Assert.That(power.Commit(new[] { StationPower.Air, StationPower.Air }).Code, Is.EqualTo(ResultCode.InvalidRequest));
            Assert.That(power.RequestLoad(new StableId("load.unknown"), true).Code, Is.EqualTo(ResultCode.UnknownId));
            Assert.That(power.Snapshot, Is.SameAs(before));
            Assert.That(bootstrap.RestartWithPower(new Catalog(), Array.Empty<StableId>(),
                () => Array.Empty<IInteractable>(), _ => ResultCode.Unavailable,
                new PowerCheckpoint(-1m, 0m, Array.Empty<StableId>()), clock: clock), Is.Not.Empty);
            Assert.That(bootstrap.Current.Power, Is.SameAs(power));
        }

        [Test]
        public void ConvertsSecondsToEnergyAndCapsRecharge()
        {
            var power = Start(0m);
            AdvanceTo(60);
            Assert.That(power.Snapshot.BatteryKilowattHours, Is.EqualTo(0.9).Within(1e-12));
            AdvanceTo(100000);
            Assert.That(power.Snapshot.BatteryKilowattHours, Is.EqualTo(2));
        }

        [Test]
        public void ExactDepletionShedsLoadAndRecoveryKeepsEssentialsSupplied()
        {
            var power = Start(0.05m);
            var selection = new[] { StationPower.Air, StationPower.Water, StationPower.Lab, StationPower.Transmit };
            Assert.That(power.Commit(selection).Succeeded, Is.True);
            AdvanceTo(8.1);
            Assert.That(power.Snapshot.BatteryKilowattHours, Is.Zero.Within(1e-12));
            Assert.That(power.Snapshot.Loads.Single(x => x.Id == StationPower.Transmit).Supplied, Is.False);
            AdvanceTo(10000);
            Assert.That(power.Snapshot.Loads.Single(x => x.Id == StationPower.Transmit).Supplied, Is.False);
            Assert.That(power.Recover().Succeeded, Is.True);
            Assert.That(power.EssentialSupportSupplied, Is.True);
        }

        [Test]
        public void FrameSchedulesPauseAndCheckpointPreserveFixedStepState()
        {
            var selection = new[] { StationPower.Air };
            var power = Start(0m, selection);
            AdvanceTo(7.55);
            var single = power.Capture();

            clock.Seconds = 0;
            power = Start(0m, selection);
            for (int frame = 1; frame <= 755; frame++)
                AdvanceTo(frame / 100.0);
            Assert.That(power.Capture().BatteryKwh, Is.EqualTo(single.BatteryKwh));
            Assert.That(power.Capture().PendingSeconds, Is.EqualTo(single.PendingSeconds));

            bootstrap.Current.SetPowerReadingPaused(true);
            AdvanceTo(50);
            bootstrap.Current.SetPowerReadingPaused(false);
            Assert.That(power.Capture().BatteryKwh, Is.EqualTo(single.BatteryKwh));
            Assert.That(power.Capture().PendingSeconds, Is.EqualTo(single.PendingSeconds));

            power = Start(single.BatteryKwh, single.Requested.ToArray(), single.PendingSeconds);
            Assert.That(power.Capture().Requested, Is.EqualTo(selection));
            AdvanceTo(50.05);
            Assert.That(power.Capture().PendingSeconds, Is.Zero);
            Assert.That(power.Snapshot.BatteryKilowattHours, Is.GreaterThan((double)single.BatteryKwh));
        }

        private StationPower Start(decimal battery, StableId[] loads = null, decimal pending = 0m)
        {
            var checkpoint = new PowerCheckpoint(battery, pending, loads ?? Array.Empty<StableId>());
            var errors = bootstrap.RestartWithPower(new Catalog(), Array.Empty<StableId>(),
                () => Array.Empty<IInteractable>(), _ => ResultCode.Unavailable, checkpoint, clock: clock);
            Assert.That(errors, Is.Empty);
            return bootstrap.Current.Power;
        }

        private void AdvanceTo(double seconds)
        {
            clock.Seconds = seconds;
            bootstrap.Current.Tick();
        }

        private sealed class Clock : ISessionClock
        {
            public double Seconds { get; set; }
        }

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
