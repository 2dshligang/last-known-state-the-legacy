using System;
using System.Collections.Generic;
using UnityEngine;

namespace LKS
{
    [DisallowMultipleComponent]
    public sealed class SessionBootstrap : MonoBehaviour
    {
        public GameplaySession Current { get; private set; }

        public IReadOnlyList<string> Restart(IScienceCatalog catalog,
            IEnumerable<StableId> requiredRecords, Func<IEnumerable<IInteractable>> createTargets,
            ISessionClock clock = null) =>
            RestartCore(catalog, requiredRecords, createTargets, clock, null);

        public IReadOnlyList<string> RestartWithQuests(IScienceCatalog catalog,
            IEnumerable<StableId> requiredRecords, Func<IEnumerable<IInteractable>> createTargets,
            Func<StableId, ResultCode> readiness, Func<CommandResult> recovery,
            IEnumerable<StableId> checkpoint = null, ISessionClock clock = null)
        {
            if (readiness == null || recovery == null)
                return Array.AsReadOnly(new[] { "Provide quest readiness and power recovery services." });

            return RestartCore(catalog, requiredRecords, createTargets, clock,
                session => session.AttachQuests(readiness, recovery, checkpoint));
        }

        public IReadOnlyList<string> RestartWithPower(IScienceCatalog catalog,
            IEnumerable<StableId> requiredRecords, Func<IEnumerable<IInteractable>> createTargets,
            Func<StableId, ResultCode> readiness, PowerCheckpoint powerCheckpoint = null,
            IEnumerable<StableId> questCheckpoint = null, ISessionClock clock = null,
            LifeSupportCheckpoint lifeSupportCheckpoint = null,
            LogisticsCheckpoint logisticsCheckpoint = null, IEnumerable<StableId> archiveCheckpoint = null,
            EchoInvestigationCheckpoint investigationCheckpoint = null, SampleCheckpoint sampleCheckpoint = null,
            ResearchDraft researchCheckpoint = null, OpeningProgress openingCheckpoint = OpeningProgress.Farewell)
        {
            if (readiness == null)
                return Array.AsReadOnly(new[] { "Provide quest readiness services." });

            return RestartCore(catalog, requiredRecords, createTargets, clock, session =>
            {
                session.AttachPower(powerCheckpoint);
                session.AttachLifeSupport(lifeSupportCheckpoint, logisticsCheckpoint, sampleCheckpoint);
                session.AttachArchive(archiveCheckpoint, investigationCheckpoint);
                session.AttachResearch(researchCheckpoint, openingCheckpoint);
                var restoredQuests = questCheckpoint == null
                    ? null : new List<StableId>(questCheckpoint);
                if (restoredQuests != null && restoredQuests.Contains(StationQuests.RequiredStages[2]) &&
                    session.Investigation.Conclusion != EchoConclusion.Confirmed)
                    throw new ArgumentException("Restore confirmed ECHO evidence with completed ECHO progress.");
                if (restoredQuests != null &&
                    ((restoredQuests.Contains(StationQuests.RequiredStages[3]) && !session.Samples.AllCollected) ||
                     (restoredQuests.Contains(StationQuests.RequiredStages[4]) && !session.Samples.AllReviewed)))
                    throw new ArgumentException("Restore sample evidence with completed sample progress.");
                if (restoredQuests != null && restoredQuests.Contains(StationQuests.RequiredStages[5]) &&
                    session.Research.Snapshot != ReportStatus.Accepted)
                    throw new ArgumentException("Restore an accepted report with completed report progress.");
                if (restoredQuests != null && restoredQuests.Contains(StationEnding.Photograph) &&
                    !session.Ending.HasTransmission)
                    throw new ArgumentException("Restore the report transmission with completed ending progress.");
                var power = session.Power;
                session.AttachQuests(stage =>
                {
                    if (stage == StationQuests.RecoveryTarget)
                        return ResultCode.Applied;
                    if (stage == StationQuests.RequiredStages[1])
                        return session.LifeSupport.Snapshot.Air == AirStatus.Safe ? ResultCode.Applied : ResultCode.InsufficientResources;
                    if (stage == StationQuests.RequiredStages[2] && session.Investigation.Conclusion != EchoConclusion.Confirmed)
                        return ResultCode.EvidenceInsufficient;
                    if (stage == StationQuests.RequiredStages[2])
                        return ResultCode.Applied;
                    if (stage == StationQuests.RequiredStages[3])
                        return session.Samples.AllCollected ? ResultCode.Applied : ResultCode.EvidenceInsufficient;
                    if (stage == StationQuests.RequiredStages[4])
                        return session.Samples.AllReviewed ? ResultCode.Applied : ResultCode.EvidenceInsufficient;
                    if (stage == StationQuests.RequiredStages[5])
                        return session.Research.Snapshot == ReportStatus.Accepted ? ResultCode.Applied : ResultCode.EvidenceInsufficient;
                    if (stage == StationEnding.Photograph)
                        return session.Ending.Readiness().Code;
                    return readiness(stage);
                }, power.Recover, restoredQuests);
            });
        }

