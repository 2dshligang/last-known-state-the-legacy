using System;
using System.Collections.Generic;
using System.Linq;

namespace LKS
{
    public sealed class EchoFrame
    {
        public EchoRecord Evidence { get; }
        public int Sequence { get; }
        public int ReplaySecond { get; }
        public StableId CaptionKey { get; }
        public StableId VisualCue { get; }

        internal EchoFrame(string id, int sequence, int replaySecond, DateTimeOffset? recordedAt,
            EvidenceKind kind, EvidenceQuality quality, string cue)
        {
            Evidence = new EchoRecord(new StableId(id), recordedAt, kind, quality);
            Sequence = sequence;
            ReplaySecond = replaySecond;
            CaptionKey = new StableId(id + ".caption");
            VisualCue = new StableId(cue);
        }
    }

    public readonly struct EchoPlayback
    {
        public decimal Seconds { get; }
        public bool Playing { get; }
        public EchoFrame Frame { get; }

        internal EchoPlayback(decimal seconds, bool playing, EchoFrame frame)
        {
            Seconds = seconds;
            Playing = playing;
            Frame = frame;
        }
    }

    public sealed class StationEcho : IStateSource<EchoPlayback>, IDisposable
    {
        private static readonly StableId TimelineId = new StableId("echo.asterion");
        private static readonly StableId PlaybackCause = new StableId("cause.echo-playback");
        private static readonly DateTimeOffset IncidentTime =
            new DateTimeOffset(2059, 8, 18, 14, 20, 0, TimeSpan.Zero);

        public const int DurationSeconds = 20;
        public static IReadOnlyList<EchoFrame> Timeline { get; } = Array.AsReadOnly(new[]
        {
            new EchoFrame("echo.stale-input", 1, 0, IncidentTime.AddMinutes(-4),
                EvidenceKind.Fiction, EvidenceQuality.Recorded, "cue.echo-console"),
            new EchoFrame("echo.controller-decision", 2, 4, IncidentTime,
                EvidenceKind.Fiction, EvidenceQuality.Recorded, "cue.echo-controller"),
            new EchoFrame("echo.backup-estimate", 3, 4, IncidentTime,
                EvidenceKind.EngineeringModel, EvidenceQuality.Estimated, "cue.echo-backup"),
            new EchoFrame("echo.missing-interval", 4, 8, null,
                EvidenceKind.Fiction, EvidenceQuality.Missing, "cue.echo-gap"),
            new EchoFrame("echo.backup-exhausted", 5, 14, IncidentTime.AddMinutes(1),
                EvidenceKind.Fiction, EvidenceQuality.Recorded, "cue.echo-outage")
        });

        private readonly Func<long> nextSequence;
        private readonly Action<Action> enqueue;
        private readonly List<Subscription> observers = new List<Subscription>();
        private bool notificationPending;
        private bool disposed;

        public EchoPlayback Snapshot { get; private set; } = new EchoPlayback(0m, false, Timeline[0]);

        internal StationEcho(Func<long> nextSequence, Action<Action> enqueue)
        {
            this.nextSequence = nextSequence;
            this.enqueue = enqueue;
        }

        public void Restart() => Update(0m, true);
        public void Stop() => Update(0m, false);
        public void Seek(decimal seconds) => Update(Math.Max(0m, Math.Min(DurationSeconds, seconds)), Snapshot.Playing);

        internal void Advance(decimal seconds)
        {
            if (Snapshot.Playing)
                Seek(Snapshot.Seconds + seconds);
        }

        private void Update(decimal seconds, bool playing)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(StationEcho));
            playing &= seconds < DurationSeconds;
            if (seconds == Snapshot.Seconds && playing == Snapshot.Playing)
                return;

            Snapshot = new EchoPlayback(seconds, playing, Timeline.Last(frame => frame.ReplaySecond <= seconds));
            // Keep at most one pending delivery. A burst of seeks publishes its final snapshot.
            if (notificationPending || observers.Count == 0)
                return;

            notificationPending = true;
            var listeners = observers.ToArray();
            enqueue(() =>
            {
                notificationPending = false;
                if (disposed)
                    return;

                var change = new StateChange<EchoPlayback>(nextSequence(), TimelineId, PlaybackCause, Snapshot);
                List<Exception> failures = null;
                foreach (var listener in listeners)
                {
                    try { listener.Notify(change); }
                    catch (Exception error) { (failures ??= new List<Exception>()).Add(error); }
                }

                if (failures != null)
                    throw new AggregateException(failures);
            });
        }

        public IDisposable Subscribe(Action<StateChange<EchoPlayback>> observer)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(StationEcho));
            var subscription = new Subscription(observers, observer ?? throw new ArgumentNullException(nameof(observer)));
            observers.Add(subscription);
            return subscription;
        }

        public void Dispose()
        {
            disposed = true;
            foreach (var subscription in observers.ToArray())
                subscription.Dispose();
        }

        private sealed class Subscription : IDisposable
        {
            private readonly List<Subscription> owner;
            private Action<StateChange<EchoPlayback>> observer;

            public Subscription(List<Subscription> owner, Action<StateChange<EchoPlayback>> observer)
            {
                this.owner = owner;
                this.observer = observer;
            }

            public void Notify(StateChange<EchoPlayback> change) => observer?.Invoke(change);

            public void Dispose()
            {
                owner.Remove(this);
                observer = null;
            }
        }
    }
}
