#if UNITY_EDITOR
using System.Collections;
using LKS.World;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace LKS.Editor.World
{
    public sealed class StationOpeningTests
    {
        [UnityTest]
        public IEnumerator NewGameAndRestartCanReadOrSkipToTheSameStationState()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_Game/Scenes/Station.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var player = Object.FindAnyObjectByType<StationFirstPerson>();
            player.SendMessage("OnApplicationFocus", true);
            var bridge = player.GetComponent<StationInteraction>();
            var opening = player.GetComponent<StationOpening>();
            var camera = player.GetComponentInChildren<Camera>();
            var spawn = player.transform.position;
            var lens = camera.fieldOfView;
            var cameraPose = camera.transform.localRotation;
            var photograph = GameObject.Find("Dmitry's photograph");
            Assert.That(opening.Photograph, Is.Not.Null);
            Assert.That(photograph.GetComponent<Renderer>().sharedMaterial.mainTexture, Is.SameAs(opening.Photograph));

            for (int attempt = 0; attempt < 4; attempt++)
            {
                if (attempt == 2)
                {
                    // A fresh scene also exposes skip through the initial New Game opening.
                    yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_Game/Scenes/Station.unity",
                        new LoadSceneParameters(LoadSceneMode.Single));
                    yield return null;
                    player = Object.FindAnyObjectByType<StationFirstPerson>();
                    player.SendMessage("OnApplicationFocus", true);
                    bridge = player.GetComponent<StationInteraction>();
                    opening = player.GetComponent<StationOpening>();
                    camera = player.GetComponentInChildren<Camera>();
                    photograph = GameObject.Find("Dmitry's photograph");
                }
                else if (attempt > 0) player.SendMessage("RequestRestart");
                var session = bridge.Session;
                Assert.That(opening.IsOpen && player.HasReadingFocus);
                Assert.That(player.HasWorldFocus, Is.False);
                Assert.That(opening.Passage, Does.Contain("January 20, 2059").And.Contain("Alexei is eight"));
                Assert.That(bridge.Submit(StationInteractionTests.AimAt(
                    StationInteractionTests.Anchor(new StableId("quest.arrival")))), Is.False);

                if (attempt == 1 || attempt == 2)
                {
                    Assert.That(opening.Skip());
                }
                else
                {
                    Assert.That(opening.Continue());
                    Assert.That(session.Narrative.CaptureOpening(), Is.EqualTo(OpeningProgress.Arrival));
                    Assert.That(opening.Passage, Does.Contain("November 14, 2089").And.Contain("Alexei is 39"));
                    player.SetPaused(true);
                    Assert.That(opening.Continue(), Is.False);
                    player.SetPaused(false);
                    Assert.That(opening.Continue());
                }
                Assert.That(session.Narrative.CaptureOpening(), Is.EqualTo(OpeningProgress.Complete));
                Assert.That(session.CaptureQuestCheckpoint(), Is.Empty);
                Assert.That(session.Quests.Snapshot[new StableId("quest.arrival")], Is.EqualTo(QuestStatus.Available));
                Assert.That(opening.IsOpen || player.HasReadingFocus, Is.False);
                Assert.That(opening.Passage, Is.Null);
                Assert.That(player.HasWorldFocus);
                Assert.That(player.transform.position, Is.EqualTo(spawn));
                Assert.That(camera.fieldOfView, Is.EqualTo(lens));
                Assert.That(camera.transform.localRotation, Is.EqualTo(cameraPose));
                Assert.That(GameObject.Find("Dmitry's photograph"), Is.SameAs(photograph));
            }

            Assert.That(bridge.Submit(StationInteractionTests.AimAt(
                StationInteractionTests.Anchor(new StableId("quest.arrival")))));
        }
    }
}
#endif
