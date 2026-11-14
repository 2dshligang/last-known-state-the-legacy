using System;

namespace LKS
{
    public sealed class StationEnding
    {
        public static readonly StableId Photograph = new StableId("quest.ending");
        public static readonly StableId PhotographInspected = new StableId("cause.photograph-inspected");

        private static readonly NarrativeCue FinalCue =
            new NarrativeCue("ending.final", "portrait.dmitry-photograph", "camera.final-photograph");

        private readonly GameplaySession session;
        private readonly IStateSource<bool> completion;

        internal StationEnding(GameplaySession session)
        {
            this.session = session;
            completion = new CompletionSource(session);
        }

        public IStateSource<bool> Completion => completion;
        public bool IsCompleted => session.Quests?.Snapshot[Photograph] == QuestStatus.Completed;

        public CommandResult QueueAcceptedReport()
        {
            if (session.IsDisposed)
                return Result(ResultCode.Unavailable, "unavailable");
            if (session.Research.Snapshot != ReportStatus.Accepted)
                return Result(ResultCode.EvidenceInsufficient, "accept-report");

            return session.Logistics.QueueReport(StationLogistics.ReportMessage);
        }

        public CommandResult Readiness()
        {
            if (session.IsDisposed)
                return Result(ResultCode.Unavailable, "unavailable");
            if (IsCompleted)
                return Result(ResultCode.AlreadyApplied, "completed");
            if (session.LifeSupport.Snapshot.Air != AirStatus.Safe)
                return Result(ResultCode.InsufficientResources, "restore-air");
            if (session.Investigation.Conclusion != EchoConclusion.Confirmed)
                return Result(ResultCode.EvidenceInsufficient, "confirm-echo");
            if (!session.Samples.AllReviewed)
                return Result(ResultCode.EvidenceInsufficient, "review-samples");
            if (session.Research.Snapshot != ReportStatus.Accepted)
                return Result(ResultCode.EvidenceInsufficient, "accept-report");
            if (!HasTransmission)
                return Result(ResultCode.PrerequisiteMissing, "queue-report");
            if (session.Quests.Snapshot[Photograph] != QuestStatus.Available)
                return Result(ResultCode.PrerequisiteMissing, "finish-objectives");

            return Result(ResultCode.Applied, "ready");
        }

        public bool TryGetFinalCue(out NarrativeCue cue)
        {
            cue = !session.IsDisposed && IsCompleted ? FinalCue : null;
            return cue != null;
        }

        internal bool HasTransmission => session.Logistics.Delivery == DeliveryState.Queued ||
            session.Logistics.Delivery == DeliveryState.Delivered;

        internal IInteractable BindPhotograph(IInteractable questTarget) => new PhotographTarget(this, questTarget);

        private static CommandResult Result(ResultCode code, string key) =>
            new CommandResult(code, new StableId("ending.result." + key));

        private sealed class PhotographTarget : IInteractable
        {
            private readonly StationEnding ending;
            private readonly IInteractable target;

            public PhotographTarget(StationEnding ending, IInteractable target)
            {
                this.ending = ending;
                this.target = target;
            }

            public StableId Id => target.Id;

            public InteractionState Read(StableId actor)
            {
                if (actor != StationQuests.Player)
                    return target.Read(actor);
                var readiness = ending.Readiness();
                return readiness.Code == ResultCode.Applied ? target.Read(actor) :
                    new InteractionState(false, readiness.MessageKey);
            }

            public CommandResult Request(StableId actor, StableId action)
            {
                if (actor != StationQuests.Player || action != StationQuests.Confirm)
                    return target.Request(actor, action);
                var readiness = ending.Readiness();
                return readiness.Code == ResultCode.Applied ? target.Request(actor, action) : readiness;
            }
        }

        private sealed class CompletionSource : IStateSource<bool>
        {
            private readonly GameplaySession session;

            public CompletionSource(GameplaySession session) => this.session = session;

            public bool Snapshot => session.Ending.IsCompleted;

            public IDisposable Subscribe(Action<StateChange<bool>> observer)
            {
                if (observer == null)
                    throw new ArgumentNullException(nameof(observer));
                return session.Quests.Subscribe(change =>
                {
                    if (change.Subject == Photograph && change.Snapshot[Photograph] == QuestStatus.Completed)
                        observer(new StateChange<bool>(change.Sequence, Photograph, PhotographInspected, true));
                });
            }
        }
    }
}
