using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace LKS
{
    [Serializable]
    public sealed class ArchivedSepEvent
    {
        [SerializeField] private string id, observedAt, recordUrl, units;
        [SerializeField] private string[] instruments;
        [SerializeField] private EvidenceKind kind;

        public string OriginalId => id;
        public string Units => units;
        public double? Dose => null;
        public IReadOnlyList<string> Instruments => Array.AsReadOnly(instruments);
        public ScienceRecord Record => new ScienceRecord(
            new StableId("donki." + id.ToLowerInvariant().Replace(':', '-')), kind,
            new Uri(recordUrl), DateTimeOffset.Parse(observedAt, CultureInfo.InvariantCulture));

        public ArchivedSepEvent(string id, string observedAt, string recordUrl, string units,
            string[] instruments, EvidenceKind kind)
        {
            this.id = id;
            this.observedAt = observedAt;
            this.recordUrl = recordUrl;
            this.units = units;
            this.instruments = (string[])instruments.Clone();
            this.kind = kind;
        }
    }

    public sealed class StationScienceCatalog : ScriptableObject, IScienceCatalog
    {
        public const int SchemaVersion = 1;
        public static readonly StableId DonkiSource = new StableId("nasa-donki-sep-2017");
        [SerializeField] private string attribution, license, transformVersion, rawSha256, normalizedSha256, transformSha256;
        [SerializeField] private ArchivedSepEvent[] events = Array.Empty<ArchivedSepEvent>();

        public string Attribution => attribution;
        public string License => license;
        public string TransformVersion => transformVersion;
        public string RawSha256 => rawSha256;
        public string NormalizedSha256 => normalizedSha256;
        public string TransformSha256 => transformSha256;
        public IReadOnlyList<ArchivedSepEvent> Events => Array.AsReadOnly(events);

        public bool TryGet(StableId id, out ScienceRecord record)
        {
            foreach (var entry in events)
            {
                var candidate = entry.Record;
                if (candidate.Id == id)
                {
                    record = candidate;
                    return true;
                }
            }
            record = default;
            return false;
        }

#if UNITY_EDITOR
        public void ReplaceSnapshot(string attribution, string license, string transformVersion,
            string rawSha256, string normalizedSha256, string transformSha256, ArchivedSepEvent[] records)
        {
            this.attribution = attribution;
            this.license = license;
            this.transformVersion = transformVersion;
            this.rawSha256 = rawSha256;
            this.normalizedSha256 = normalizedSha256;
            this.transformSha256 = transformSha256;
            events = (ArchivedSepEvent[])records.Clone();
        }
#endif
    }

    public sealed class FictionalSample
    {
        public StableId Id { get; }
        public StableId ContextKey { get; }
        public Uri Reference { get; }
        public EvidenceKind Kind => EvidenceKind.Fiction;

        private FictionalSample(string id, string reference)
        {
            Id = new StableId(id);
            ContextKey = new StableId(id + ".context");
            Reference = new Uri(reference);
        }

        public static IReadOnlyList<FictionalSample> All { get; } = Array.AsReadOnly(new[]
        {
            new FictionalSample("sample.layered-mudstone", "https://ntrs.nasa.gov/citations/20210013268"),
            new FictionalSample("sample.iron-rich-rock", "https://ntrs.nasa.gov/citations/20205001823"),
            new FictionalSample("sample.procedural-blank", "https://ntrs.nasa.gov/citations/20130011130")
        });
    }
}
