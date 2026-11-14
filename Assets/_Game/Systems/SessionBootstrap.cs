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
            ISessionClock clock = null)
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
