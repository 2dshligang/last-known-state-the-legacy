using System;
using System.Collections.Generic;

namespace LKS
{
    public interface ISessionClock
    {
        double Seconds { get; }
    }

    // Main-thread owner of a single run and its pending notifications.
    public sealed class GameplaySession : IDisposable, IStateSource<double>
    {
        private static readonly StableId SessionId = new StableId("session.run");
        private static readonly StableId TickId = new StableId("cause.clock-advanced");
        private readonly ISessionClock clock;
        private readonly double startedAt;
        private readonly Dictionary<StableId, IInteractable> targets;
        private readonly List<Subscription> subscriptions = new List<Subscription>();
        private readonly Queue<Action> pending =
            new Queue<Action>();
        private long sequence;
        private bool dispatching;
        private bool disposed;

        internal GameplaySession(ISessionClock clock, double startedAt, IScienceCatalog catalog,
            Dictionary<StableId, IInteractable> targets)
        {
            this.clock = clock;
            this.startedAt = startedAt;
            Science = catalog;
            this.targets = targets;
        }

        public IScienceCatalog Science { get; }
        public double Snapshot { get; private set; }
        private StationQuests quests;
        private bool powerReadingPaused;
        private decimal powerClockPosition;
        public StationPower Power { get; private set; }

        internal void AttachPower(PowerCheckpoint checkpoint)
        {
            Power = new StationPower(() => ++sequence, pending.Enqueue, checkpoint);
        }

        public void SetPowerReadingPaused(bool paused)
        {
            EnsureActive();
            if (powerReadingPaused == paused)
                return;
            Tick();
            powerReadingPaused = paused;
        }
        public IQuestService Quests => quests;

        public IReadOnlyList<StableId> CaptureQuestCheckpoint()
        {
            EnsureActive();
            if (quests == null)
                throw new InvalidOperationException("The session has no station quest graph.");
            return quests.CaptureCheckpoint();
        }

        internal void AttachQuests(Func<StableId, ResultCode> readiness, Func<CommandResult> recovery,
            IEnumerable<StableId> checkpoint)
        {
            quests = new StationQuests(readiness, recovery, checkpoint, () => ++sequence, pending.Enqueue);
            foreach (var target in quests.CreateTargets())
                targets.Add(target.Id, target);
        }

        public bool TryGetTarget(StableId id, out IInteractable target)
        {
            EnsureActive();
            return targets.TryGetValue(id, out target);
        }

        public IDisposable Subscribe(Action<StateChange<double>> observer)
        {
            EnsureActive();
            if (observer == null)
                throw new ArgumentNullException(nameof(observer));
            var subscription = new Subscription(this, observer);
            subscriptions.Add(subscription);
            return subscription;
        }

        public void Tick()
        {
            EnsureActive();
            double now = clock.Seconds;
            double elapsed = now - startedAt;
            if (double.IsNaN(elapsed) || double.IsInfinity(elapsed) || elapsed < Snapshot)
                throw new InvalidOperationException("Session clock must be finite and monotonic.");
            if (elapsed == Snapshot)
                return;

            if (Power != null)
            {
                decimal position = ((decimal)now - (decimal)startedAt) * StationPower.SimulationSecondsPerGameSecond;
                if (!powerReadingPaused)
                    Power.Advance(position - powerClockPosition);
                powerClockPosition = position;
            }

            Snapshot = elapsed;
            var change = new StateChange<double>(++sequence, SessionId, TickId, elapsed);
            foreach (var listener in subscriptions.ToArray())
                pending.Enqueue(() => listener.Notify(change));
        }

        public void Dispatch()
        {
            EnsureActive();
            if (dispatching)
                return;

            dispatching = true;
            List<Exception> failures = null;
            try
            {
                int count = pending.Count;
                for (int index = 0; index < count && !disposed; index++)
                {
                    try
                    {
                        pending.Dequeue().Invoke();
                    }
                    catch (Exception exception)
                    {
                        failures ??= new List<Exception>();
                        failures.Add(exception);
                    }
                }
            }
            finally
            {
                dispatching = false;
            }

            if (failures != null)
                throw new AggregateException("One or more session observers failed.", failures);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            pending.Clear();
            targets.Clear();
            quests?.Dispose();
            Power?.Dispose();
            foreach (var subscription in subscriptions.ToArray())
                subscription.Dispose();
        }

        private void EnsureActive()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(GameplaySession));
        }

        private sealed class Subscription : IDisposable
        {
            private GameplaySession owner;
            private Action<StateChange<double>> observer;

            public Subscription(GameplaySession owner, Action<StateChange<double>> observer)
            {
                this.owner = owner;
                this.observer = observer;
            }

            public void Notify(StateChange<double> change)
            {
                observer?.Invoke(change);
            }

            public void Dispose()
            {
                owner?.subscriptions.Remove(this);
                owner = null;
                observer = null;
            }
        }
    }
}
