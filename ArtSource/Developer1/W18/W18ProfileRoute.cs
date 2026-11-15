using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace LKS.World
{
    public sealed class W18ProfileRoute : MonoBehaviour
    {
        private const int Frames = 240;
        private readonly List<Sample> samples = new(8000);
        private readonly FrameTiming[] timings = new FrameTiming[1];
        private readonly WaitForEndOfFrame endOfFrame = new();
        private ProfilerRecorder draws, batches, setPass, triangles, shaderCreate;
        private StationFirstPerson player;
        private Transform eye;
        private Vector3 position, direction;
        private bool drive;
        private ulong lastTimestamp;
        private double previousTime;
        private string output;

        // Positions are first-person floor poses on the accepted indoor and two-leg exterior route.
        private static readonly (string Name, Vector3 Start, Vector3 End, Vector3 LookFrom, Vector3 LookTo)[] Segments =
        {
            ("arrival", new(0,0,1), new(0,0,3.8f), new(0,-.08f,1), new(0,0,1)),
            ("power", new(0,0,3.8f), new(0,0,7.7f), new(-.70f,-.12f,1), new(-1,-.14f,.28f)),
            ("archive", new(0,0,7.7f), new(0,0,11.7f), new(.6f,-.08f,1), new(1,-.14f,.28f)),
            ("shelter", new(0,0,11.7f), new(0,0,15.7f), new(.62f,-.08f,1), new(-.82f,-.12f,.5f)),
            ("laboratory", new(0,0,15.7f), new(0,0,19.9f), new(-.6f,-.12f,1), new(.72f,-.1f,.6f)),
            ("airlock", new(0,0,19.9f), new(0,0,26), new(0,-.05f,1), new(0,-.05f,1)),
            ("exterior-near", new(0,0,26), new(-4.2f,0,26), new(0,-.08f,1), new(.45f,-.08f,1)),
            ("exterior-west", new(-4.2f,0,26), new(-4.2f,0,34.5f), new(.5f,-.14f,1), new(.81f,-.13f,.39f)),
            ("exterior-far", new(-4.2f,0,34.5f), new(4.2f,0,34.5f), new(.76f,-.13f,-.5f), new(0,-.10f,-1)),
            ("exterior-east", new(4.2f,0,34.5f), new(4.2f,0,26), new(-.60f,-.08f,-1), new(-.35f,-.1f,-1)),
            ("airlock-return", new(4.2f,0,26), new(0,0,22), new(-1,-.08f,0), new(0,-.08f,-1)),
        };

        private static readonly (string Name, Vector3 Position, Vector3 Focus)[] Visuals =
        {
            ("arrival", new(0,1.62f,.95f), new(-.14f,1.32f,5.8f)),
            ("echo", new(-1.03f,1.64f,8.76f), new(1.09f,1.32f,10.30f)),
            ("shelter", new(-1.02f,1.61f,12.70f), new(1.03f,1.31f,14.18f)),
            ("exterior", new(.08f,1.63f,25.04f), new(0,1.03f,32.45f)),
            ("overlook", new(-3.08f,1.62f,25.76f), new(-4.6f,1.42f,25.24f))
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Environment.GetCommandLineArgs().Contains("-lksW18Profile"))
                new GameObject("W18 profile route").AddComponent<W18ProfileRoute>();
        }

        private void LateUpdate()
        {
            if (!drive) return;
            player.transform.position = position;
            eye.localPosition = Vector3.up * 1.62f;
            eye.rotation = Quaternion.LookRotation(direction);
        }

        private IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            output = args[Array.IndexOf(args, "-lksW18Profile") + 1];
            Directory.CreateDirectory(output);
            AudioListener.volume = 0;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            bool shadowControl = args.Contains("-lksW18ShadowControl");
            bool photoEvidence = !args.Contains("-lksW18NoShots");
            bool previousText = StationUi.LargeText;
            StationUi.LargeText = false;
            yield return null;

            var station = FindAnyObjectByType<StationInteraction>();
            player = station.GetComponent<StationFirstPerson>();
            eye = player.GetComponentInChildren<Camera>().transform;
            var opening = player.GetComponent<StationOpening>();
            var echo = player.GetComponent<StationEchoReview>();
            var light = FindObjectsByType<Light>().Single(candidate => candidate.type == LightType.Directional);
            if (shadowControl) light.shadows = LightShadows.None;
            player.SendMessage("OnApplicationFocus", true);
            if (Screen.width != 1920 || Screen.height != 1080 || !FrameTimingManager.IsFeatureEnabled())
                throw new InvalidOperationException("W18 requires a 1920x1080 graphics player with frame timing enabled.");

            draws = Counter("Draw Calls Count");
            batches = Counter("Batches Count");
            setPass = Counter("SetPass Calls Count");
            triangles = Counter("Triangles Count");
            shaderCreate = Counter("Shader.CreateGPUProgram");
            Debug.Log($"W18_CONFIG {Screen.width}x{Screen.height} gpu={SystemInfo.graphicsDeviceName} " +
                $"api={SystemInfo.graphicsDeviceType} cpu={SystemInfo.processorType} ramMB={SystemInfo.systemMemorySize} " +
                $"vramMB={SystemInfo.graphicsMemorySize} quality={QualitySettings.names[QualitySettings.GetQualityLevel()]} " +
                $"development={Debug.isDebugBuild} shadows={light.shadows} frameTiming={FrameTimingManager.IsFeatureEnabled()} " +
                $"gpuFrequency={FrameTimingManager.GetGpuTimerFrequency()} draws={draws.Valid} batches={batches.Valid} " +
                $"setPass={setPass.Valid} triangles={triangles.Valid} shaderMarker={shaderCreate.Valid}");

            try
            {
                yield return Segment("opening", "startup", 160);
                if (!opening.Skip() || !station.Session.Power.Commit(new[] { StationPower.Air, StationPower.Water }).Succeeded)
                    throw new InvalidOperationException("Cannot enter the powered station.");
                player.ResumePointer();
                drive = true;
                foreach (var lap in new[] { "first", "repeat" })
                {
                    foreach (var segment in Segments)
                    {
                        position = segment.Start;
                        direction = segment.LookFrom;
                        for (int index = 0; index < Frames; index++)
                        {
                            float t = index / (Frames - 1f);
                            position = Vector3.Lerp(segment.Start, segment.End, t);
                            if (segment.Name == "airlock-return" && t < .65f)
                                position = Vector3.Lerp(segment.Start, new Vector3(0,0,26), t / .65f);
                            else if (segment.Name == "airlock-return")
                                position = Vector3.Lerp(new Vector3(0,0,26), segment.End, (t - .65f) / .35f);
                            direction = Vector3.Slerp(segment.LookFrom.normalized, segment.LookTo.normalized, t);
                            yield return endOfFrame;
                            Record(lap, segment.Name, index);
                        }
                        Memory(lap, segment.Name);
                    }
                    position = new Vector3(-.7f,0,9.3f);
                    direction = new Vector3(1,-.12f,.4f);
                    yield return null;
                    if (!echo.Open(player, () => station.Session) || !echo.Scrub(4m))
                        throw new InvalidOperationException("ECHO projection is unavailable.");
                    yield return Segment(lap, "echo-panel", Frames);
                    echo.Close();
                }

                if (photoEvidence)
                {
                    position = new Vector3(-.7f,0,9.3f);
                    direction = new Vector3(1,-.12f,.4f);
                    yield return null;
                    if (!echo.Open(player, () => station.Session))
                        throw new InvalidOperationException("Cannot open ECHO visual QA.");
                    foreach (var cue in new[] { (0m, "echo-recorded"), (4m, "echo-model"), (8m, "echo-missing") })
                    {
                        if (!echo.Scrub(cue.Item1)) throw new InvalidOperationException("Cannot select ECHO visual QA cue.");
                        for (int i = 0; i < 15; i++) yield return null;
                        yield return endOfFrame;
                        Capture(cue.Item2);
                    }
                    echo.Close();
                    foreach (var shot in Visuals)
                    {
                        position = shot.Position - Vector3.up * 1.62f;
                        direction = shot.Focus - shot.Position;
                        for (int i = 0; i < 15; i++) yield return null;
                        yield return endOfFrame;
                        Capture(shot.Name);
                    }
                }
                Export();
                Debug.Log($"W18_PROFILE_PASS {samples.Count} samples");
                Application.Quit();
            }
            finally
            {
                StationUi.LargeText = previousText;
                draws.Dispose();
                batches.Dispose();
                setPass.Dispose();
                triangles.Dispose();
                shaderCreate.Dispose();
            }
        }

        private IEnumerator Segment(string lap, string stage, int count)
        {
            for (int index = 0; index < count; index++)
            {
                yield return endOfFrame;
                Record(lap, stage, index);
            }
            Memory(lap, stage);
        }

        private void Record(string lap, string stage, int index)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            FrameTimingManager.CaptureFrameTimings();
            FrameTiming timing = default;
            if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].frameStartTimestamp != lastTimestamp)
            {
                timing = timings[0];
                lastTimestamp = timing.frameStartTimestamp;
            }
            samples.Add(new Sample
            {
                Lap = lap, Stage = stage, Index = index, Frame = Time.frameCount,
                Wall = previousTime == 0 ? 0 : (now - previousTime) * 1000,
                Cpu = timing.cpuFrameTime, Main = timing.cpuMainThreadFrameTime,
                Render = timing.cpuRenderThreadFrameTime, Gpu = timing.gpuFrameTime,
                Draws = Value(draws), Batches = Value(batches), SetPass = Value(setPass),
                Triangles = Value(triangles), Shader = Value(shaderCreate)
            });
            previousTime = now;
        }

        private void Memory(string lap, string stage)
        {
            using var process = System.Diagnostics.Process.GetCurrentProcess();
            Debug.Log(FormattableString.Invariant($"W18_MEMORY {lap},{stage},{Profiler.GetTotalAllocatedMemoryLong()},{Profiler.GetTotalReservedMemoryLong()},{Texture.totalTextureMemory},{process.WorkingSet64}"));
        }

        private void Capture(string name)
        {
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
            Destroy(image);
        }

        private void Export()
        {
            using var writer = new StreamWriter(Path.Combine(output, "frames.csv"));
            writer.WriteLine("lap,region,index,frame,wall_ms,cpu_ms,main_ms,render_ms,gpu_ms,draws,batches,setpass,triangles,shader_ns");
            foreach (var row in samples)
                writer.WriteLine(FormattableString.Invariant(
                    $"{row.Lap},{row.Stage},{row.Index},{row.Frame},{row.Wall:F4},{row.Cpu:F4},{row.Main:F4},{row.Render:F4},{row.Gpu:F4},{row.Draws},{row.Batches},{row.SetPass},{row.Triangles},{row.Shader}"));
        }

        private static ProfilerRecorder Counter(string name)
        {
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            foreach (var handle in handles)
            {
                var description = ProfilerRecorderHandle.GetDescription(handle);
                if (description.Name == name)
                    return ProfilerRecorder.StartNew(description.Category, name, 1);
            }
            return default;
        }

        private static long Value(ProfilerRecorder counter) => counter.Valid ? counter.LastValue : -1;

        private struct Sample
        {
            public string Lap, Stage;
            public int Index, Frame;
            public double Wall, Cpu, Main, Render, Gpu;
            public long Draws, Batches, SetPass, Triangles, Shader;
        }
    }
}
