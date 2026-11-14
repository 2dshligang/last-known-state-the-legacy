using System;
using System.Collections.Generic;

namespace LKS
{
    public enum EvidenceKind { Unknown, Observation, EngineeringModel, Fiction }

    public readonly struct ScienceRecord
    {
        public StableId Id { get; }
        public EvidenceKind Kind { get; }
        public Uri Source { get; }
        public DateTimeOffset? RecordedAt { get; }

        public ScienceRecord(StableId id, EvidenceKind kind, Uri source, DateTimeOffset? recordedAt)
        {
            Id = id;
            Kind = kind;
            Source = source;
            RecordedAt = recordedAt;
        }
    }

    public interface IScienceCatalog
    {
        bool TryGet(StableId id, out ScienceRecord record);
    }

    public enum EvidenceQuality { Unknown, Missing, Estimated, Recorded }

    public readonly struct EchoRecord
    {
        public StableId Id { get; }
        public DateTimeOffset? RecordedAt { get; }
        public EvidenceKind Kind { get; }
        public EvidenceQuality Quality { get; }

        public EchoRecord(StableId id, DateTimeOffset? recordedAt, EvidenceKind kind, EvidenceQuality quality)
        {
            Id = id;
            RecordedAt = recordedAt;
            Kind = kind;
            Quality = quality;
        }
    }

    public interface IEchoService : IStateSource<IReadOnlyList<EchoRecord>>
    {
        CommandResult SubmitSequence(IReadOnlyList<StableId> evidenceInCausalOrder);
    }

    public enum SampleStatus { Unknown, Collected, AnalysisReady, Analyzed }
    public enum ControlStatus { Unknown, Pending, Valid, Invalid }

    public interface ISampleService : IStateSource<IReadOnlyDictionary<StableId, SampleStatus>>
    {
        ControlStatus BlankControl { get; }
        CommandResult Collect(StableId specimen);
        CommandResult Analyze(StableId specimen);
    }

    public enum ReportStatus { Unknown, Incomplete, Accepted }

    public interface IResearchService : IStateSource<ReportStatus>
    {
        CommandResult Submit(StableId interpretation, IReadOnlyList<StableId> evidence);
    }
}
