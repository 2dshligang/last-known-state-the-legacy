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
    public sealed class StationFirstPersonTests
    {
        private const string Path = "Assets/_Game/Scenes/Station.unity";

        [UnityTest]
        public IEnumerator WalkFullStationAndRespectObstacles()
        {
            yield return LoadStation();
            var player = FindPlayer();
            player.SendMessage("OnApplicationFocus", false);
            var motor = player.GetComponent<FirstPersonMotor>();
            var layout = Object.FindFirstObjectByType<StationGenerationManifest>().Layout;
            float end = layout.InteriorEnd;
            float side = layout.ExteriorWidth / 2 - 1.3f;
            float near = end + 2;
            float far = end + layout.ExteriorDepth - 1.5f;
            var stops = new[] { new Vector3(0,0,near), new Vector3(-side,0,near),
                new Vector3(-side,0,far),new Vector3(side,0,far),new Vector3(side,0,near),
                new Vector3(0,0,near),new Vector3(0,0,layout.BayLength*4.49f) };
            foreach (var stop in stops) Walk(motor, stop);
            var anchors = Object.FindObjectsByType<StationInteractionAnchor>(FindObjectsSortMode.None);
            foreach (var station in anchors.Where(anchor => anchor.TargetId.value != "quest.sampling"))
            {
                Vector3 approach = station.Approach.position;
                Walk(motor, new Vector3(0,0,approach.z));
                Walk(motor, approach);
                Walk(motor, new Vector3(0,0,approach.z));
            }
            Walk(motor, new Vector3(0,0,end+2));
            for (int index = 0; index < 70; index++) motor.Move(Vector2.right, .5f);
            Assert.That(player.transform.position.x, Is.InRange(side, layout.ExteriorWidth / 2 - .15f));
            Assert.That(player.transform.position.y, Is.EqualTo(0).Within(.08f));
        }

        [UnityTest]
        public IEnumerator StepsSlopesAndClosedDoorAtLowFrameRate()
        {
            var scene = SceneManager.CreateScene("W05 collision fixture");
            SceneManager.SetActiveScene(scene);
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Prefabs/W05/StationFirstPerson.prefab");
            Vector3 offset = Vector3.right * 100;
            var player = Object.Instantiate(prefab, offset, Quaternion.identity).GetComponent<StationFirstPerson>();
            yield return null;
            player.SendMessage("OnApplicationFocus", false);
            var motor = player.GetComponent<FirstPersonMotor>();
            motor.ResetMotion();
            MakeBox(offset + new Vector3(0,-.11f,1.65f),new Vector3(3,.22f,7));
            MakeBox(offset + new Vector3(0,.10f,1.15f),new Vector3(2,.2f,.7f));
            var ramp = MakeBox(offset + new Vector3(0,.24f,2.8f),new Vector3(2,.12f,1.6f));
            ramp.transform.rotation = Quaternion.Euler(-19,0,0);
            MakeBox(offset + new Vector3(0,.46f,3.9f),new Vector3(2,.18f,.80f));
            MakeBox(offset + new Vector3(0,1.55f,4.42f),new Vector3(2,2,.14f));
            Physics.SyncTransforms();
            motor.Move(Vector2.zero, .1f);
            for (int i = 0; i < 22; i++)
            {
                motor.Move(Vector2.up, .25f);
                yield return null;
            }
            Assert.That(player.transform.position.z, Is.InRange(3.7f,4.17f), player.transform.position.ToString("F3"));
            Assert.That(player.transform.position.y, Is.InRange(.44f,.68f), player.transform.position.ToString("F3"));
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator ModalPauseFocusAndReload()
        {
            yield return LoadStation();
            var player = FindPlayer();
            var view = player.GetComponent<StationInputOverlay>();
            var session = player.GetComponent<SessionBootstrap>();
            Assert.That(session.Restart(new EmptyCatalog(), Array.Empty<StableId>(),
                () => Array.Empty<IInteractable>()), Is.Empty);
            var before = player.transform.position;
            var eye = player.GetComponentInChildren<Camera>().transform;
            var rotation = eye.rotation;
            player.SendMessage("OnApplicationFocus", true);
            player.ResumePointer();

            int presses = 0;
            player.InteractPressed += () => presses++;
            try
            {
                view.OpenReading("TERMINAL");
                Assert.That(player.HasReadingFocus && !player.HasWorldFocus);
                var second = player.AcquireReadingFocus();
                view.CloseReading();
                Assert.That(player.HasReadingFocus, "First reader released another reader's focus.");
                second.Dispose();
                view.OpenReading("ARCHIVE");
                player.QueueInteraction();
                yield return null;
                yield return null;
                Assert.That(presses, Is.Zero);
                Assert.That(player.transform.position, Is.EqualTo(before));
                Assert.That(eye.rotation, Is.EqualTo(rotation));
                player.QueueEscape();
                player.QueueInteraction();
                yield return null;
                yield return null;
                Assert.That(player.IsPaused && player.HasReadingFocus);
                Assert.That(Time.timeScale, Is.Zero);
                Assert.That(presses, Is.Zero);
                yield return null;
                player.SetPaused(false);
                Assert.That(player.HasReadingFocus && !player.HasWorldFocus);
                player.SendMessage("OnApplicationFocus", false);
                player.SendMessage("OnApplicationFocus", true);
                view.CloseReading();
                Assert.That(player.HasWorldFocus, Is.False);
                player.ResumePointer();
                Assert.That(player.HasWorldFocus, Is.True);

                view.OpenReading("TERMINAL");
                player.SetPaused(true);
                player.RestartAtSpawn();
                Assert.That(player.IsPaused || player.HasReadingFocus, Is.False);
                Assert.That(Time.timeScale, Is.EqualTo(1));
                Assert.That(player.transform.position, Is.EqualTo(before));
                view.OpenReading("ARCHIVE");
                player.SetPaused(true);
                yield return view.ReloadStation();
                yield return null;
                var reopened = FindPlayer();
                Assert.That(player == null);
                Assert.That(reopened.IsPaused || reopened.HasReadingFocus, Is.False);
                Assert.That(Time.timeScale, Is.EqualTo(1));
                Assert.That(presses, Is.Zero);
            }
            finally
            {
                Time.timeScale = 1;
            }
        }

        private static IEnumerator LoadStation()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(Path, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
        }

        private static StationFirstPerson FindPlayer() =>
            SceneManager.GetSceneByPath(Path).GetRootGameObjects().Select(root => root.GetComponent<StationFirstPerson>())
                .Single(player => player != null);

        private static void Walk(FirstPersonMotor motor, Vector3 target)
        {
            for (int index = 0; index < 1200; index++)
            {
                Vector3 difference = target - motor.transform.position;
                difference.y = 0;
                if (difference.magnitude < .025f) return;
                var local = motor.transform.InverseTransformDirection(difference);
                motor.Move(Vector2.ClampMagnitude(new Vector2(local.x,local.z)/.272f,1), .08f);
            }
            Assert.Fail($"Player could not reach {target:F2} from {motor.transform.position:F2}.");
        }

        private static GameObject MakeBox(Vector3 position, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.transform.SetPositionAndRotation(position, Quaternion.identity);
            box.transform.localScale = size;
            return box;
        }

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
#endif
