#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using LKS.World;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace LKS.Editor.World
{
    public sealed class StationEchoReviewTests
    {
        private const string ScenePath = "Assets/_Game/Scenes/Station.unity";
        private static readonly StableId RequiredArchive = new StableId("archive.donki-sep");

        [UnityTest]
        public IEnumerator EveryReplayUsesTheSameBoundedProjectionWithoutChangingEvidenceOrCollision()
        {
            yield return LoadStation();
            var player = Object.FindAnyObjectByType<StationFirstPerson>();
            var interaction = player.GetComponent<StationInteraction>();
            var reader = player.GetComponent<StationArchiveReader>();
            var review = player.GetComponent<StationEchoReview>();
            var session = interaction.Session;
            int colliders = Object.FindObjectsByType<Collider>().Length;
            int projected = 0;

            for (int i = 0; i < 5; i++)
            {
                OpenReview(interaction, reader, review);
                Assert.That(player.HasReadingFocus && review.ProjectionVisible);
                Assert.That(review.Playback.Seconds, Is.Zero);
                if (i == 0) projected = review.ProjectedObjectCount;
                Assert.That(projected, Is.InRange(10, 70));
                Assert.That(review.ProjectedObjectCount, Is.EqualTo(projected));
                Assert.That(review.Replay());
                Assert.That(review.Scrub(4));
                Assert.That(review.Playback.Playing, Is.False);
                Assert.That(StationEcho.Timeline.Count(frame => frame.ReplaySecond == 4), Is.EqualTo(2));
                Assert.That(review.Playback.Frame.Sequence, Is.EqualTo(3));
                Assert.That(review.Playback.Frame.Evidence.Quality, Is.EqualTo(EvidenceQuality.Estimated));
                Assert.That(StationEchoReview.LabelOf(review.Playback.Frame), Is.EqualTo("Simplified model estimate"));
                Assert.That(review.CaptionOf(review.Playback.Frame), Does.Contain("suggests"));
                Assert.That(review.Scrub(8));
                Assert.That(review.Playback.Frame.Evidence.Quality, Is.EqualTo(EvidenceQuality.Missing));
                Assert.That(StationEchoReview.DateOf(review.Playback.Frame), Is.EqualTo("Original timestamp unknown"));
                Assert.That(review.ProjectionVisible, Is.False);
                Assert.That(review.Scrub(14));
                Assert.That(review.ProjectionVisible, Is.True);
                Assert.That(review.Playback.Frame.Evidence.Quality, Is.EqualTo(EvidenceQuality.Recorded));
                Assert.That(StationEchoReview.DateOf(review.Playback.Frame), Is.EqualTo("18 Aug 2059, 14:21 UTC (story time)"));
                Press(review, KeyCode.LeftArrow);
                Assert.That(review.Playback.Seconds, Is.EqualTo(13m));
                Assert.That(review.ProjectionVisible, Is.False);
                review.Close();
                yield return null;

                Assert.That(review.IsOpen || review.ProjectionVisible || player.HasReadingFocus, Is.False);
                Assert.That(session.Echo.Snapshot.Seconds, Is.Zero);
                Assert.That(session.Echo.Snapshot.Playing, Is.False);
                Assert.That(Object.FindObjectsByType<Collider>().Length, Is.EqualTo(colliders));
                Assert.That(session.CaptureQuestCheckpoint(), Is.Empty);
                Assert.That(session.Archive.Capture(), Is.Empty);
                Assert.That(session.Investigation.Conclusion, Is.Not.EqualTo(EchoConclusion.Confirmed));
            }
            Object.Destroy(review);
            yield return null;
            Assert.That(player.transform.Find("ECHO replay projection"), Is.Null);
        }

        [UnityTest]
        public IEnumerator GlobalPauseFreezesReplayAndDiskReloadStartsWithCleanPresentation()
        {
            yield return LoadStation();
            var player = Object.FindAnyObjectByType<StationFirstPerson>();
            var interaction = player.GetComponent<StationInteraction>();
            var reader = player.GetComponent<StationArchiveReader>();
            var review = player.GetComponent<StationEchoReview>();
            var session = interaction.Session;
            var folder = Path.Combine(Application.temporaryCachePath, "lks-w10-" + Guid.NewGuid().ToString("N"));
            var slot = new StableId("echo-replay");
            var saves = new StationSaves(folder, (StationScienceCatalog)session.Science,
                new[] { new StableId("donki.2017-09-04t23-52-00-sep-001") });
            try
            {
                StationArchiveReaderTests.OpenAtTerminal(interaction, reader);
                Assert.That(reader.OpenDocument(0, Vector2.zero) && reader.MarkAsRead());
                Assert.That(reader.OpenReplay(), Is.False);
                Assert.That(reader.Document, Is.Not.Null);
                reader.Close();
                Assert.That(saves.Save(slot, session).Succeeded);

                OpenReview(interaction, reader, review);
                Assert.That(review.Replay());
                float deadline = Time.realtimeSinceStartup + 3;
                while (review.Playback.Seconds == 0m && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.That(review.Playback.Seconds, Is.Positive);
                player.SetPaused(true);
                yield return null;
                decimal frozen = session.Echo.Snapshot.Seconds;
                yield return new WaitForSecondsRealtime(.15f);
                Assert.That(session.Echo.Snapshot.Seconds, Is.EqualTo(frozen));
                Assert.That(review.Scrub(14), Is.False);
                player.SetPaused(false);
                Assert.That(player.HasReadingFocus, Is.True);
                Assert.That(review.Hold());
                Assert.That(review.Playback.Playing, Is.False);

                var bootstrap = player.GetComponent<SessionBootstrap>();
                var load = saves.Load(slot, bootstrap, () => Array.Empty<IInteractable>(), _ => ResultCode.EvidenceInsufficient);
                Assert.That(load.Succeeded);
                yield return null;
                Assert.That(review.IsOpen || player.HasReadingFocus || review.ProjectionVisible, Is.False);
                Assert.That(interaction.Session.Archive.Capture(), Is.EqualTo(new[] { RequiredArchive }));
                OpenReview(interaction, reader, review);
                Assert.That(review.Playback.Seconds, Is.Zero);
                Assert.That(review.Playback.Frame.Evidence.Id.value, Is.EqualTo("echo.stale-input"));
                Assert.That(review.Playback.Playing, Is.False);
                review.enabled = false;
                Assert.That(player.HasReadingFocus || review.ProjectionVisible, Is.False);
            }
            finally
            {
                player.SetPaused(false);
                if (Directory.Exists(folder))
                {
                    foreach (string file in Directory.GetFiles(folder)) File.Delete(file);
                    Directory.Delete(folder);
                }
            }
        }

        internal static void OpenReview(StationInteraction interaction, StationArchiveReader reader, StationEchoReview review)
        {
            Assert.That(review, Is.Not.Null, "The saved scene must bind the ECHO reconstruction.");
            StationArchiveReaderTests.OpenAtTerminal(interaction, reader);
            reader.HandleKeyboard(new Event { type = EventType.KeyDown, keyCode = KeyCode.Tab });
            reader.HandleKeyboard(new Event { type = EventType.KeyDown, keyCode = KeyCode.Return });
            Assert.That(reader.IsOpen, Is.False);
            Assert.That(review.IsOpen, Is.True);
        }

        private static IEnumerator LoadStation()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            Object.FindAnyObjectByType<StationFirstPerson>().SendMessage("OnApplicationFocus", true);
        }

        private static void Press(StationEchoReview review, KeyCode key) =>
            review.HandleKeyboard(new Event { type = EventType.KeyDown, keyCode = key });
    }
}
#endif
