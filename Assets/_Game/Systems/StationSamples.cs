using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace LKS
{
    public enum SamplePhase { Unseen, ContextRecorded, Collected, AnalysisPending, EvidenceAvailable, Reviewed }

    public sealed class SampleReading
    {
        public FictionalSample Definition { get; }
        public SamplePhase Phase { get; }
        public ControlStatus Control { get; }
        public EvidenceKind Kind => EvidenceKind.Fiction;
        public StableId ObservationKey => new StableId(Definition.Id + ".observation");
        public StableId InterpretationKey => new StableId(
            Phase < SamplePhase.EvidenceAvailable ? "sample.interpretation.pending" :
            Control == ControlStatus.Valid ? Definition.Id + ".interpretation" : "sample.interpretation.limited");

        internal SampleReading(FictionalSample definition, SamplePhase phase, ControlStatus control)
        {
            Definition = definition;
            Phase = phase;
            Control = control;
        }
    }

    public sealed class SampleCheckpoint
    {
        public const int SchemaVersion = 1;
        public IReadOnlyDictionary<StableId, SamplePhase> Phases { get; }
        public ControlStatus Control { get; }
        public StableId PendingId { get; }
        public int RemainingSeconds { get; }
        public bool ChamberClean { get; }

        public SampleCheckpoint(IReadOnlyDictionary<StableId, SamplePhase> phases, ControlStatus control,
            StableId pendingId, int remainingSeconds, bool chamberClean)
        {
            Phases = new ReadOnlyDictionary<StableId, SamplePhase>(phases.ToDictionary(pair => pair.Key, pair => pair.Value));
            Control = control;
            PendingId = pendingId;
            RemainingSeconds = remainingSeconds;
            ChamberClean = chamberClean;
        }
    }

    public sealed class StationSamples : ISampleService, IDisposable
    {
        public static readonly StableId Blank = new StableId("sample.procedural-blank");
        private static readonly StableId Subject = new StableId("samples.station");
        private static readonly StableId Cause = new StableId("cause.sample-changed");
        private const int RunSeconds = 5;

        private readonly StationPower power;
        private readonly Func<long> nextSequence;
        private readonly Action<Action> enqueue;
        private readonly Dictionary<StableId, SamplePhase> phases;
        private readonly List<Subscription> observers = new List<Subscription>();
        private bool chamberClean;
        private StableId pendingId;
        private int remaining;
        private bool disposed;

        public ControlStatus BlankControl { get; private set; }
        public IReadOnlyDictionary<StableId, SampleStatus> Snapshot { get; private set; }
        public IReadOnlyDictionary<StableId, SampleReading> Readings { get; private set; }
        public bool AllCollected => phases.Values.All(phase => phase >= SamplePhase.Collected);
        public bool AllReviewed => BlankControl == ControlStatus.Valid &&
            phases.Values.All(phase => phase == SamplePhase.Reviewed);

        internal StationSamples(StationPower power, Func<long> nextSequence,
            Action<Action> enqueue, SampleCheckpoint saved)
        {
            this.power = power;
            this.nextSequence = nextSequence;
            this.enqueue = enqueue;
            phases = FictionalSample.All.ToDictionary(sample => sample.Id, _ => SamplePhase.Unseen);
            if (saved != null)
                Restore(saved);
            RebuildSnapshots();
        }

        public SampleCheckpoint Capture() => new SampleCheckpoint(phases, BlankControl, pendingId, remaining, chamberClean);

        internal Action<StableId, CommandResult> Attempted { get; set; }

        private CommandResult Observe(StableId specimen, CommandResult result)
        {
            Attempted?.Invoke(specimen, result);
            return result;
        }

        public CommandResult RecordContext(StableId specimen) => Observe(specimen, Record(specimen));

        private CommandResult Record(StableId specimen)
        {
            if (!CanUse(specimen, out var phase, out var error))
                return error;
            if (phase >= SamplePhase.ContextRecorded)
                return Result(ResultCode.AlreadyApplied, "already-recorded");
            phases[specimen] = SamplePhase.ContextRecorded;
            return Changed("context-recorded");
        }

        public CommandResult Collect(StableId specimen) => Observe(specimen, PickUp(specimen));

        private CommandResult PickUp(StableId specimen)
        {
            if (!CanUse(specimen, out var phase, out var error))
                return error;
            if (phase >= SamplePhase.Collected)
                return Result(ResultCode.AlreadyApplied, "already-collected");
            if (phase != SamplePhase.ContextRecorded)
                return Result(ResultCode.PrerequisiteMissing, "record-context");
            phases[specimen] = SamplePhase.Collected;
            return Changed("collected");
        }

        public CommandResult Analyze(StableId specimen) => Observe(specimen, StartAnalysis(specimen));

        private CommandResult StartAnalysis(StableId specimen)
        {
            if (!CanUse(specimen, out var phase, out var error))
                return error;
            if (phase >= SamplePhase.AnalysisPending)
                return Result(ResultCode.AlreadyApplied, "already-started");
            if (phase != SamplePhase.Collected)
                return Result(ResultCode.PrerequisiteMissing, "collect-first");
            if (remaining != 0)
                return Result(ResultCode.Unavailable, "lab-busy");
            if (specimen != Blank && BlankControl != ControlStatus.Valid && BlankControl != ControlStatus.Invalid)
                return Result(ResultCode.PrerequisiteMissing, "run-blank");
            if (!LabPowered())
                return Result(ResultCode.InsufficientResources, "power-lab");

            phases[specimen] = SamplePhase.AnalysisPending;
            pendingId = specimen;
            remaining = RunSeconds;
            if (specimen == Blank)
                BlankControl = ControlStatus.Pending;
            return Changed("analysis-started");
        }

        public CommandResult Cancel()
        {
            if (disposed || remaining == 0)
                return Result(ResultCode.Unavailable, "no-active-run");
            phases[pendingId] = SamplePhase.Collected;
            if (pendingId == Blank)
                BlankControl = ControlStatus.Unknown;
            pendingId = default;
            remaining = 0;
            return Changed("cancelled");
        }

        public CommandResult CleanForRetry()
        {
            if (disposed || remaining != 0)
                return Result(ResultCode.Unavailable, "lab-busy");
            if (BlankControl != ControlStatus.Invalid)
                return Result(ResultCode.PrerequisiteMissing, "failed-blank-first");
            if (!LabPowered())
                return Result(ResultCode.InsufficientResources, "power-lab");

            chamberClean = true;
            BlankControl = ControlStatus.Unknown;
            foreach (var id in phases.Keys.ToArray())
            {
                if (phases[id] >= SamplePhase.EvidenceAvailable)
                    phases[id] = SamplePhase.Collected;
            }
            return Changed("cleaned");
        }

        public CommandResult Review(StableId specimen) => Observe(specimen, ReviewResult(specimen));

        private CommandResult ReviewResult(StableId specimen)
        {
            if (!CanUse(specimen, out var phase, out var error))
                return error;
            if (phase == SamplePhase.Reviewed)
                return Result(ResultCode.AlreadyApplied, "already-reviewed");
            if (phase != SamplePhase.EvidenceAvailable)
                return Result(ResultCode.PrerequisiteMissing, "await-evidence");
            phases[specimen] = SamplePhase.Reviewed;
            return Changed("reviewed");
        }

        internal void Advance(decimal seconds, bool supplied)
        {
            if (remaining == 0 || !supplied || seconds == 0)
                return;
            remaining -= (int)seconds;
            if (remaining != 0)
                return;
            phases[pendingId] = SamplePhase.EvidenceAvailable;
            if (pendingId == Blank)
                BlankControl = chamberClean ? ControlStatus.Valid : ControlStatus.Invalid;
            pendingId = default;
            Changed("evidence-ready");
        }

        private void Restore(SampleCheckpoint saved)
        {
            var pending = saved.Phases.Where(pair => pair.Value == SamplePhase.AnalysisPending).ToArray();
            bool validPending = saved.RemainingSeconds == 0
                ? saved.PendingId == default && pending.Length == 0
                : saved.RemainingSeconds > 0 && saved.RemainingSeconds <= RunSeconds &&
                    pending.Length == 1 && pending[0].Key == saved.PendingId;
            if (saved.Phases.Count != phases.Count || !validPending ||
                saved.Phases.Any(pair => !phases.ContainsKey(pair.Key) || !Enum.IsDefined(typeof(SamplePhase), pair.Value)) ||
                !Enum.IsDefined(typeof(ControlStatus), saved.Control))
                throw new ArgumentException("Invalid sample checkpoint.", nameof(saved));

            var blankPhase = saved.Phases[Blank];
            bool validBlank = saved.Control switch
            {
                ControlStatus.Unknown => blankPhase < SamplePhase.AnalysisPending,
                ControlStatus.Pending => blankPhase == SamplePhase.AnalysisPending,
                ControlStatus.Valid => blankPhase >= SamplePhase.EvidenceAvailable && saved.ChamberClean,
                ControlStatus.Invalid => blankPhase >= SamplePhase.EvidenceAvailable && !saved.ChamberClean,
                _ => false
            };
            if (!validBlank ||
                ((saved.Control == ControlStatus.Unknown || saved.Control == ControlStatus.Pending) &&
                 saved.Phases.Any(pair => pair.Key != Blank && pair.Value >= SamplePhase.AnalysisPending)))
                throw new ArgumentException("Sample checkpoint does not match its blank control.", nameof(saved));

            foreach (var pair in saved.Phases)
                phases[pair.Key] = pair.Value;
            BlankControl = saved.Control;
            chamberClean = saved.ChamberClean;
            pendingId = saved.PendingId;
            remaining = saved.RemainingSeconds;
        }

        private bool LabPowered() => power.Snapshot.Loads.Any(load => load.Id == StationPower.Lab && load.Supplied);

        private bool CanUse(StableId specimen, out SamplePhase phase, out CommandResult error)
        {
            phase = default;
            error = default;
            if (disposed)
                error = Result(ResultCode.Unavailable, "unavailable");
            else if (!phases.TryGetValue(specimen, out phase))
                error = Result(ResultCode.UnknownId, "unknown-sample");
            return error.Code == ResultCode.Uninitialized;
        }

        public IDisposable Subscribe(Action<StateChange<IReadOnlyDictionary<StableId, SampleStatus>>> observer)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(StationSamples));
            var subscription = new Subscription(observers, observer ?? throw new ArgumentNullException(nameof(observer)));
            observers.Add(subscription);
            return subscription;
        }

        private CommandResult Changed(string key)
        {
            RebuildSnapshots();
            var change = new StateChange<IReadOnlyDictionary<StableId, SampleStatus>>(
                nextSequence(), Subject, Cause, Snapshot);
            foreach (var observer in observers.ToArray())
                enqueue(() => observer.Notify(change));
            return Result(ResultCode.Applied, key);
        }

        private void RebuildSnapshots()
        {
            Snapshot = new ReadOnlyDictionary<StableId, SampleStatus>(phases.ToDictionary(pair => pair.Key,
                pair => pair.Value >= SamplePhase.EvidenceAvailable ? SampleStatus.Analyzed :
                    pair.Value == SamplePhase.AnalysisPending ? SampleStatus.AnalysisReady :
                    pair.Value == SamplePhase.Collected ? SampleStatus.Collected : SampleStatus.Unknown));
            Readings = new ReadOnlyDictionary<StableId, SampleReading>(FictionalSample.All.ToDictionary(
                sample => sample.Id, sample => new SampleReading(sample, phases[sample.Id], BlankControl)));
        }

        private static CommandResult Result(ResultCode code, string key) =>
            new CommandResult(code, new StableId("sample.result." + key));

        public void Dispose()
        {
            disposed = true;
            foreach (var observer in observers.ToArray())
                observer.Dispose();
        }

        private sealed class Subscription : IDisposable
        {
            private readonly List<Subscription> owner;
            private Action<StateChange<IReadOnlyDictionary<StableId, SampleStatus>>> observer;

            public Subscription(List<Subscription> owner,
                Action<StateChange<IReadOnlyDictionary<StableId, SampleStatus>>> observer)
            {
                this.owner = owner;
                this.observer = observer;
            }

            public void Notify(StateChange<IReadOnlyDictionary<StableId, SampleStatus>> change) => observer?.Invoke(change);

            public void Dispose()
            {
                owner.Remove(this);
                observer = null;
            }
        }
    }
}
