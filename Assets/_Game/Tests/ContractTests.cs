using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace LKS.Tests
{
    public sealed class ContractTests
    {
        [Test]
        public void IndependentlyConstructedIdsAreEqualDictionaryKeys()
        {
            var first = new StableId("fixture.archive");
            var second = new StableId("fixture.archive");
            var dictionary = new Dictionary<StableId, int> { { first, 42 } };

            Assert.That(first == second, Is.True);
            Assert.That(dictionary[second], Is.EqualTo(42));
            Assert.That(first != new StableId("fixture.other"), Is.True);
        }

        [Test]
        public void BindingRejectsDuplicateAndInvalidSerializedIds()
        {
            var id = new StableId("fixture.archive");
            StableId.ValidateUnique(new[] { id, new StableId("fixture.other") });
            Assert.Throws<ArgumentException>(() => StableId.ValidateUnique(new[] { id, id }));
            Assert.Throws<ArgumentException>(() => StableId.ValidateUnique(new[] { default(StableId) }));
            Assert.Throws<ArgumentException>(() => StableId.ValidateUnique(new[] { new StableId { value = "Upper" } }));
        }

        [Test]
        public void InteractionRejectsInvalidActionAndAppliesValidActionOnlyOnce()
        {
            IInteractable target = new FixtureInteractable();
            Assert.That(target.Read(default).Available, Is.False);
            Assert.That(target.Read(FixtureInteractable.Actor).Available, Is.True);
            var rejected = target.Request(FixtureInteractable.Actor, default);
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(rejected.Code, Is.EqualTo(ResultCode.InvalidRequest));
            Assert.That(target.Request(FixtureInteractable.Actor, FixtureInteractable.Inspect).Code,
                Is.EqualTo(ResultCode.Applied));
            var repeated = target.Request(FixtureInteractable.Actor, FixtureInteractable.Inspect);
            Assert.That(repeated.Succeeded, Is.True);
            Assert.That(repeated.Code, Is.EqualTo(ResultCode.AlreadyApplied));
            Assert.That(default(CommandResult).Succeeded, Is.False);
        }

        [Test]
        public void SnapshotsOwnTheirInputs()
        {
            var loads = new List<LoadState> { new LoadState(new StableId("load.air"), true, true) };
            var power = new PowerSnapshot(6, 2, 1, 2, loads);
            loads.Clear();
            Assert.That(power.Loads.Count, Is.EqualTo(1));

            var bytes = new byte[] { 42 };
            var save = new SaveSnapshot(1, "fixture", bytes);
            bytes[0] = 0;
            save.CopyPayload()[0] = 1;
            Assert.That(save.CopyPayload()[0], Is.EqualTo(42));
        }
    }
}
