using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace LKS
{
    public sealed class StationSaves
    {
        public const int SchemaVersion = 2;
        private const string ContentVersion = "asterion-story-v1";
        private readonly AtomicSaveStore store;
        private readonly StationScienceCatalog science;
        private readonly string contentHash;
        private readonly StableId[] requiredRecords;

        public StationSaves(string folder, StationScienceCatalog science, IEnumerable<StableId> requiredRecords)
        {
            this.science = science != null ? science : throw new ArgumentNullException(nameof(science));
            this.requiredRecords = requiredRecords?.ToArray() ?? throw new ArgumentNullException(nameof(requiredRecords));
            StableId.ValidateUnique(this.requiredRecords);
            foreach (var id in this.requiredRecords)
            {
                if (!science.TryGet(id, out var record) || record.Id != id || record.Kind == EvidenceKind.Unknown)
                    throw new ArgumentException($"Missing required science record '{id}'.", nameof(requiredRecords));
            }
            if (new[] { science.RawSha256, science.NormalizedSha256, science.TransformSha256 }
                .Any(hash => hash == null || hash.Length != 64 || hash.Any(value => !Uri.IsHexDigit(value))))
                throw new ArgumentException("Supply the approved imported science snapshot.", nameof(science));

            using var sha = SHA256.Create();
            string input = string.Join("|", ContentVersion, science.RawSha256, science.NormalizedSha256,
                science.TransformSha256, string.Join(",", this.requiredRecords.Select(id => id.ToString())
                    .OrderBy(id => id, StringComparer.Ordinal)));
            contentHash = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(input)));
            store = new AtomicSaveStore(folder, contentHash, SchemaVersion);
        }

        public static StationSaves ForPlayer(StationScienceCatalog science, IEnumerable<StableId> requiredRecords,
            bool cleanProfile = false)
        {
            string profile = cleanProfile ? "test-" + Guid.NewGuid().ToString("N") : "player";
            return new StationSaves(System.IO.Path.Combine(Application.persistentDataPath, "Saves", profile),
                science, requiredRecords);
        }

        public CommandResult Save(StableId slot, GameplaySession session)
        {
            if (session == null || session.IsDisposed || session.Science != science ||
                session.Ending == null || session.Quests == null)
                return new CommandResult(ResultCode.InvalidRequest, new StableId("save.result.invalid"));
            string json = JsonUtility.ToJson(Payload.Capture(session));
            return store.Write(slot, new SaveSnapshot(SchemaVersion, contentHash, Encoding.UTF8.GetBytes(json)));
        }

        public CommandResult Load(StableId slot, SessionBootstrap bootstrap,
            Func<IEnumerable<IInteractable>> createTargets, Func<StableId, ResultCode> readiness,
            ISessionClock clock = null)
        {
            if (bootstrap == null || !bootstrap.isActiveAndEnabled || createTargets == null || readiness == null)
                return new CommandResult(ResultCode.InvalidRequest, new StableId("save.result.invalid"));
            return store.ReadValidated(slot, snapshot =>
            {
                try
                {
                    var payload = JsonUtility.FromJson<Payload>(
                        new UTF8Encoding(false, true).GetString(snapshot.CopyPayload()));
                    if (payload == null)
                        return false;
                    return payload.Restore(snapshot.SchemaVersion, bootstrap, science,
                        requiredRecords, createTargets, readiness, clock);
                }
                catch (Exception error) when (error is ArgumentException || error is FormatException || error is OverflowException)
                {
                    return false;
                }
            }, out _);
        }

        [Serializable]
        private sealed class Payload
        {
            public string[] quests, archive, clues, acceptedOrder;
            public string opening;
            public PowerData power;
            public AirData air;
            public LogisticsData logistics;
            public SampleData samples;
            public ReportData report;

            public static Payload Capture(GameplaySession session)
            {
                var power = session.Power.Capture();
                var life = session.LifeSupport.Capture();
                var logistics = session.Logistics.Capture();
                var echo = session.Investigation.Capture();
                var samples = session.Samples.Capture();
                var report = session.Research.CaptureAccepted();
                return new Payload
                {
                    quests = SerializeIds(session.CaptureQuestCheckpoint()),
                    archive = SerializeIds(session.Archive.Capture()),
                    clues = SerializeIds(echo.Collected),
                    acceptedOrder = SerializeIds(echo.AcceptedOrder),
                    opening = session.Narrative.CaptureOpening().ToString(),
                    power = new PowerData
                    {
                        battery = Format(power.BatteryKwh), fraction = Format(power.PendingSeconds),
                        requested = SerializeIds(power.Requested), reserve = power.BatteryReserveMode
                    },
                    air = new AirData
                    {
                        reserve = Format(life.AirReserve), training = Format(life.TrainingSeconds),
                        actuator = Format(life.ActuatorSeconds), requested = life.ShelterRequested
                    },
                    logistics = new LogisticsData
                    {
                        schedule = logistics.Schedule, message = logistics.MessageId.value,
                        delivery = logistics.Delivery, remaining = logistics.RemainingSeconds
                    },
                    samples = new SampleData
                    {
                        phases = samples.Phases.Select(pair => new SampleEntry
                        {
                            id = pair.Key.value, phase = pair.Value
                        }).ToArray(),
                        control = samples.Control, pending = samples.PendingId.value,
                        remaining = samples.RemainingSeconds, clean = samples.ChamberClean
                    },
                    report = report == null ? null : new ReportData
                    {
                        observations = report.Observations.ToArray(), interpretation = report.Interpretation,
                        alternative = report.Alternative, uncertainty = report.Uncertainty, followUp = report.FollowUp
                    }
                };
            }

            public bool Restore(int version, SessionBootstrap bootstrap, IScienceCatalog science,
                StableId[] requiredRecords, Func<IEnumerable<IInteractable>> createTargets,
                Func<StableId, ResultCode> readiness, ISessionClock clock)
            {
                // Decimal strings preserve sub-step progress across locales and Unity JSON round trips.
                if (quests == null || clues == null || acceptedOrder == null || power?.requested == null ||
                    air == null || logistics == null || samples?.phases == null)
                    return false;
                var milestones = ParseIds(quests);
                OpeningProgress restoredOpening;
                if (version == 1 && string.IsNullOrEmpty(opening))
                    restoredOpening = milestones.Length == 0 ? OpeningProgress.Farewell : OpeningProgress.Complete;
                else if (!Enum.TryParse(opening, out restoredOpening) || opening != restoredOpening.ToString())
                    return false;

                var archived = ParseIds(archive ?? Array.Empty<string>());
                StableId.ValidateUnique(archived);
                var known = new HashSet<StableId>(EnglishContentCatalog.Archive.Select(entry => entry.Id));
                var phases = samples.phases.ToDictionary(entry =>
                    new StableId(entry?.id), entry => entry.phase);
                var errors = bootstrap.RestartWithPower(science, requiredRecords, createTargets, readiness,
                    powerCheckpoint: new PowerCheckpoint(Parse(power.battery), Parse(power.fraction),
                        ParseIds(power.requested), power.reserve),
                    questCheckpoint: milestones, clock: clock,
                    lifeSupportCheckpoint: new LifeSupportCheckpoint(Parse(air.reserve), Parse(air.training),
                        Parse(air.actuator), air.requested),
                    logisticsCheckpoint: new LogisticsCheckpoint(logistics.schedule, ParseOptional(logistics.message),
                        logistics.delivery, logistics.remaining),
                    archiveCheckpoint: archived.Where(known.Contains),
                    investigationCheckpoint: new EchoInvestigationCheckpoint(ParseIds(clues), ParseIds(acceptedOrder)),
                    sampleCheckpoint: new SampleCheckpoint(phases, samples.control, ParseOptional(samples.pending),
                        samples.remaining, samples.clean),
                    researchCheckpoint: !(report?.observations?.Length > 0) ? null : new ResearchDraft(report.observations,
                        report.interpretation, report.alternative, report.uncertainty, report.followUp),
                    openingCheckpoint: restoredOpening);
                return errors.Count == 0;
            }

            private static string[] SerializeIds(IEnumerable<StableId> ids) => ids.Select(id => id.value).ToArray();
            private static StableId[] ParseIds(string[] ids) => ids.Select(id => new StableId(id)).ToArray();
            private static StableId ParseOptional(string id) => string.IsNullOrEmpty(id) ? default : new StableId(id);
            private static string Format(decimal number) => number.ToString(CultureInfo.InvariantCulture);
            private static decimal Parse(string value) =>
                decimal.Parse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        }

        [Serializable] private sealed class PowerData { public string battery, fraction; public string[] requested; public bool reserve; }
        [Serializable] private sealed class AirData { public string reserve, training, actuator; public bool requested; }
        [Serializable] private sealed class LogisticsData
        {
            public FoodSchedule schedule;
            public string message;
            public DeliveryState delivery;
            public int remaining;
        }

        [Serializable] private sealed class SampleEntry { public string id; public SamplePhase phase; }
        [Serializable] private sealed class SampleData
        {
            public SampleEntry[] phases;
            public ControlStatus control;
            public string pending;
            public int remaining;
            public bool clean;
        }

        [Serializable] private sealed class ReportData
        {
            public ReportObservation[] observations;
            public ReportInterpretation interpretation;
            public ReportAlternative alternative;
            public ReportUncertainty uncertainty;
            public ReportFollowUp followUp;
        }
    }
}
