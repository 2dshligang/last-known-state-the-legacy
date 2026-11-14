using System;
using System.Collections.Generic;
using System.Linq;

namespace LKS
{
    public enum ReportObservation { LayersVisible, IronMineralSigns, BlankPassed, BlankShowedBackground }
    public enum ReportInterpretation { Unspecified, SedimentMayHaveSettled, FormationRemainsUncertain, LifeConfirmed }
    public enum ReportAlternative { Unspecified, WindDeposition, LaterMineralChange, NoAlternatives }
    public enum ReportUncertainty { Unspecified, OriginUncertain, MoreEvidenceNeededForLife, NoUncertainty }
    public enum ReportFollowUp { Unspecified, CompareNearbyLayersWithBlank, CheckMineralsWithFreshBlank, None }

    public sealed class ResearchDraft
    {
        public IReadOnlyList<ReportObservation> Observations { get; }
        public ReportInterpretation Interpretation { get; }
        public ReportAlternative Alternative { get; }
        public ReportUncertainty Uncertainty { get; }
        public ReportFollowUp FollowUp { get; }

        public ResearchDraft(IEnumerable<ReportObservation> observations, ReportInterpretation interpretation,
            ReportAlternative alternative, ReportUncertainty uncertainty, ReportFollowUp followUp)
        {
            Observations = Array.AsReadOnly(observations.ToArray());
            Interpretation = interpretation;
            Alternative = alternative;
            Uncertainty = uncertainty;
            FollowUp = followUp;
        }
    }

    public sealed class ReportEvidence
    {
        public SampleReading StationResult { get; }
        public StableId ReferenceContextKey => StationResult.Definition.ContextKey;
        public Uri ReferenceSource => StationResult.Definition.Reference;
        public EvidenceKind StationKind => StationResult.Kind;

        internal ReportEvidence(SampleReading result) => StationResult = result;
    }

    public sealed class StationResearch : IResearchService, IDisposable
    {
        public static readonly StableId ReportId = new StableId("research.dmitry-report");
        public static readonly StableId CompletedCause = new StableId("cause.research-complete");
        public const int CheckpointVersion = 1;

        private readonly StationSamples samples;
        private readonly EchoInvestigation investigation;
        private readonly Func<long> nextSequence;
        private readonly Action<Action> enqueue;
        private readonly List<Subscription> observers = new List<Subscription>();
        private ResearchDraft accepted;
        private bool stopped;

        public ReportStatus Snapshot => accepted == null ? ReportStatus.Incomplete : ReportStatus.Accepted;

        internal StationResearch(StationSamples samples, EchoInvestigation investigation,
            Func<long> nextSequence, Action<Action> enqueue, ResearchDraft checkpoint)
        {
            this.samples = samples;
            this.investigation = investigation;
            this.nextSequence = nextSequence;
            this.enqueue = enqueue;
            if (checkpoint != null && !Evaluate(checkpoint).Succeeded)
                throw new ArgumentException("Restore supporting evidence with the accepted research report.", nameof(checkpoint));
            accepted = checkpoint;
        }

        public ResearchDraft CaptureAccepted() => accepted;

        public static StableId TextKey(ReportObservation choice) => ChoiceKey("observation", choice);
        public static StableId TextKey(ReportInterpretation choice) => ChoiceKey("interpretation", choice);
        public static StableId TextKey(ReportAlternative choice) => ChoiceKey("alternative", choice);
        public static StableId TextKey(ReportUncertainty choice) => ChoiceKey("uncertainty", choice);
        public static StableId TextKey(ReportFollowUp choice) => ChoiceKey("follow-up", choice);

        private static StableId ChoiceKey<T>(string section, T choice) where T : Enum =>
            new StableId("report.choice." + section + "." + choice.ToString().ToLowerInvariant());

        public IReadOnlyList<ReportEvidence> ReadEvidence() => Array.AsReadOnly(
            samples.Readings.Values.Where(reading => reading.Phase >= SamplePhase.EvidenceAvailable)
                .Select(reading => new ReportEvidence(reading)).ToArray());

        public static FictionalSample FeedbackReference(StableId messageKey)
        {
            string sampleId = messageKey.ToString() switch
            {
                "report.result.life-unsupported" or "report.result.interpretation-needed" => "sample.layered-mudstone",
                "report.result.alternative-needed" => "sample.iron-rich-rock",
                "report.result.valid-blank" or "report.result.cite-blank" or
                    "report.result.contradictory-blank" => "sample.procedural-blank",
                _ => null
            };
            return sampleId == null ? null : FictionalSample.All.Single(sample => sample.Id.value == sampleId);
        }

