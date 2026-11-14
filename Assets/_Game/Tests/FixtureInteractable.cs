namespace LKS.Tests
{
    internal sealed class FixtureInteractable : IInteractable
    {
        internal static readonly StableId Actor = new StableId("actor.fixture");
        internal static readonly StableId Inspect = new StableId("action.inspect");
        private bool inspected;

        public StableId Id { get; } = new StableId("fixture.archive");
        public InteractionState Read(StableId actor) =>
            new InteractionState(actor == Actor, new StableId("prompt.inspect"));

        public CommandResult Request(StableId actor, StableId action)
        {
            if (!Read(actor).Available || action != Inspect)
                return new CommandResult(ResultCode.InvalidRequest, new StableId("result.invalid-request"));
            if (inspected)
                return new CommandResult(ResultCode.AlreadyApplied, new StableId("result.already-inspected"));
            inspected = true;
            return new CommandResult(ResultCode.Applied, new StableId("result.inspected"));
        }
    }
}