        private IReadOnlyList<string> RestartCore(IScienceCatalog catalog,
            IEnumerable<StableId> requiredRecords, Func<IEnumerable<IInteractable>> createTargets,
            ISessionClock clock, Action<GameplaySession> configure)
        {
            var errors = new List<string>();
            if (!isActiveAndEnabled)
            {
                errors.Add("Enable the session bootstrap before starting the session.");
                return errors.AsReadOnly();
            }

            clock ??= new UnitySessionClock();
            double startedAt = clock.Seconds;
            if (double.IsNaN(startedAt) || double.IsInfinity(startedAt))
                errors.Add("The session clock must provide a finite time.");

            if (catalog == null)
                errors.Add("Assign a science catalog before starting the session.");
            if (requiredRecords == null)
                errors.Add("Provide the list of required science records.");
            else if (catalog != null)
            {
                foreach (var id in requiredRecords)
                {
                    if (!id.IsValid)
                        errors.Add("A required science record has an invalid ID.");
                    else if (!catalog.TryGet(id, out var record) || record.Id != id ||
                        record.Kind == EvidenceKind.Unknown)
                        errors.Add($"The science catalog is missing a usable record for '{id}'.");
                }
            }

            if (createTargets == null)
                errors.Add("Provide an interaction target factory before starting the session.");
            if (errors.Count != 0)
                return errors.AsReadOnly();

            var targets = new Dictionary<StableId, IInteractable>();
            var created = createTargets();
            if (created == null)
                errors.Add("The interaction target factory returned no collection.");
            else
            {
                foreach (var target in created)
                {
                    if (target == null || !target.Id.IsValid)
                        errors.Add("An interaction target is missing or has an invalid ID.");
                    else if (!targets.TryAdd(target.Id, target))
                        errors.Add($"Duplicate interaction target ID '{target.Id}'.");
                }
            }

            if (errors.Count == 0)
            {
                var next = new GameplaySession(clock, startedAt, catalog, targets);
                try
                {
                    configure?.Invoke(next);
                }
                catch (ArgumentException exception)
                {
                    next.Dispose();
                    errors.Add(exception.Message);
                    return errors.AsReadOnly();
                }

                StopSession();
                Current = next;
            }

            return errors.AsReadOnly();
        }

        public void StopSession()
        {
            Current?.Dispose();
            Current = null;
        }

        private void LateUpdate()
        {
            var session = Current;
            if (session == null)
                return;

            session.Tick();
            session.Dispatch();
        }

        private void OnDisable() => StopSession();

        private sealed class UnitySessionClock : ISessionClock
        {
            public double Seconds => Time.timeAsDouble;
        }
    }
}
