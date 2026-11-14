using System;
using System.Collections.Generic;
using System.Linq;

namespace LKS
{
    public sealed class PowerCheckpoint
    {
        public const int SchemaVersion = 1;
        public decimal BatteryKwh { get; }
        public decimal PendingSeconds { get; }
        public bool BatteryReserveMode { get; }
        public IReadOnlyList<StableId> Requested { get; }

        public PowerCheckpoint(decimal batteryKwh, decimal pendingSeconds, IEnumerable<StableId> requested, bool batteryReserveMode = false)
        {
            BatteryKwh = batteryKwh;
            PendingSeconds = pendingSeconds;
            BatteryReserveMode = batteryReserveMode;
            Requested = Array.AsReadOnly(requested.ToArray());
        }
    }

    public sealed class StationPower : IPowerService, IDisposable
    {
        public const EvidenceKind Classification = EvidenceKind.EngineeringModel;
        public const decimal StepSeconds = 1m;
        public const decimal SimulationSecondsPerGameSecond = 10m;
        private const decimal Capacity = 2m;
        private const decimal Generation = 6m;
        private const decimal Efficiency = 0.9m;
        private const decimal PowerRoundingToleranceKw = 0.000000001m;

        public static readonly StableId Air = new StableId("load.air");
        public static readonly StableId Water = new StableId("load.water");
        public static readonly StableId Archive = new StableId("load.archive");
        public static readonly StableId Lab = new StableId("load.lab");
        public static readonly StableId Seedlings = new StableId("load.seedling-preservation");
        public static readonly StableId Transmit = new StableId("load.transmit");
        public static readonly StableId ShelterActuator = new StableId("load.shelter-actuator");
        private static readonly StableId Network = new StableId("power.station");
        private static readonly StableId Allocation = new StableId("cause.power-allocation");
        private static readonly StableId Simulation = new StableId("cause.power-step");

        // In priority order, with equal priorities resolved by ordinal stable ID.
        private static readonly (StableId Id, decimal Kw)[] Loads =
        {
            (Air, 2m), (Water, 1m), (Seedlings, 1m), (Archive, 2m), (Lab, 3m), (ShelterActuator, 1m), (Transmit, 2m)
        };

        private readonly Func<long> nextSequence;
        private readonly Action<Action> enqueue;
        private readonly List<Subscription> observers = new List<Subscription>();
        private HashSet<StableId> requested;
        private decimal battery;
        private decimal pendingSeconds;
        private bool disposed;
        private bool batteryReserveMode;

        internal Action<decimal, bool, bool> SupplyChanged { get; set; }

        public PowerSnapshot Snapshot { get; private set; }
        public bool EssentialSupportSupplied =>
            Snapshot.Loads.Where(load => load.Id == Air || load.Id == Water).All(load => load.Supplied);

        internal StationPower(Func<long> nextSequence, Action<Action> enqueue, PowerCheckpoint checkpoint)
        {
            this.nextSequence = nextSequence;
            this.enqueue = enqueue;
            battery = checkpoint?.BatteryKwh ?? 1.5m;
            pendingSeconds = checkpoint?.PendingSeconds ?? 0m;
            if (battery < 0m || battery > Capacity || pendingSeconds < 0m || pendingSeconds >= StepSeconds ||
                (checkpoint != null && ReadAllocation(checkpoint.Requested, out _) != ResultCode.Applied))
                throw new ArgumentException("Invalid station power checkpoint.", nameof(checkpoint));
            requested = new HashSet<StableId>(checkpoint?.Requested ?? Array.Empty<StableId>());
            batteryReserveMode = checkpoint?.BatteryReserveMode ?? false;
            Snapshot = Project(requested, batteryReserveMode);
        }

        public PowerCheckpoint Capture() =>
            new PowerCheckpoint(battery, pendingSeconds, Loads.Select(load => load.Id).Where(requested.Contains), batteryReserveMode);

        public CommandResult Preview(IEnumerable<StableId> allocation, out PowerSnapshot preview)
        {
            preview = Snapshot;
            if (disposed)
                return Result(ResultCode.Unavailable, "unavailable");
            var code = ReadAllocation(allocation, out var selection);
            if (code != ResultCode.Applied)
                return AllocationError(code);
            preview = Project(selection);
            return Outcome(preview);
        }

        public CommandResult Commit(IEnumerable<StableId> allocation)
        {
            if (disposed)
                return Result(ResultCode.Unavailable, "unavailable");
            var code = ReadAllocation(allocation, out var selection);
            if (code != ResultCode.Applied)
                return AllocationError(code);
            if (!batteryReserveMode && requested.SetEquals(selection))
                return Result(ResultCode.AlreadyApplied, "unchanged");

            var projection = Project(selection);
            var outcome = Outcome(projection);
            if (!outcome.Succeeded)
                return outcome;
            requested = selection;
            batteryReserveMode = false;
            Publish(projection, Allocation);
            ReportSupply(0m, projection);
            return outcome;
        }

        public CommandResult RequestLoad(StableId load, bool enabled)
        {
            if (disposed)
                return Result(ResultCode.Unavailable, "unavailable");
            if (!Loads.Any(definition => definition.Id == load))
                return Result(ResultCode.UnknownId, "unknown-load");
            var selection = new HashSet<StableId>(requested);
            if (enabled)
                selection.Add(load);
            else
                selection.Remove(load);
            return Commit(selection);
        }

