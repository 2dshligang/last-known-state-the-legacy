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
            LogisticsCheckpoint logisticsCheckpoint = null, IEnumerable<StableId> archiveCheckpoint = null)
        {
            if (readiness == null)
                return Array.AsReadOnly(new[] { "Provide quest readiness services." });

            return RestartCore(catalog, requiredRecords, createTargets, clock, session =>
            {
                session.AttachPower(powerCheckpoint);
                session.AttachLifeSupport(lifeSupportCheckpoint, logisticsCheckpoint);
                session.AttachArchive(archiveCheckpoint);
                var power = session.Power;
                session.AttachQuests(stage =>
                {
                    if (stage == StationQuests.RecoveryTarget)
                        return ResultCode.Applied;
                    if (stage == StationQuests.RequiredStages[1])
                        return session.LifeSupport.Snapshot.Air == AirStatus.Safe ? ResultCode.Applied : ResultCode.InsufficientResources;
                    if (stage == StationQuests.RequiredStages[2] && !session.Archive.RequiredEntryAcknowledged)
                        return ResultCode.EvidenceInsufficient;
                    return readiness(stage);
                }, power.Recover, questCheckpoint);
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
