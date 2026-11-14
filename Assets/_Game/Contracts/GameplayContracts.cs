using System;
using System.Collections.Generic;

namespace LKS
{
    public static class ContractVersion
    {
        public const int Major = 1;
        public const int Minor = 0;
    }

    public enum ResultCode
    {
        Uninitialized, Applied, AlreadyApplied, InvalidRequest, UnknownId, Unavailable,
        PrerequisiteMissing, InsufficientResources, EvidenceInsufficient, IncompatibleVersion, StorageError
    }

    public readonly struct CommandResult
    {
        public ResultCode Code { get; }
        public bool Succeeded => Code == ResultCode.Applied || Code == ResultCode.AlreadyApplied;
        public StableId MessageKey { get; }

        public CommandResult(ResultCode code, StableId messageKey)
        {
            if (code == ResultCode.Uninitialized || !Enum.IsDefined(typeof(ResultCode), code))
                throw new ArgumentOutOfRangeException(nameof(code));
            if (!messageKey.IsValid)
                throw new ArgumentException("A valid message key is required.", nameof(messageKey));
            Code = code;
            MessageKey = messageKey;
        }
    }

    public readonly struct InteractionState
    {
        public bool Available { get; }
        public StableId PromptKey { get; }

        public InteractionState(bool available, StableId promptKey)
        {
            Available = available;
            PromptKey = promptKey;
        }
    }

    public readonly struct StateChange<T>
    {
        public long Sequence { get; }
        public StableId Subject { get; }
        public StableId Cause { get; }
        public T Snapshot { get; }

        public StateChange(long sequence, StableId subject, StableId cause, T snapshot)
        {
            Sequence = sequence;
            Subject = subject;
            Cause = cause;
            Snapshot = snapshot;
        }
    }

    public interface IStateSource<T>
    {
        T Snapshot { get; }
        IDisposable Subscribe(Action<StateChange<T>> observer);
    }

    public interface IInteractable
    {
        StableId Id { get; }
        InteractionState Read(StableId actor);
        CommandResult Request(StableId actor, StableId action);
    }

    public readonly struct LoadState
    {
        public StableId Id { get; }
        public bool Requested { get; }
        public bool Supplied { get; }

        public LoadState(StableId id, bool requested, bool supplied)
        {
            Id = id;
            Requested = requested;
            Supplied = supplied;
        }
    }

    public sealed class PowerSnapshot
    {
        public double GenerationKilowatts { get; }
        public double DemandKilowatts { get; }
        public double BatteryKilowattHours { get; }
        public double CapacityKilowattHours { get; }
        public IReadOnlyList<LoadState> Loads { get; }

        public PowerSnapshot(double generationKilowatts, double demandKilowatts,
            double batteryKilowattHours, double capacityKilowattHours, IEnumerable<LoadState> loads)
        {
            GenerationKilowatts = generationKilowatts;
            DemandKilowatts = demandKilowatts;
            BatteryKilowattHours = batteryKilowattHours;
            CapacityKilowattHours = capacityKilowattHours;
            Loads = new List<LoadState>(loads).AsReadOnly();
        }
    }

    public interface IPowerService : IStateSource<PowerSnapshot>
    {
        CommandResult RequestLoad(StableId load, bool enabled);
    }

    public enum AirStatus { Unknown, Safe, Degraded }

    public readonly struct LifeSupportSnapshot
    {
        public AirStatus Air { get; }
        public double? ReserveFraction { get; }

        public LifeSupportSnapshot(AirStatus air, double? reserveFraction)
        {
            Air = air;
            ReserveFraction = reserveFraction;
        }
    }

    public interface ILifeSupportService : IStateSource<LifeSupportSnapshot> { }

    public enum QuestStatus { Unknown, Locked, Available, Completed }

    public interface IQuestService : IStateSource<IReadOnlyDictionary<StableId, QuestStatus>> { }
}