        public CommandResult Submit(ResearchDraft draft)
        {
            var result = Evaluate(draft);
            if (!result.Succeeded)
                return result;
            if (accepted != null)
                return Result(ResultCode.AlreadyApplied, "already-accepted");

            accepted = draft;
            var change = new StateChange<ReportStatus>(nextSequence(), ReportId, CompletedCause, Snapshot);
            foreach (var observer in observers.ToArray())
                enqueue(() => observer.Notify(change));
            return result;
        }

        // The S01 submission shape cannot carry alternative, uncertainty or follow-up.
        CommandResult IResearchService.Submit(StableId interpretation, IReadOnlyList<StableId> evidence) =>
            stopped ? Result(ResultCode.Unavailable, "unavailable") : Result(ResultCode.InvalidRequest, "complete-sections");

        public CommandResult Evaluate(ResearchDraft draft)
        {
            if (stopped)
                return Result(ResultCode.Unavailable, "unavailable");
            if (draft == null)
                return Result(ResultCode.InvalidRequest, "complete-sections");
            if (draft.Interpretation == ReportInterpretation.LifeConfirmed)
                return Result(ResultCode.EvidenceInsufficient, "life-unsupported");
            if (investigation.Conclusion != EchoConclusion.Confirmed)
                return Result(ResultCode.PrerequisiteMissing, "confirm-echo");
            if (samples.BlankControl != ControlStatus.Valid)
                return Result(ResultCode.EvidenceInsufficient, "valid-blank");
            if (draft.Observations.Contains(ReportObservation.BlankShowedBackground))
                return Result(ResultCode.InvalidRequest, "contradictory-blank");
            if (!draft.Observations.Contains(ReportObservation.BlankPassed))
                return Result(ResultCode.EvidenceInsufficient, "cite-blank");
            if (!samples.AllReviewed || !draft.Observations.Contains(ReportObservation.LayersVisible) ||
                !draft.Observations.Contains(ReportObservation.IronMineralSigns))
                return Result(ResultCode.EvidenceInsufficient, "cite-reviewed-samples");
            if (draft.Observations.Count != 3)
                return Result(ResultCode.InvalidRequest, "duplicate-observation");
            if (draft.Interpretation != ReportInterpretation.SedimentMayHaveSettled &&
                draft.Interpretation != ReportInterpretation.FormationRemainsUncertain)
                return Result(ResultCode.InvalidRequest, "interpretation-needed");
            if (draft.Alternative != ReportAlternative.WindDeposition &&
                draft.Alternative != ReportAlternative.LaterMineralChange)
                return Result(ResultCode.InvalidRequest, "alternative-needed");
            if (draft.Uncertainty != ReportUncertainty.OriginUncertain &&
                draft.Uncertainty != ReportUncertainty.MoreEvidenceNeededForLife)
                return Result(ResultCode.EvidenceInsufficient, "state-uncertainty");
            if (draft.FollowUp != ReportFollowUp.CompareNearbyLayersWithBlank &&
                draft.FollowUp != ReportFollowUp.CheckMineralsWithFreshBlank)
                return Result(ResultCode.InvalidRequest, "follow-up-needed");

            return Result(ResultCode.Applied, "accepted");
        }

        private static CommandResult Result(ResultCode code, string key) =>
            new CommandResult(code, new StableId("report.result." + key));

        public IDisposable Subscribe(Action<StateChange<ReportStatus>> observer)
        {
            if (stopped)
                throw new ObjectDisposedException(nameof(StationResearch));
            var subscription = new Subscription(observers, observer ?? throw new ArgumentNullException(nameof(observer)));
            observers.Add(subscription);
            return subscription;
        }

        public void Dispose()
        {
            stopped = true;
            foreach (var observer in observers.ToArray())
                observer.Dispose();
        }

        private sealed class Subscription : IDisposable
        {
            private readonly List<Subscription> owner;
            private Action<StateChange<ReportStatus>> observer;

            public Subscription(List<Subscription> owner, Action<StateChange<ReportStatus>> observer)
            {
                this.owner = owner;
                this.observer = observer;
            }

            public void Notify(StateChange<ReportStatus> change) => observer?.Invoke(change);

            public void Dispose()
            {
                owner.Remove(this);
                observer = null;
            }
        }
    }
}
