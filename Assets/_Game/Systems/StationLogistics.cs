using System;

namespace LKS
{
    public enum FoodSchedule { Unselected, PreserveSeedlings, RunLab }
    public enum DeliveryState { NotQueued, Queued, Delivered }

    public sealed class LogisticsCheckpoint
    {
        public const int SchemaVersion = 1;
        public FoodSchedule Schedule { get; }
        public StableId MessageId { get; }
        public DeliveryState Delivery { get; }
        public int RemainingSeconds { get; }

        public LogisticsCheckpoint(FoodSchedule schedule, StableId messageId,
            DeliveryState delivery, int remainingSeconds)
        {
            Schedule = schedule;
            MessageId = messageId;
            Delivery = delivery;
            RemainingSeconds = remainingSeconds;
        }
    }

    public sealed class StationLogistics
    {
        public static readonly StableId ReportMessage = new StableId("message.research-report");
        public const int NarrativeYear = 2089;
        public const int FictionalDelaySeconds = 30;
        public const EvidenceKind FoodModelKind = EvidenceKind.EngineeringModel;
        public const EvidenceKind DelayKind = EvidenceKind.Fiction;
        public const string FoodSource = "https://www.asc-csa.gc.ca/eng/youth-educators/activities/fitness-and-nutrition/interplanetary-farming.asp";
        public const string FoodExplanation = "Growing food takes time. Space crops need suitable light, water, nutrients and temperature.";

        private readonly StationPower power;
        private FoodSchedule schedule;
        private DeliveryState delivery;
        private int remaining;
        private bool disposed;

        internal StationLogistics(StationPower power, LogisticsCheckpoint checkpoint)
        {
            this.power = power;
            if (checkpoint == null)
                return;
            if (!Enum.IsDefined(typeof(FoodSchedule), checkpoint.Schedule) ||
                !Enum.IsDefined(typeof(DeliveryState), checkpoint.Delivery) ||
                (checkpoint.Delivery == DeliveryState.NotQueued ? checkpoint.MessageId != default :
                    checkpoint.MessageId != ReportMessage) ||
                (checkpoint.Delivery == DeliveryState.Queued
                    ? checkpoint.RemainingSeconds < 1 || checkpoint.RemainingSeconds > FictionalDelaySeconds
                    : checkpoint.RemainingSeconds != 0))
                throw new ArgumentException("Invalid station logistics checkpoint.", nameof(checkpoint));

            schedule = checkpoint.Schedule;
            delivery = checkpoint.Delivery;
            remaining = checkpoint.RemainingSeconds;
        }

        public bool SeedlingPlanPowered => schedule == FoodSchedule.PreserveSeedlings &&
            IsSupplied(StationPower.Seedlings);

        public LogisticsCheckpoint Capture() => new LogisticsCheckpoint(schedule,
            delivery == DeliveryState.NotQueued ? default : ReportMessage, delivery, remaining);

        public CommandResult Choose(FoodSchedule choice)
        {
            if (disposed)
                return Result(ResultCode.Unavailable, "unavailable");
            if (choice != FoodSchedule.PreserveSeedlings && choice != FoodSchedule.RunLab)
                return Result(ResultCode.InvalidRequest, "invalid-choice");

            var loads = new System.Collections.Generic.HashSet<StableId>(power.Capture().Requested);
            loads.Remove(StationPower.Seedlings);
            loads.Remove(StationPower.Lab);
            loads.Add(choice == FoodSchedule.PreserveSeedlings ? StationPower.Seedlings : StationPower.Lab);
            var result = power.Commit(loads);
            if (result.Succeeded)
                schedule = choice;
            return result;
        }

        public CommandResult QueueReport(StableId messageId)
        {
            if (disposed)
                return Result(ResultCode.Unavailable, "unavailable");
            if (messageId != ReportMessage)
                return Result(ResultCode.UnknownId, "unknown-message");
            if (delivery != DeliveryState.NotQueued)
                return Result(ResultCode.AlreadyApplied, "already-queued");

            delivery = DeliveryState.Queued;
            remaining = FictionalDelaySeconds;
            return Result(ResultCode.Applied, "queued");
        }

        internal void Advance(decimal seconds, bool transmitterPowered)
        {
            if (delivery != DeliveryState.Queued || !transmitterPowered)
                return;
            remaining = Math.Max(0, remaining - (int)seconds);
            if (remaining == 0)
                delivery = DeliveryState.Delivered;
        }

        internal void Stop() => disposed = true;

        private bool IsSupplied(StableId id)
        {
            foreach (var load in power.Snapshot.Loads)
            {
                if (load.Id == id)
                    return load.Supplied;
            }
            return false;
        }

        private static CommandResult Result(ResultCode code, string key) =>
            new CommandResult(code, new StableId("logistics.result." + key));
    }
}
