#if UNITY_EDITOR
using System.Collections;
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

        [UnityTest]
        public IEnumerator EveryReplayUsesTheSameBoundedProjectionWithoutChangingEvidenceOrCollision()
        {
            yield return LoadStation();
            var player = Object.FindAnyObjectByType<StationFirstPerson>();
            var interaction = player.GetComponent<StationInteraction>();
            var review = player.gameObject.AddComponent<StationEchoReview>();
            var session = interaction.Session;
            int colliders = Object.FindObjectsByType<Collider>().Length;
            int projected = 0;

            for (int i = 0; i < 5; i++)
            {
                Assert.That(review.Open(player, () => interaction.Session));
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
        public IEnumerator GlobalPauseFreezesReplayAndCheckpointReplacementStartsWithCleanPresentation()
        {
            yield return LoadStation();
            var player = Object.FindAnyObjectByType<StationFirstPerson>();
            var interaction = player.GetComponent<StationInteraction>();
            var review = player.gameObject.AddComponent<StationEchoReview>();
            var session = interaction.Session;
            Assert.That(review.Open(player, () => interaction.Session));
            Assert.That(review.Replay());
            float deadline = Time.realtimeSinceStartup + 3;
            while (review.Playback.Seconds == 0m && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(review.Playback.Seconds, Is.Positive);
            player.SetPaused(true);
            try
            {
                yield return null;
                decimal frozen = session.Echo.Snapshot.Seconds;
                yield return new WaitForSecondsRealtime(.15f);
                Assert.That(session.Echo.Snapshot.Seconds, Is.EqualTo(frozen));
                Assert.That(review.Scrub(14), Is.False);
            }
            finally { player.SetPaused(false); }
            Assert.That(player.HasReadingFocus, Is.True);
            Assert.That(review.Hold());
            Assert.That(review.Playback.Playing, Is.False);
            var saved = session;
            Assert.That(interaction.Restore(saved.Power.Capture(), saved.CaptureQuestCheckpoint(),
                saved.LifeSupport.Capture(), saved.Logistics.Capture(), saved.Archive.Capture(),
                saved.Investigation.Capture(), saved.Samples.Capture(), saved.Research.CaptureAccepted()));
            Assert.That(review.IsOpen || player.HasReadingFocus || review.ProjectionVisible, Is.False);
            Assert.That(review.Open(player, () => interaction.Session));
            Assert.That(review.Playback.Seconds, Is.Zero);
            Assert.That(review.Playback.Frame.Evidence.Id.value, Is.EqualTo("echo.stale-input"));
            Assert.That(review.Playback.Playing, Is.False);
            review.enabled = false;
            Assert.That(player.HasReadingFocus || review.ProjectionVisible, Is.False);
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
