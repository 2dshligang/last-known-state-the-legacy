using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace LKS
{
    public sealed class StationQuests : IQuestService, IDisposable
    {
        public static readonly StableId Player = new StableId("actor.alexei");
        public static readonly StableId Confirm = new StableId("action.confirm");
        public static readonly StableId Recover = new StableId("action.recover-power");
        public static readonly StableId RecoveryTarget = new StableId("target.power-recovery");

        private static readonly IReadOnlyList<StableId> Route = Array.AsReadOnly(new[]
        {
            new StableId("quest.arrival"), new StableId("quest.safe-power"),
            new StableId("quest.echo-evidence"), new StableId("quest.sampling"),
            new StableId("quest.analysis"), new StableId("quest.report"), new StableId("quest.ending")
        });

        public static IReadOnlyList<StableId> RequiredStages => Route;

        private readonly Func<StableId, ResultCode> checkEvidence;
        private readonly Func<CommandResult> recoverPower;
        private readonly Func<long> nextSequence;
        private readonly Action<Action> enqueue;
        private readonly List<Subscription> observers = new List<Subscription>();
        private int completed;
        private bool disposed;

        public IReadOnlyDictionary<StableId, QuestStatus> Snapshot { get; private set; }

        public StationQuests(Func<StableId, ResultCode> checkEvidence, Func<CommandResult> recoverPower,
            IEnumerable<StableId> checkpoint, Func<long> nextSequence, Action<Action> enqueue)
        {
            this.checkEvidence = checkEvidence ?? throw new ArgumentNullException(nameof(checkEvidence));
            this.recoverPower = recoverPower ?? throw new ArgumentNullException(nameof(recoverPower));
            this.nextSequence = nextSequence ?? throw new ArgumentNullException(nameof(nextSequence));
            this.enqueue = enqueue ?? throw new ArgumentNullException(nameof(enqueue));
            var restored = checkpoint?.ToArray() ?? Array.Empty<StableId>();
            if (!restored.SequenceEqual(Route.Take(restored.Length)))
                throw new ArgumentException("Quest checkpoint must be an ordered prefix of the station route.", nameof(checkpoint));
            completed = restored.Length;
            PublishSnapshot();
        }

        public static IEnumerable<string> ValidateReachability(IEnumerable<StableId> boundTargets)
        {
            var targets = new HashSet<StableId>(boundTargets);
            StableId.ValidateUnique(Route);
            foreach (var stage in Route)
            {
                if (!targets.Contains(stage))
                    yield return $"Required quest stage '{stage}' has no interaction target.";
            }

            if (!targets.Contains(RecoveryTarget))
                yield return "The power recovery interaction is missing.";
        }

        public IReadOnlyList<StableId> CaptureCheckpoint() =>
            Array.AsReadOnly(Route.Take(completed).ToArray());

        public IEnumerable<IInteractable> CreateTargets()
        {
            foreach (var stage in Route)
                yield return new Target(this, stage, Confirm);
            yield return new Target(this, RecoveryTarget, Recover);
        }

        public IDisposable Subscribe(Action<StateChange<IReadOnlyDictionary<StableId, QuestStatus>>> observer)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(StationQuests));
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

        private ResultCode Evaluate(StableId target, StableId actor)
        {
            if (disposed || actor != Player)
                return ResultCode.Unavailable;
            if (target == RecoveryTarget)
                return completed > 0 ? checkEvidence(target) : ResultCode.PrerequisiteMissing;

            int index = IndexOf(target);
            if (index < completed)
                return ResultCode.AlreadyApplied;
            if (index > completed)
                return ResultCode.PrerequisiteMissing;

            // Each domain owns the evidence and performs a side-effect-free readiness check.
            return index == 0 ? ResultCode.Applied : checkEvidence(target);
        }

        private CommandResult Execute(StableId target, StableId actor, StableId action, StableId expected)
        {
            if (action != expected)
                return Result(ResultCode.InvalidRequest);
            var code = Evaluate(target, actor);
            if (code != ResultCode.Applied)
                return Result(code);
            if (target == RecoveryTarget)
                return recoverPower();

            completed++;
            PublishSnapshot();
            var change = new StateChange<IReadOnlyDictionary<StableId, QuestStatus>>(
                nextSequence(), target, action, Snapshot);
            foreach (var listener in observers.ToArray())
                enqueue(() => listener.Notify(change));
            return Result(ResultCode.Applied);
        }

        private static int IndexOf(StableId target)
        {
            for (int index = 0; index < Route.Count; index++)
            {
                if (Route[index] == target)
                    return index;
            }
            throw new ArgumentException("Unknown quest target.", nameof(target));
        }

        private void PublishSnapshot()
        {
            var states = new Dictionary<StableId, QuestStatus>();
            for (int index = 0; index < Route.Count; index++)
            {
                states.Add(Route[index], index < completed ? QuestStatus.Completed :
                    index == completed ? QuestStatus.Available : QuestStatus.Locked);
            }
            Snapshot = new ReadOnlyDictionary<StableId, QuestStatus>(states);
        }

        private static CommandResult Result(ResultCode code)
        {
            switch (code)
            {
                case ResultCode.Applied: return new CommandResult(code, new StableId("quest.result.applied"));
                case ResultCode.AlreadyApplied: return new CommandResult(code, new StableId("quest.result.completed"));
                case ResultCode.PrerequisiteMissing: return new CommandResult(code, new StableId("quest.result.prerequisite"));
                case ResultCode.InsufficientResources: return new CommandResult(code, new StableId("quest.result.resources"));
                case ResultCode.EvidenceInsufficient: return new CommandResult(code, new StableId("quest.result.evidence"));
                case ResultCode.InvalidRequest: return new CommandResult(code, new StableId("quest.result.invalid-action"));
                default: return new CommandResult(ResultCode.Unavailable, new StableId("quest.result.unavailable"));
            }
        }

        private sealed class Target : IInteractable
        {
            private readonly StationQuests quests;
            private readonly StableId action;
            public StableId Id { get; }

            public Target(StationQuests quests, StableId id, StableId action)
            {
                this.quests = quests;
                Id = id;
                this.action = action;
            }

            public InteractionState Read(StableId actor)
            {
                var code = quests.Evaluate(Id, actor);
                return code == ResultCode.Applied
                    ? new InteractionState(true, action)
                    : new InteractionState(false, Result(code).MessageKey);
            }

            public CommandResult Request(StableId actor, StableId requestedAction) =>
                quests.Execute(Id, actor, requestedAction, action);
        }

        private sealed class Subscription : IDisposable
        {
            private readonly List<Subscription> owner;
            private Action<StateChange<IReadOnlyDictionary<StableId, QuestStatus>>> observer;

            public Subscription(List<Subscription> owner,
                Action<StateChange<IReadOnlyDictionary<StableId, QuestStatus>>> observer)
            {
                this.owner = owner;
                this.observer = observer;
            }

            public void Notify(StateChange<IReadOnlyDictionary<StableId, QuestStatus>> change) =>
                observer?.Invoke(change);

            public void Dispose()
            {
                owner.Remove(this);
                observer = null;
            }
        }
    }
}
