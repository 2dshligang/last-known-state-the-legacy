#if UNITY_EDITOR
using System;
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
    public sealed class StationArchiveReaderTests
    {
        private const string ScenePath = "Assets/_Game/Scenes/Station.unity";
        private static readonly StableId Donki = new StableId("archive.donki-sep");

        [UnityTest]
        public IEnumerator ReadsEveryDocumentMutedWithKeyboardAndRestoresListPosition()
        {
            yield return LoadStation();
            var player = Object.FindAnyObjectByType<StationFirstPerson>();
            var bootstrap = player.GetComponent<SessionBootstrap>();
            var reader = player.gameObject.AddComponent<StationArchiveReader>();
            float volume = AudioListener.volume;
            AudioListener.volume = 0;
            try
            {
                Assert.That(reader.Open(player, () => bootstrap.Current));
                Assert.That(reader.IsOpen && player.HasReadingFocus);
                var entries = EnglishContentCatalog.Archive;
                Press(reader, KeyCode.End);
                Assert.That(reader.SelectedIndex, Is.EqualTo(entries.Count - 1));
                var scrolled = reader.ListPosition;
                Assert.That(scrolled.y, Is.Positive);
                Press(reader, KeyCode.Return);
                Assert.That(reader.Document, Is.SameAs(entries[^1]));
                Assert.That(reader.Expanded, Is.False);
                Press(reader, KeyCode.Tab);
                Press(reader, KeyCode.Return);
                Assert.That(reader.Expanded, Is.True);
                Press(reader, KeyCode.Tab, shift: true);
                Press(reader, KeyCode.Return);
                Assert.That(reader.Document, Is.Null);
                Assert.That(reader.SelectedIndex, Is.EqualTo(entries.Count - 1));
                Assert.That(reader.ListPosition, Is.EqualTo(scrolled));

                for (int i = 0; i < entries.Count; i++)
                {
                    Assert.That(reader.OpenDocument(i, new Vector2(0, 44)));
                    Assert.That(StationArchiveReader.TitleOf(reader.Document), Is.Not.Empty);
                    Assert.That(reader.SummaryOf(reader.Document), Is.Not.Empty);
                    Assert.That(reader.SummaryOf(reader.Document), Does.Not.Contain("[Missing text:"));
                    Assert.That(reader.ToggleDetails());
                    Assert.That(reader.DetailOf(reader.Document), Is.Not.Empty);
                    Assert.That(reader.DetailOf(reader.Document), Does.Not.Contain("[Missing text:"));
                    Assert.That(reader.ReturnToList());
                    Assert.That(reader.ListPosition.y, Is.EqualTo(44));
                }
                Assert.That(StationArchiveReader.CategoryOf(entries[0]), Is.EqualTo("Archived NASA data"));
                Assert.That(StationArchiveReader.DateOf(entries[0]), Does.Contain("4 Sep 2017, 23:52 UTC"));
                Assert.That(StationArchiveReader.SourcesOf(entries[0]), Does.Contain("NASA DONKI archive, record 13000"));
                Assert.That(StationArchiveReader.CategoryOf(entries[1]), Does.Contain("Simplified model").And.Contain("Fictional"));
                Assert.That(StationArchiveReader.DateOf(entries[1]), Does.Contain("Station date (fiction)").And.Contain("2089"));
                Assert.That(StationArchiveReader.CategoryOf(entries[2]), Is.EqualTo("Fictional station record"));
                Assert.That(bootstrap.Current.Archive.Capture(), Is.Empty, "Reading alone does not acknowledge a document.");
                reader.Close();
                Assert.That(player.HasReadingFocus, Is.False);
            }
            finally { AudioListener.volume = volume; }
        }

        [UnityTest]
        public IEnumerator AcknowledgementIsDeliberateAndPauseReleaseSurvivesOverlappingReaderAndReplacement()
        {
            yield return LoadStation();
            var player = Object.FindAnyObjectByType<StationFirstPerson>();
            var bridge = player.GetComponent<StationInteraction>();
            var bootstrap = player.GetComponent<SessionBootstrap>();
            var reader = player.gameObject.AddComponent<StationArchiveReader>();
            var clock = new Clock();
            var stages = StationQuests.RequiredStages.Take(2).ToArray();
            var errors = bootstrap.RestartWithPower(bootstrap.Current.Science, Array.Empty<StableId>(),
                () => Array.Empty<IInteractable>(), _ => ResultCode.EvidenceInsufficient, questCheckpoint: stages, clock: clock);
            Assert.That(errors, Is.Empty);
            var session = bridge.Session;
            Assert.That(reader.MarkAsRead(), Is.False);
            Assert.That(reader.Open(player, () => bootstrap.Current));
            Assert.That(reader.OpenDocument(0, Vector2.zero));
            Assert.That(session.Archive.RequiredEntryAcknowledged, Is.False);
            player.SetPaused(true);
            Assert.That(reader.MarkAsRead(), Is.False);
            player.SetPaused(false);
            player.SendMessage("OnApplicationFocus", false);
            Assert.That(reader.MarkAsRead(), Is.False);
            player.SendMessage("OnApplicationFocus", true);
            Assert.That(reader.MarkAsRead(), Is.True);
            Assert.That(reader.Feedback, Is.EqualTo("Marked as read."));
            Assert.That(reader.MarkAsRead(), Is.True);
            Assert.That(reader.Feedback, Is.EqualTo("Already marked as read."));
            Assert.That(session.Archive.Capture(), Is.EqualTo(new[] { Donki }));
            Assert.That(session.CaptureQuestCheckpoint(), Is.EqualTo(stages));

            var battery = session.Power.Capture().BatteryKwh;
            using (player.AcquireReadingFocus())
            {
                reader.Close();
                Assert.That(player.HasReadingFocus, Is.True);
                Assert.That(session.Archive.Acknowledge(Donki).Code, Is.EqualTo(ResultCode.Unavailable));
                clock.Advance(session, 1);
                Assert.That(session.Power.Capture().BatteryKwh, Is.EqualTo(battery));
            }
            player.ResumePointer();
            yield return null;
            clock.Advance(session, 1);
            Assert.That(session.Power.Capture().BatteryKwh, Is.GreaterThan(battery));
            Assert.That(reader.Open(player, () => bootstrap.Current));
            Assert.That(reader.OpenDocument(0, Vector2.zero));
            Assert.That(reader.WasRead(reader.Document));
            Assert.That(bridge.Restore(session.Power.Capture(), stages, session.LifeSupport.Capture(),
                session.Logistics.Capture(), session.Archive.Capture(), null, null, null));
            Assert.That(reader.IsOpen || player.HasReadingFocus, Is.False);
            Assert.That(bridge.Session.Archive.Capture(), Is.EqualTo(new[] { Donki }));
            Assert.That(bridge.Session.CaptureQuestCheckpoint(), Is.EqualTo(stages));
            Assert.That(reader.Open(player, () => bootstrap.Current));
            Assert.That(reader.OpenDocument(0, Vector2.zero));
            reader.enabled = false;
            Assert.That(reader.IsOpen || player.HasReadingFocus, Is.False);
            Assert.That(bridge.Session.Archive.Acknowledge(Donki).Code, Is.EqualTo(ResultCode.Unavailable));
        }

        private static IEnumerator LoadStation()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            Object.FindAnyObjectByType<StationFirstPerson>().SendMessage("OnApplicationFocus", true);
        }

        private static void Press(StationArchiveReader reader, KeyCode key, bool shift = false)
        {
            reader.HandleKeyboard(new Event { type = EventType.KeyDown, keyCode = key,
                modifiers = shift ? EventModifiers.Shift : EventModifiers.None });
        }

        private sealed class Clock : ISessionClock
        {
            public double Seconds { get; private set; }

            public void Advance(GameplaySession session, double amount)
            {
                Seconds += amount;
                session.Tick();
                session.Dispatch();
            }
        }
    }
}
#endif
