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
        private readonly Queue<(StateChange<double> Change, Subscription[] Listeners)> pending =
            new Queue<(StateChange<double>, Subscription[])>();
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
            double elapsed = clock.Seconds - startedAt;
            if (double.IsNaN(elapsed) || double.IsInfinity(elapsed) || elapsed < Snapshot)
                throw new InvalidOperationException("Session clock must be finite and monotonic.");
            if (elapsed == Snapshot)
                return;

            Snapshot = elapsed;
            var change = new StateChange<double>(++sequence, SessionId, TickId, elapsed);
            if (subscriptions.Count > 0)
                pending.Enqueue((change, subscriptions.ToArray()));
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
                    var notification = pending.Dequeue();
                    foreach (var listener in notification.Listeners)
                    {
                        try
                        {
                            listener.Notify(notification.Change);
                        }
                        catch (Exception exception)
                        {
                            failures ??= new List<Exception>();
                            failures.Add(exception);
                        }
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
