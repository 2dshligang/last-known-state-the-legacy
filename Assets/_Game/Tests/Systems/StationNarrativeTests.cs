using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LKS.Tests
{
    public sealed class StationNarrativeTests
    {
        private GameObject host;
        private SessionBootstrap bootstrap;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Narrative test");
            bootstrap = host.AddComponent<SessionBootstrap>();
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(host);

        [Test]
        public void NormalAndSkippedOpeningsHaveEquivalentGameplayProgress()
        {
            var normal = Start();
            AssertCue(normal, "narrative.farewell", "portrait.dmitry", "camera.farewell");
            Assert.That(normal.Narrative.AdvanceOpening(), Is.True);
            AssertCue(normal, "narrative.arrival", "portrait.alexei", "camera.station-arrival");
            Assert.That(normal.Narrative.AdvanceOpening(), Is.True);
            Assert.That(normal.Narrative.TryGetOpening(out _), Is.False);
            var progress = normal.CaptureQuestCheckpoint();

            var skipped = Start();
            Assert.That(skipped.Narrative.SkipOpening(), Is.True);
            Assert.That(skipped.Narrative.CaptureOpening(), Is.EqualTo(normal.Narrative.CaptureOpening()));
            Assert.That(skipped.CaptureQuestCheckpoint(), Is.EqualTo(progress).And.Empty);
            Assert.That(skipped.Quests.Snapshot[StationQuests.RequiredStages[0]], Is.EqualTo(QuestStatus.Available));
            Assert.That(skipped.Quests.Snapshot[StationQuests.RequiredStages[1]], Is.EqualTo(QuestStatus.Locked));
            Assert.That(skipped.Narrative.SkipOpening(), Is.False);
        }

        [Test]
        public void RestoreResumesAtArrivalAndReplayStaysOutOfCheckpoint()
        {
            var before = Start();
            before.Narrative.AdvanceOpening();
            var run = Start(before.Narrative.CaptureOpening());
            Assert.That(before.Narrative.TryGetOpening(out _), Is.False);
            AssertCue(run, "narrative.arrival", "portrait.alexei", "camera.station-arrival");
            Assert.That(run.Narrative.ReplayOpening(), Is.False);
            run.Narrative.AdvanceOpening();

            Assert.That(run.Narrative.ReplayOpening(), Is.True);
            Assert.That(run.Narrative.ReplayOpening(), Is.False);
            AssertCue(run, "narrative.farewell", "portrait.dmitry", "camera.farewell");
            Assert.That(run.Narrative.CaptureOpening(), Is.EqualTo(OpeningProgress.Complete));
            Assert.That(run.Narrative.SkipOpening(), Is.True);
            Assert.That(run.Narrative.IsReplaying, Is.False);
            Assert.That(run.Narrative.TryGetOpening(out _), Is.False);
            Assert.That(run.Narrative.ReplayOpening(), Is.True);
            run.Narrative.AdvanceOpening();
            run.Narrative.AdvanceOpening();
            Assert.That(run.Narrative.TryGetOpening(out _), Is.False);
            Assert.That(run.CaptureQuestCheckpoint(), Is.Empty);
        }

        [Test]
        public void DatesAndEnglishKeysAgreeWithTheStory()
        {
            var run = Start();
            var content = run.Archive.Content;
            Assert.That(AgeAt(StationNarrative.FarewellDate), Is.EqualTo(8));
            Assert.That(AgeAt(StationNarrative.ArrivalDate), Is.EqualTo(39));
            Assert.That(content.Resolve(new StableId("narrative.farewell"), false), Does.Contain("January 20, 2059"));
            Assert.That(content.Resolve(new StableId("narrative.arrival"), false), Does.Contain("November 14, 2089"));
            foreach (string key in new[]
            {
                "narrative.action.continue", "narrative.action.skip", "narrative.action.replay",
                "archive.dmitry-notes.summary", "archive.dmitry-notes.detail",
                "archive.arrival.summary", "archive.arrival.detail", "narrative.research-context"
            })
                Assert.That(content.Resolve(new StableId(key), false), Is.Not.Empty);

            Assert.That(content.Resolve(EnglishContentCatalog.EndingKey, false), Is.EqualTo("Dad... I did it."));
            Assert.That(run.Narrative.TryGetResearchContext(out _), Is.False);
        }

        private static int AgeAt(DateTime date)
        {
            var birth = StationNarrative.AlexeiBirthDate;
            int years = date.Year - birth.Year;
            return birth.AddYears(years) > date ? years - 1 : years;
        }

        private static void AssertCue(GameplaySession run, string key, string portrait, string camera)
        {
            Assert.That(run.Narrative.TryGetOpening(out var cue), Is.True);
            Assert.That(cue.TextKey, Is.EqualTo(new StableId(key)));
            var view = new CaptureView();
            cue.Present(view, run.Archive.Content);
            Assert.That(view.Text, Is.EqualTo(run.Archive.Content.Resolve(cue.TextKey, false)));
            Assert.That(view.Portrait, Is.EqualTo(new StableId(portrait)));
            Assert.That(view.Camera, Is.EqualTo(new StableId(camera)));
        }

        private GameplaySession Start(OpeningProgress opening = OpeningProgress.Farewell)
        {
            IReadOnlyList<string> errors = bootstrap.RestartWithPower(new Catalog(), Array.Empty<StableId>(),
                () => Array.Empty<IInteractable>(), _ => ResultCode.Unavailable, openingCheckpoint: opening);
            Assert.That(errors, Is.Empty);
            return bootstrap.Current;
        }

        private sealed class CaptureView : INarrativeView
        {
            public string Text { get; private set; }
            public StableId Portrait { get; private set; }
            public StableId Camera { get; private set; }

            public void ShowText(string englishText, StableId portrait, StableId cameraCue)
            {
                Text = englishText;
                Portrait = portrait;
                Camera = cameraCue;
            }
        }

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
