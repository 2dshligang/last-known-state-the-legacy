using System;
using System.Collections.Generic;
using System.Linq;

namespace LKS
{
    public enum PacingEventKind { BlockedInteraction, PowerDecision, FoodDecision, ReportSubmission }

    public readonly struct PacingEvent
    {
        public PacingEventKind Kind { get; }
        public StableId Subject { get; }
        public ResultCode Code { get; }
        public StableId Reason { get; }
        public decimal Seconds { get; }
        public string Selection { get; }

        internal PacingEvent(PacingEventKind kind, StableId subject, CommandResult result, decimal seconds, string selection)
        {
            Kind = kind;
            Subject = subject;
            Code = result.Code;
            Reason = result.MessageKey;
            Seconds = seconds;
            Selection = selection;
        }
    }

    public readonly struct ChapterTime
    {
        public StableId Chapter { get; }
        public decimal ActiveSeconds { get; }
        public decimal ReadingSeconds { get; }
        public decimal WaitingSeconds { get; }

        internal ChapterTime(StableId chapter, decimal activeSeconds, decimal readingSeconds, decimal waitingSeconds)
        {
            Chapter = chapter;
            ActiveSeconds = activeSeconds;
            ReadingSeconds = readingSeconds;
            WaitingSeconds = waitingSeconds;
        }
    }

    // Main-thread, per-run diagnostics. This service has no disk, network or gameplay command access.
    public sealed class StationPacing
    {
        public const int ApiVersion = 1;
        public const int EventCapacity = 64;
        public const int HintThreshold = 3;
        public static readonly StableId PowerDecision = new StableId("pacing.power-allocation");
        public static readonly StableId FoodDecision = new StableId("pacing.food-schedule");

        private readonly Queue<PacingEvent> events = new Queue<PacingEvent>(EventCapacity);
        private readonly decimal[] active = new decimal[StationQuests.RequiredStages.Count];
        private readonly decimal[] reading = new decimal[StationQuests.RequiredStages.Count];
        private readonly decimal[] waiting = new decimal[StationQuests.RequiredStages.Count];
        private StableId lastSubject, lastReason, hint;
        private int consecutiveFailures, chapter;
        private decimal elapsed;
        private bool stopped;

        public int ReportRetries { get; private set; }
        private bool reportSubmitted;

        public IReadOnlyList<PacingEvent> CaptureEvents() => Array.AsReadOnly(events.ToArray());

        public IReadOnlyList<ChapterTime> CaptureChapters() => Array.AsReadOnly(StationQuests.RequiredStages
            .Select((id, index) => new ChapterTime(id, active[index], reading[index], waiting[index])).ToArray());

        public bool TryGetHint(StableId subject, out StableId key)
        {
            key = !stopped && subject == lastSubject ? hint : default;
            return key.IsValid;
        }

        // Use this only for a deliberate press the presenter rejects before invoking IInteractable.Request.
        public void RecordBlockedInteraction(StableId target, ResultCode reason, StableId messageKey)
        {
            if (!messageKey.IsValid) return;
            if (reason == ResultCode.PrerequisiteMissing || reason == ResultCode.InsufficientResources ||
                reason == ResultCode.EvidenceInsufficient || reason == ResultCode.Unavailable ||
                reason == ResultCode.InvalidRequest)
                Record(PacingEventKind.BlockedInteraction, target, new CommandResult(reason, messageKey));
        }

        internal void Advance(decimal seconds, bool modalReading, bool waitingForOperation)
        {
            elapsed += seconds;
            if (chapter >= active.Length) return;
            var bucket = modalReading ? reading : waitingForOperation ? waiting : active;
            bucket[chapter] += seconds;
        }

        internal void SetChapter(IQuestService quests)
        {
            if (quests == null) return;
            while (chapter < active.Length && quests.Snapshot[StationQuests.RequiredStages[chapter]] == QuestStatus.Completed)
                chapter++;
        }

        internal void Interaction(StableId target, CommandResult result)
        {
            if (result.Succeeded)
            {
                if (target == lastSubject) ResetHint();
                return;
            }
            Record(PacingEventKind.BlockedInteraction, target, result);
        }

        internal void Power(CommandResult result, IEnumerable<StableId> loads) =>
            Record(PacingEventKind.PowerDecision, PowerDecision, result, loads == null ? null :
                string.Join(",", loads.Select(id => id.value).OrderBy(id => id, StringComparer.Ordinal)));

        internal void Food(CommandResult result, FoodSchedule choice) =>
            Record(PacingEventKind.FoodDecision, FoodDecision, result, choice.ToString());

        internal void Report(CommandResult result)
        {
            if (stopped) return;
            if (reportSubmitted && result.Code != ResultCode.AlreadyApplied && ReportRetries < int.MaxValue)
                ReportRetries++;
            reportSubmitted = true;
            Record(PacingEventKind.ReportSubmission, StationResearch.ReportId, result);
        }

        internal void Stop()
        {
            stopped = true;
            ResetHint();
        }

        private void Record(PacingEventKind kind, StableId subject, CommandResult result, string selection = null)
        {
            if (stopped || !subject.IsValid || subject.value.Length > 80 || !result.MessageKey.IsValid ||
                result.MessageKey.value.Length > 80) return;
            if (events.Count == EventCapacity) events.Dequeue();
            events.Enqueue(new PacingEvent(kind, subject, result, elapsed, selection));
            if (result.Succeeded)
            {
                if (subject == lastSubject) ResetHint();
                return;
            }
            if (subject != lastSubject || result.MessageKey != lastReason)
            {
                lastSubject = subject;
                lastReason = result.MessageKey;
                consecutiveFailures = 0;
                hint = default;
            }
            if (consecutiveFailures < HintThreshold && ++consecutiveFailures == HintThreshold)
                hint = HintFor(subject, result);
        }

        private void ResetHint()
        {
            lastSubject = default;
            lastReason = default;
            hint = default;
            consecutiveFailures = 0;
        }

        private static StableId HintFor(StableId subject, CommandResult result)
        {
            string key = result.MessageKey.value switch
            {
                "report.result.life-unsupported" => "report-limit",
                "report.result.valid-blank" or "report.result.cite-blank" => "blank",
                "report.result.cite-reviewed-samples" => "review",
                "report.result.complete-sections" => "report-sections",
                "echo.result.read-archive" => "archive",
                "echo.result.missing-clues" => "echo-clues",
                "echo.result.timing-before-decision" or "echo.result.decision-before-outage" => "echo-clues",
                "sample.result.record-context" or "sample.result.collect-first" => "samples",
                "sample.result.run-blank" => "blank",
                "sample.result.power-lab" => "lab-power",
                _ when result.Code == ResultCode.InsufficientResources => "power",
                _ when result.Code == ResultCode.PrerequisiteMissing => "previous-step",
                _ when result.Code == ResultCode.EvidenceInsufficient => subject.value switch
                {
                    "quest.echo-evidence" => "echo-clues",
                    "quest.sampling" => "samples",
                    "quest.analysis" => "blank",
                    "quest.report" => "report-limit",
                    _ => null
                },
                _ => null
            };
            return key == null ? default : new StableId("hint.pacing." + key);
        }
    }
}
