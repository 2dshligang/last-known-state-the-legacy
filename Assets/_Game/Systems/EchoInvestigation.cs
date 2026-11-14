using System;
using System.Collections.Generic;
using System.Linq;

namespace LKS
{
    public enum EchoConclusion { Unexamined, Tentative, Confirmed }
    public enum ReadingFreshness { Unknown, Fresh, Uncertain, Stale }

    public sealed class EchoInvestigationCheckpoint
    {
        public const int SchemaVersion = 1;
        public IReadOnlyList<StableId> Collected { get; }
        public IReadOnlyList<StableId> AcceptedOrder { get; }

        public EchoInvestigationCheckpoint(IEnumerable<StableId> collected, IEnumerable<StableId> acceptedOrder)
        {
            Collected = Array.AsReadOnly(collected.ToArray());
            AcceptedOrder = Array.AsReadOnly(acceptedOrder.ToArray());
        }
    }

    public sealed class EchoInvestigation
    {
        public static readonly StableId Timing = new StableId("echo.stale-input");
        public static readonly StableId Decision = new StableId("echo.controller-decision");
        public static readonly StableId Consequence = new StableId("echo.backup-exhausted");
        private static readonly IReadOnlyList<StableId> Clues = Array.AsReadOnly(new[] { Timing, Decision, Consequence });
        public static IReadOnlyList<StableId> RequiredClues => Clues;
        public static readonly TimeSpan MaximumControlAge = TimeSpan.FromMinutes(1);
        public static readonly TimeSpan TimestampUncertainty = TimeSpan.FromSeconds(15);
        public const EvidenceKind FreshnessRuleKind = EvidenceKind.EngineeringModel;

        private readonly StationArchive archive;
        private readonly HashSet<StableId> collected = new HashSet<StableId>();
        private bool confirmed;
        private bool stopped;

        internal EchoInvestigation(StationArchive archive, EchoInvestigationCheckpoint checkpoint)
        {
            this.archive = archive;
            if (checkpoint == null)
                return;

            StableId.ValidateUnique(checkpoint.Collected);
            if (checkpoint.Collected.Any(id => !Clues.Contains(id)))
                throw new ArgumentException("ECHO checkpoint contains an unknown clue.", nameof(checkpoint));

            if (checkpoint.Collected.Count > 0 && !archive.RequiredEntryAcknowledged)
                throw new ArgumentException("Read the required archive entry before restoring ECHO clues.", nameof(checkpoint));

            collected.UnionWith(checkpoint.Collected);
            if (checkpoint.AcceptedOrder.Count == 0)
                return;
            if (Validate(checkpoint.AcceptedOrder).Code != ResultCode.Applied)
                throw new ArgumentException("ECHO checkpoint contains an unsupported conclusion.", nameof(checkpoint));

            confirmed = true;
        }

        public EchoConclusion Conclusion => confirmed ? EchoConclusion.Confirmed :
            collected.Count == 0 ? EchoConclusion.Unexamined : EchoConclusion.Tentative;

        public ReadingFreshness ControlReadingFreshness => EvaluateFreshness(
            StationEcho.Timeline.Single(frame => frame.Evidence.Id == Timing).Evidence.RecordedAt,
            StationEcho.Timeline.Single(frame => frame.Evidence.Id == Decision).Evidence.RecordedAt);

        public static ReadingFreshness EvaluateFreshness(DateTimeOffset? reading, DateTimeOffset? decision)
        {
            if (!reading.HasValue || !decision.HasValue || decision.Value < reading.Value)
                return ReadingFreshness.Unknown;
            var age = decision.Value - reading.Value;
            if (age - TimestampUncertainty > MaximumControlAge)
                return ReadingFreshness.Stale;
            if (age + TimestampUncertainty <= MaximumControlAge)
                return ReadingFreshness.Fresh;
            return ReadingFreshness.Uncertain;
        }

        public CommandResult Collect(StableId id)
        {
            if (stopped)
                return Result(ResultCode.Unavailable, "unavailable");
            if (!Clues.Contains(id))
                return Result(ResultCode.UnknownId, "unknown-clue");
            if (!archive.RequiredEntryAcknowledged)
                return Result(ResultCode.PrerequisiteMissing, "read-archive");

            return collected.Add(id) ? Result(ResultCode.Applied, "collected")
                : Result(ResultCode.AlreadyApplied, "already-collected");
        }

        public CommandResult SubmitSequence(IReadOnlyList<StableId> order)
        {
            if (stopped)
                return Result(ResultCode.Unavailable, "unavailable");
            if (!archive.RequiredEntryAcknowledged)
                return Result(ResultCode.PrerequisiteMissing, "read-archive");

            var result = Validate(order);
            if (!result.Succeeded)
                return result;
            if (confirmed)
                return Result(ResultCode.AlreadyApplied, "already-confirmed");

            confirmed = true;
            return result;
        }

        public EchoInvestigationCheckpoint Capture() => new EchoInvestigationCheckpoint(
            Clues.Where(collected.Contains), confirmed ? Clues : Array.Empty<StableId>());

        internal void Stop() => stopped = true;

        private CommandResult Validate(IReadOnlyList<StableId> order)
        {
            if (order == null || order.Count > Clues.Count || order.Distinct().Count() != order.Count)
                return Result(ResultCode.InvalidRequest, "invalid-selection");
            if (order.Any(id => !Clues.Contains(id)))
                return Result(ResultCode.UnknownId, "unknown-clue");
            if (order.Count != Clues.Count || !Clues.All(collected.Contains))
                return Result(ResultCode.EvidenceInsufficient, "missing-clues");

            // The reading must precede the decision that used it. The outage follows that decision.
            int timing = Position(order, Timing);
            int decision = Position(order, Decision);
            int consequence = Position(order, Consequence);
            if (timing >= decision)
                return Result(ResultCode.InvalidRequest, "timing-before-decision");
            if (decision >= consequence)
                return Result(ResultCode.InvalidRequest, "decision-before-outage");
            if (ControlReadingFreshness != ReadingFreshness.Stale)
                return Result(ResultCode.EvidenceInsufficient, "uncertain-reading");
            return Result(ResultCode.Applied, "confirmed");
        }

        private static int Position(IReadOnlyList<StableId> order, StableId id)
        {
            for (int index = 0; index < order.Count; index++)
            {
                if (order[index] == id)
                    return index;
            }
            return -1;
        }

        private static CommandResult Result(ResultCode code, string key) =>
            new CommandResult(code, new StableId("echo.result." + key));
    }
}
