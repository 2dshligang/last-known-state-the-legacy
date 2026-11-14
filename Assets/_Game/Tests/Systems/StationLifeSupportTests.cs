using System;
using NUnit.Framework;
using UnityEngine;

namespace LKS.Tests
{
    public sealed class StationLifeSupportTests
    {
        private GameObject host;
        private SessionBootstrap bootstrap;
        private Clock clock;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Life support test");
            bootstrap = host.AddComponent<SessionBootstrap>();
            clock = new Clock();
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(host);

        [Test]
        public void ExhaustedReserveRecoversAfterPowerReturns()
        {
            Start();
            Advance(20);
            Assert.That(bootstrap.Current.LifeSupport.Snapshot.Air, Is.EqualTo(AirStatus.Degraded));
            Assert.That(bootstrap.Current.LifeSupport.Snapshot.ReserveFraction, Is.Zero);
            Assert.That(bootstrap.Current.Power.Recover().Succeeded, Is.True);
            Advance(1.3);
            Assert.That(bootstrap.Current.LifeSupport.Snapshot.Air, Is.EqualTo(AirStatus.Safe));
        }

        [Test]
        public void ShelterRejectsStaleReadingsAndResumesAfterOutageAndReload()
        {
            Start();
            var run = bootstrap.Current;
            var support = run.LifeSupport;
            Advance(1);
            Assert.That(support.ReadShelter().Phase, Is.EqualTo(ShelterPhase.Ready));
            var expired = support.ReadShelter();
            Advance(1);
            Assert.That(support.RequestShelter(expired).Code, Is.EqualTo(ResultCode.InvalidRequest));

            var old = support.ReadShelter();
            Assert.That(run.Power.RequestLoad(StationPower.ShelterActuator, true).Succeeded, Is.True);
            Assert.That(support.RequestShelter(old).Code, Is.EqualTo(ResultCode.InvalidRequest));
            Assert.That(support.RequestShelter(support.ReadShelter()).Code, Is.EqualTo(ResultCode.Applied));
            Assert.That(support.RequestShelter(support.ReadShelter()).Code, Is.EqualTo(ResultCode.AlreadyApplied));
            Advance(.2);
            Assert.That(support.Capture().ActuatorSeconds, Is.EqualTo(2m));

            run.SetPowerReadingPaused(true);
            Advance(100);
            Assert.That(support.Capture().TrainingSeconds, Is.EqualTo(22m));
            Assert.That(support.Capture().ActuatorSeconds, Is.EqualTo(2m));
            run.SetPowerReadingPaused(false);
            Assert.That(run.Power.RequestLoad(StationPower.ShelterActuator, false).Succeeded, Is.True);
            Assert.That(support.ReadShelter().Phase, Is.EqualTo(ShelterPhase.Interrupted));

            var staleRun = support.ReadShelter();
            Start(run.Power.Capture(), support.Capture());
            support = bootstrap.Current.LifeSupport;
            Assert.That(support.RequestShelter(staleRun).Code, Is.EqualTo(ResultCode.InvalidRequest));
            Advance(1);
            Assert.That(support.Capture().ActuatorSeconds, Is.EqualTo(2m));
            Assert.That(bootstrap.Current.Power.RequestLoad(StationPower.ShelterActuator, true).Succeeded, Is.True);
            Advance(.3);
            Assert.That(support.ReadShelter().Phase, Is.EqualTo(ShelterPhase.Sheltered));
            Assert.That(bootstrap.Current.Power.RequestLoad(StationPower.ShelterActuator, false).Succeeded, Is.True);
            Assert.That(support.ReadShelter().Phase, Is.EqualTo(ShelterPhase.Sheltered));
        }

        private void Start(PowerCheckpoint power = null, LifeSupportCheckpoint life = null)
        {
            var errors = bootstrap.RestartWithPower(new Catalog(), Array.Empty<StableId>(),
                () => Array.Empty<IInteractable>(), _ => ResultCode.Unavailable, power,
                clock: clock, lifeSupportCheckpoint: life);
            Assert.That(errors, Is.Empty);
        }

        private void Advance(double duration)
        {
            clock.Seconds += duration;
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
