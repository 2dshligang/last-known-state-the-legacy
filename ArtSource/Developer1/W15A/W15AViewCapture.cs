using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;

namespace LKS.World
{
    public sealed class W15AViewCapture : MonoBehaviour
    {
        private static readonly (string name, Vector3 position, Vector3 focus)[] Shots =
        {
            ("arrival", new(0, 1.62f, .95f), new(-.14f, 1.32f, 5.8f)),
            ("power", new(.87f, 1.64f, 4.90f), new(-1.11f, 1.21f, 5.89f)),
            ("echo", new(-1.03f, 1.64f, 8.76f), new(1.09f, 1.32f, 10.30f)),
            ("shelter", new(-1.02f, 1.61f, 12.70f), new(1.03f, 1.31f, 14.18f)),
            ("seedlings", new(.35f, 1.62f, 13.35f), new(-1.22f, 1.22f, 14.47f)),
            ("laboratory", new(.67f, 1.66f, 16.36f), new(-1.11f, 1.20f, 17.63f)),
            ("report", new(-.34f, 1.62f, 16.51f), new(1.14f, 1.20f, 18.03f)),
            ("exterior", new(.08f, 1.63f, 25.04f), new(.0f, 1.03f, 32.45f)),
            ("overlook", new(-3.08f, 1.62f, 25.76f), new(-4.60f, 1.42f, 25.24f))
        };

        private Camera camera;
        private (string name, Vector3 position, Vector3 focus) shot;
        private bool fixedView;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartCapture()
        {
            if (Environment.GetCommandLineArgs().Contains("-lksVisualCapture"))
                new GameObject("World visual evidence fixture").AddComponent<W15AViewCapture>();
        }

        private void LateUpdate()
        {
            if (fixedView) camera.transform.SetPositionAndRotation(shot.position,
                Quaternion.LookRotation(shot.focus - shot.position));
        }

        private IEnumerator Start()
        {
            yield return null;
            string[] args = Environment.GetCommandLineArgs();
            string folder = args[Array.IndexOf(args, "-lksVisualCapture") + 1];
            Directory.CreateDirectory(folder);
            AudioListener.volume = 0;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            var player = FindAnyObjectByType<StationFirstPerson>();
            player.SendMessage("OnApplicationFocus", true);
            if (!player.GetComponent<StationOpening>().Skip()) throw new InvalidOperationException("Opening fixture did not close.");
            camera = player.GetComponentInChildren<Camera>();
            Debug.Log($"W15A_CAPTURE_CONFIG {Screen.width}x{Screen.height} fov={camera.fieldOfView} near={camera.nearClipPlane} far={camera.farClipPlane} quality={QualitySettings.names[QualitySettings.GetQualityLevel()]} gpu={SystemInfo.graphicsDeviceName} render={SystemInfo.graphicsDeviceType}");
            using (player.AcquireReadingFocus())
            {
                fixedView = true;
                foreach (var view in Shots)
                {
                    if (Screen.width < 1200 && view.name != "echo") continue;
                    shot = view;
                    for (int i = 0; i < 18; i++) yield return null;
                    if (Screen.width >= 1200 && view.name == "arrival")
                    {
                        var times = new List<double>(160);
                        for (int i = 0; i < 160; i++)
                        {
                            double start = Time.realtimeSinceStartupAsDouble;
                            yield return null;
                            times.Add((Time.realtimeSinceStartupAsDouble - start) * 1000);
                        }
                        times.Sort();
                        Debug.Log(FormattableString.Invariant($"W15A_STATIC_FRAMES n={times.Count} p50-ms={times[80]:F3} p95-ms={times[151]:F3} allocated-bytes={Profiler.GetTotalAllocatedMemoryLong()}"));
                    }
                    yield return new WaitForEndOfFrame();
                    var png = ScreenCapture.CaptureScreenshotAsTexture();
                    File.WriteAllBytes(Path.Combine(folder, view.name + ".png"), png.EncodeToPNG());
                    Destroy(png);
                    Debug.Log($"W15A_SHOT {view.name} {Screen.width}x{Screen.height}");
                }
                fixedView = false;
            }
            Debug.Log("W15A_CAPTURE_PASS");
            Application.Quit();
        }
    }
}