        public CommandResult Recover() => Commit(new[] { Air, Water });

        internal void Advance(decimal seconds)
        {
            if (seconds < 0m)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            decimal steps = decimal.Floor((pendingSeconds + seconds) / StepSeconds);
            pendingSeconds = (pendingSeconds + seconds) % StepSeconds;
            if (steps == 0m)
                return;

            decimal initial = battery;
            bool initialReserveMode = batteryReserveMode;
            while (steps > 0m)
            {
                decimal demand = RequestedDemand(requested);
                if (demand > Generation + battery * (3600m * Efficiency) + PowerRoundingToleranceKw)
                    batteryReserveMode = true;
                List<LoadState> states = SupplyChanged == null ? null : new List<LoadState>(Loads.Length);
                decimal supplied = Allocate(requested, states, batteryReserveMode);
                if (states != null)
                    SupplyChanged.Invoke(StepSeconds, IsSupplied(states, Air) && IsSupplied(states, Water),
                        IsSupplied(states, ShelterActuator));
                decimal balance = Generation - supplied;
                decimal delta = balance >= 0m ? balance * Efficiency / 3600m : balance / (3600m * Efficiency);
                decimal next = Math.Max(0m, Math.Min(Capacity, battery + delta));
                if (next < 0.000000000001m)
                    next = 0m;
                if (next == battery && SupplyChanged == null)
                    break;
                battery = next;
                steps--;
            }
            if (battery != initial || batteryReserveMode != initialReserveMode)
                Publish(Project(requested, batteryReserveMode), Simulation);
            ReportSupply(0m, Snapshot);
        }

        internal void ReportSupply(decimal duration, PowerSnapshot snapshot) =>
            SupplyChanged?.Invoke(duration, IsSupplied(snapshot.Loads, Air) && IsSupplied(snapshot.Loads, Water),
                IsSupplied(snapshot.Loads, ShelterActuator));

        private static bool IsSupplied(IEnumerable<LoadState> loads, StableId id) =>
            loads.Any(load => load.Id == id && load.Supplied);

        private PowerSnapshot Project(HashSet<StableId> selection, bool reserveMode = false)
        {
            var states = new List<LoadState>(Loads.Length);
            Allocate(selection, states, reserveMode);
            decimal demand = RequestedDemand(selection);
            return new PowerSnapshot((double)Generation, (double)demand, (double)battery, (double)Capacity, states);
        }

        private static decimal RequestedDemand(HashSet<StableId> selection) =>
            Loads.Where(load => selection.Contains(load.Id)).Sum(load => load.Kw);

        private decimal Allocate(HashSet<StableId> selection, List<LoadState> states, bool reserveMode)
        {
            decimal budget = reserveMode ? Generation : Generation + battery * (3600m * Efficiency);
            decimal supplied = 0m;
            foreach (var load in Loads)
            {
                bool wanted = selection.Contains(load.Id);
                bool powered = wanted && load.Kw <= budget - supplied + PowerRoundingToleranceKw;
                if (powered)
                    supplied += load.Kw;
                states?.Add(new LoadState(load.Id, wanted, powered));
            }
            return supplied;
        }

        private static ResultCode ReadAllocation(IEnumerable<StableId> allocation, out HashSet<StableId> selection)
        {
            selection = new HashSet<StableId>();
            if (allocation == null)
                return ResultCode.InvalidRequest;
            foreach (var id in allocation)
            {
                if (!Loads.Any(load => load.Id == id))
                    return ResultCode.UnknownId;
                if (!selection.Add(id))
                    return ResultCode.InvalidRequest;
            }
            return ResultCode.Applied;
        }

        private static CommandResult Outcome(PowerSnapshot preview) =>
            preview.Loads.Any(load => load.Requested && !load.Supplied)
                ? Result(ResultCode.InsufficientResources, "insufficient")
                : Result(ResultCode.Applied, "applied");

        private static CommandResult AllocationError(ResultCode code) =>
            Result(code, code == ResultCode.UnknownId ? "unknown-load" : "invalid-allocation");

        private static CommandResult Result(ResultCode code, string key) =>
            new CommandResult(code, new StableId("power.result." + key));

        public IDisposable Subscribe(Action<StateChange<PowerSnapshot>> observer)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(StationPower));
            var subscription = new Subscription(observers, observer ?? throw new ArgumentNullException(nameof(observer)));
            observers.Add(subscription);
            return subscription;
        }

        private void Publish(PowerSnapshot snapshot, StableId cause)
        {
            Snapshot = snapshot;
            var change = new StateChange<PowerSnapshot>(nextSequence(), Network, cause, snapshot);
            foreach (var observer in observers.ToArray())
                enqueue(() => observer.Notify(change));
        }

        public void Dispose()
        {
            disposed = true;
            SupplyChanged = null;
            foreach (var observer in observers.ToArray())
                observer.Dispose();
        }

        private sealed class Subscription : IDisposable
        {
            private readonly List<Subscription> owner;
            private Action<StateChange<PowerSnapshot>> observer;

            public Subscription(List<Subscription> owner, Action<StateChange<PowerSnapshot>> observer)
            {
                this.owner = owner;
                this.observer = observer;
            }

            public void Notify(StateChange<PowerSnapshot> change) => observer?.Invoke(change);

            public void Dispose()
            {
                owner.Remove(this);
                observer = null;
            }
        }
    }
}
