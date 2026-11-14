using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LKS.Tests
{
    public sealed class EnglishContentTests
    {
        [Test]
        public void MissingLookupAndInvalidCatalogFailAsDocumented()
        {
            var catalog = EnglishContentCatalog.CreateDefault();
            var missing = new StableId("text.missing");
            Assert.That(catalog.Resolve(missing, true), Is.EqualTo("[Missing text: text.missing]"));
            Assert.Throws<KeyNotFoundException>(() => catalog.Resolve(missing, false));
            Assert.That(catalog.Resolve(EnglishContentCatalog.EndingKey, false), Is.EqualTo("Dad... I did it."));

            var ending = new KeyValuePair<StableId, string>(EnglishContentCatalog.EndingKey, "Dad... I did it.");
            Assert.Throws<KeyNotFoundException>(() => new EnglishContentCatalog(new[] { ending }, new[] { missing }));
            Assert.Throws<ArgumentException>(() => new EnglishContentCatalog(new[] { ending, ending }, Array.Empty<StableId>()));
            Assert.Throws<ArgumentException>(() => new EnglishContentCatalog(new[] { ending,
                new KeyValuePair<StableId, string>(missing, "\u0410") }, Array.Empty<StableId>()));
            Assert.Throws<ArgumentException>(() => new EnglishContentCatalog(new[] {
                new KeyValuePair<StableId, string>(ending.Key, "Dad… I did it.") }, Array.Empty<StableId>()));
        }

        [Test]
        public void AcknowledgmentIsExplicitIdempotentAndRestored()
        {
            var host = new GameObject("Archive test");
            try
            {
                var bootstrap = host.AddComponent<SessionBootstrap>();
                var clock = new Clock();
                Start(bootstrap, clock);
                var run = bootstrap.Current;
                var id = new StableId("archive.donki-sep");
                Assert.That(run.Archive.Acknowledge(id).Code, Is.EqualTo(ResultCode.Unavailable));
                using (run.Archive.Open(id))
                {
                    using (run.Archive.Open(id))
                    {
                        Assert.That(run.Archive.Capture(), Is.Empty);
                        Assert.That(run.Archive.Acknowledge(id).Code, Is.EqualTo(ResultCode.Applied));
                    }
                    Assert.That(run.Archive.Acknowledge(id).Code, Is.EqualTo(ResultCode.AlreadyApplied));
                    clock.Seconds = 2;
                    run.Tick();
                    Assert.That(run.LifeSupport.Capture().TrainingSeconds, Is.Zero);
                }
                clock.Seconds = 3;
                run.Tick();
                Assert.That(run.LifeSupport.Capture().TrainingSeconds, Is.EqualTo(10));
                Start(bootstrap, clock, run.Archive.Capture());
                Assert.That(bootstrap.Current.Archive.Capture(), Is.EqualTo(new[] { id }));
                Start(bootstrap, clock);
                Assert.That(bootstrap.Current.Archive.Capture(), Is.Empty);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        private static void Start(SessionBootstrap bootstrap, Clock clock, IEnumerable<StableId> archived = null)
        {
            Assert.That(bootstrap.RestartWithPower(new Catalog(), Array.Empty<StableId>(),
                () => Array.Empty<IInteractable>(), _ => ResultCode.EvidenceInsufficient,
                clock: clock, archiveCheckpoint: archived), Is.Empty);
        }

        private sealed class Clock : ISessionClock { public double Seconds { get; set; } }
        private sealed class Catalog : IScienceCatalog
        {
            public bool TryGet(StableId id, out ScienceRecord record)
            {
                record = default;
                return false;
            }
        }
    }
}
