using System;
using System.Collections.Generic;

namespace LKS
{
    public enum ShelterPhase { Waiting, Ready, Moving, Interrupted, Sheltered }

    public sealed class LifeSupportCheckpoint
    {
        public const int SchemaVersion = 1;
        public decimal AirReserve { get; }
        public decimal TrainingSeconds { get; }
        public decimal ActuatorSeconds { get; }
        public bool ShelterRequested { get; }

        public LifeSupportCheckpoint(decimal airReserve, decimal trainingSeconds,
            decimal actuatorSeconds, bool shelterRequested)
        {
            AirReserve = airReserve;
            TrainingSeconds = trainingSeconds;
            ActuatorSeconds = actuatorSeconds;
            ShelterRequested = shelterRequested;
        }
    }

    public readonly struct ShelterReading
    {
        internal StationLifeSupport Owner { get; }
        internal long Revision { get; }
        public decimal SimulationSeconds { get; }
        public ShelterPhase Phase { get; }
        public bool ActuatorPowered { get; }
        public StableId MessageKey => new StableId("shelter.phase." + Phase.ToString().ToLowerInvariant());

        internal ShelterReading(StationLifeSupport owner, long revision, decimal seconds,
            ShelterPhase phase, bool actuatorPowered)
        {
            Owner = owner;
            Revision = revision;
            SimulationSeconds = seconds;
            Phase = phase;
            ActuatorPowered = actuatorPowered;
        }
    }

    // Fictional gameplay rules for reserve and actuator timing.
    public sealed class StationLifeSupport : ILifeSupportService, IDisposable
    {
        public static readonly StableId ScenarioId = new StableId("training.donki-sep-2017");
        public const EvidenceKind ScenarioKind = EvidenceKind.Fiction;
        public const EvidenceKind TimingKind = EvidenceKind.EngineeringModel;
        public const string ArchiveEventId = "2017-09-04T23:52:00-SEP-001";
        public static readonly ScienceRecord ArchiveEvent = new ScienceRecord(
            new StableId("nasa.donki-sep-13000"), EvidenceKind.Observation,
            new Uri("https://ccmc.gsfc.nasa.gov/DONKI/view/SEP/13000/-1"),
            new DateTimeOffset(2017, 9, 4, 23, 52, 0, TimeSpan.Zero));

        private const decimal BriefingSeconds = 10m;
        private const decimal TravelSeconds = 5m;
        private const decimal ReadingLifetimeSeconds = 5m;
        private readonly Func<long> nextSequence;
        private readonly Action<Action> enqueue;
        private readonly List<Subscription> observers = new List<Subscription>();
        private decimal reserve;
        private decimal seconds;
        private decimal travel;
        private bool requested;
        private bool supportPowered;
        private bool actuatorPowered;
        private bool disposed;
        private long revision;

        internal StationLifeSupport(Func<long> nextSequence, Action<Action> enqueue, LifeSupportCheckpoint checkpoint)
        {
            this.nextSequence = nextSequence;
            this.enqueue = enqueue;
            reserve = checkpoint?.AirReserve ?? 1m;
            seconds = checkpoint?.TrainingSeconds ?? 0m;
            travel = checkpoint?.ActuatorSeconds ?? 0m;
            requested = checkpoint?.ShelterRequested ?? false;
            if (reserve < 0m || reserve > 1m || seconds < 0m || seconds != decimal.Truncate(seconds) ||
                travel < 0m || travel > TravelSeconds || travel != decimal.Truncate(travel) ||
                (!requested && travel != 0m) || (requested && (seconds < BriefingSeconds || travel > seconds - BriefingSeconds)))
                throw new ArgumentException("Invalid life-support checkpoint.", nameof(checkpoint));
        }

        public LifeSupportSnapshot Snapshot => new LifeSupportSnapshot(
            supportPowered && reserve >= .25m ? AirStatus.Safe : AirStatus.Degraded, (double)reserve);

        private ShelterPhase Phase => seconds < BriefingSeconds ? ShelterPhase.Waiting
            : !requested ? ShelterPhase.Ready
            : travel >= TravelSeconds ? ShelterPhase.Sheltered
            : actuatorPowered ? ShelterPhase.Moving : ShelterPhase.Interrupted;

        public ShelterReading ReadShelter() =>
            new ShelterReading(this, revision, seconds, Phase, actuatorPowered);

        public LifeSupportCheckpoint Capture() => new LifeSupportCheckpoint(reserve, seconds, travel, requested);

        public CommandResult RequestShelter(ShelterReading reading)
        {
            if (disposed)
                return Result(ResultCode.Unavailable, "unavailable");
            if (reading.Owner != this || reading.Revision != revision || seconds - reading.SimulationSeconds > ReadingLifetimeSeconds)
                return Result(ResultCode.InvalidRequest, "stale-reading");
            if (seconds < BriefingSeconds)
                return Result(ResultCode.PrerequisiteMissing, "wait-for-briefing");
            if (requested)
                return Result(ResultCode.AlreadyApplied, "already-requested");
            if (!actuatorPowered)
                return Result(ResultCode.InsufficientResources, "actuator-unpowered");

            requested = true;
            revision++;
            Publish("cause.shelter-request");
            return Result(ResultCode.Applied, "started");
        }

        internal void Advance(decimal duration, bool support, bool actuator)
        {
            var previous = Snapshot;
            var oldPhase = Phase;
            bool actuatorChanged = actuatorPowered != actuator;
            supportPowered = support;
            actuatorPowered = actuator;
            if (duration > 0m)
            {
                reserve = Math.Max(0m, Math.Min(1m, reserve + duration * (support ? .02m : -.01m)));
                seconds += duration;
                if (requested && actuator)
                    travel = Math.Min(TravelSeconds, travel + duration);
            }

            bool shelterChanged = oldPhase != Phase || actuatorChanged;
            if (shelterChanged)
                revision++;
            if (shelterChanged || previous.Air != Snapshot.Air || previous.ReserveFraction != Snapshot.ReserveFraction)
                Publish("cause.life-support-step");
        }

        public IDisposable Subscribe(Action<StateChange<LifeSupportSnapshot>> observer)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(StationLifeSupport));
            var subscription = new Subscription(observers, observer ?? throw new ArgumentNullException(nameof(observer)));
            observers.Add(subscription);
            return subscription;
        }

        private void Publish(string cause)
        {
            var change = new StateChange<LifeSupportSnapshot>(nextSequence(),
                new StableId("life-support.station"), new StableId(cause), Snapshot);
            foreach (var observer in observers.ToArray())
                enqueue(() => observer.Notify(change));
        }

        private static CommandResult Result(ResultCode code, string key) =>
            new CommandResult(code, new StableId("shelter.result." + key));

        public void Dispose()
        {
            disposed = true;
            foreach (var observer in observers.ToArray())
                observer.Dispose();
        }

        private sealed class Subscription : IDisposable
        {
            private readonly List<Subscription> owner;
            private Action<StateChange<LifeSupportSnapshot>> observer;

            public Subscription(List<Subscription> owner, Action<StateChange<LifeSupportSnapshot>> observer)
            {
                this.owner = owner;
                this.observer = observer;
            }

            public void Notify(StateChange<LifeSupportSnapshot> change) => observer?.Invoke(change);

            public void Dispose()
            {
                owner.Remove(this);
                observer = null;
            }
        }
    }
}
