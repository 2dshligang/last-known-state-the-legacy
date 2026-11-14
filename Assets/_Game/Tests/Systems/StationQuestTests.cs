using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace LKS.Tests
{
    public sealed class StationQuestTests
    {
        private GameObject host;
        private SessionBootstrap bootstrap;
        private bool safePower;
        private int recoveries;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Quest bootstrap test");
            bootstrap = host.AddComponent<SessionBootstrap>();
            safePower = false;
            recoveries = 0;
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(host);

        [Test]
        public void OrderedRouteRejectsInvalidActionsAndRecoversFromUnsafePower()
        {
            Assert.That(Restart(), Is.Empty);
            var route = StationQuests.RequiredStages;
            var session = bootstrap.Current;
            var events = new List<StableId>();
            using var subscription = session.Quests.Subscribe(change => events.Add(change.Subject));

            var locked = Target(route[2]);
            Assert.That(locked.Read(StationQuests.Player).Available, Is.False);
            Assert.That(Confirm(locked).Code, Is.EqualTo(ResultCode.PrerequisiteMissing));
            var arrival = Target(route[0]);
            Assert.That(arrival.Read(StationQuests.Player).Available, Is.True);
            Assert.That(arrival.Request(default, StationQuests.Confirm).Code, Is.EqualTo(ResultCode.Unavailable));
            Assert.That(arrival.Request(StationQuests.Player, StationQuests.Recover).Code, Is.EqualTo(ResultCode.InvalidRequest));
            Assert.That(session.CaptureQuestCheckpoint(), Is.Empty);
            Assert.That(Confirm(arrival).Code, Is.EqualTo(ResultCode.Applied));
            Assert.That(Confirm(arrival).Code, Is.EqualTo(ResultCode.AlreadyApplied));

            var power = Target(route[1]);
            var blocked = Confirm(power);
            Assert.That(blocked.Code, Is.EqualTo(ResultCode.InsufficientResources));
            Assert.That(power.Read(StationQuests.Player).PromptKey, Is.EqualTo(blocked.MessageKey));
            Assert.That(session.CaptureQuestCheckpoint(), Has.Count.EqualTo(1));
            var recovery = Target(StationQuests.RecoveryTarget);
            Assert.That(recovery.Read(StationQuests.Player).Available, Is.True);
            Assert.That(recovery.Request(StationQuests.Player, StationQuests.Recover).Succeeded, Is.True);
            Assert.That(recoveries, Is.EqualTo(1));

            foreach (var stage in route.Skip(1))
                Assert.That(Confirm(Target(stage)).Code, Is.EqualTo(ResultCode.Applied), stage.ToString());

            Assert.That(events, Is.Empty);
            session.Dispatch();
            Assert.That(events, Is.EqualTo(route));
            Assert.That(session.CaptureQuestCheckpoint(), Is.EqualTo(route));
        }

        [Test]
        public void ReloadRejectsInvalidPrefixAndNewGameClearsProgress()
        {
            Assert.That(Restart(), Is.Empty);
            var arrivalId = StationQuests.RequiredStages[0];
            var oldTarget = Target(arrivalId);
            Assert.That(Confirm(oldTarget).Succeeded, Is.True);
            var previous = bootstrap.Current;
            var checkpoint = previous.CaptureQuestCheckpoint();

            Assert.That(Restart(StationQuests.RequiredStages.Skip(1).Take(1)), Is.Not.Empty);
            Assert.That(bootstrap.Current, Is.SameAs(previous));
            Assert.That(Restart(checkpoint), Is.Empty);
            Assert.That(Confirm(oldTarget).Code, Is.EqualTo(ResultCode.Unavailable));
            Assert.That(bootstrap.Current.Quests.Snapshot[arrivalId], Is.EqualTo(QuestStatus.Completed));
            Assert.That(bootstrap.Current.CaptureQuestCheckpoint(), Is.EqualTo(checkpoint));

            Assert.That(Restart(), Is.Empty);
            Assert.That(bootstrap.Current.CaptureQuestCheckpoint(), Is.Empty);
            Assert.That(bootstrap.Current.Quests.Snapshot[arrivalId], Is.EqualTo(QuestStatus.Available));
        }

        [Test]
        public void ReachabilityValidationIdentifiesMissingBindings()
        {
            var route = StationQuests.RequiredStages;
            Assert.That(StationQuests.ValidateReachability(route.Concat(new[] { StationQuests.RecoveryTarget })), Is.Empty);
            Assert.That(StationQuests.ValidateReachability(route.Skip(1)).Count(), Is.EqualTo(2));
        }

        private IReadOnlyList<string> Restart(IEnumerable<StableId> checkpoint = null) =>
            bootstrap.RestartWithQuests(new EmptyCatalog(), Array.Empty<StableId>(),
                () => Array.Empty<IInteractable>(),
                stage => stage == StationQuests.RequiredStages[1] && !safePower
                    ? ResultCode.InsufficientResources : ResultCode.Applied,
                () =>
                {
                    recoveries++;
                    safePower = true;
                    return new CommandResult(ResultCode.Applied, new StableId("result.power-recovered"));
                }, checkpoint);

        private IInteractable Target(StableId id)
        {
            Assert.That(bootstrap.Current.TryGetTarget(id, out var target), Is.True);
            return target;
        }

        private static CommandResult Confirm(IInteractable target) =>
            target.Request(StationQuests.Player, StationQuests.Confirm);

        private sealed class EmptyCatalog : IScienceCatalog
        {
            public bool TryGet(StableId id, out ScienceRecord record)
            {
                record = default;
                return false;
            }
        }
    }
}
