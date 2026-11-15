using System;
using System.Linq;
using LKS.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LKS.Editor.World
{
    public static class W16StationLighting
    {
        private const string SkyPath = "Assets/_Game/Art/W16/StationDustSky.mat";
        private static readonly Color AmbientSky = new(.35f, .39f, .43f);
        private static readonly Color AmbientEquator = new(.24f, .27f, .29f);
        private static readonly Color AmbientGround = new(.15f, .13f, .12f);
        private static readonly Color Dust = new(.49f, .37f, .31f);
        private const float DustDensity = .013f;

        [MenuItem("LKS/World/W16/Apply Station Lighting")]
        public static void Apply() => W04StationGenerator.Generate();

        internal static bool ApplyAtmosphere(Scene station, Light sun)
        {
            var previous = SceneManager.GetActiveScene();
            if (station != previous) SceneManager.SetActiveScene(station);
            try
            {
                var sky = LoadSky();
                bool changed = false;
                changed |= Assign(RenderSettings.skybox, sky, value => RenderSettings.skybox = value);
                changed |= Assign(RenderSettings.sun, sun, value => RenderSettings.sun = value);
                changed |= Assign(RenderSettings.ambientMode, AmbientMode.Trilight, value => RenderSettings.ambientMode = value);
                changed |= Assign(RenderSettings.ambientSkyColor, AmbientSky, value => RenderSettings.ambientSkyColor = value);
                changed |= Assign(RenderSettings.ambientEquatorColor, AmbientEquator, value => RenderSettings.ambientEquatorColor = value);
                changed |= Assign(RenderSettings.ambientGroundColor, AmbientGround, value => RenderSettings.ambientGroundColor = value);
                changed |= Assign(RenderSettings.ambientIntensity, 1f, value => RenderSettings.ambientIntensity = value);
                changed |= Assign(RenderSettings.fog, true, value => RenderSettings.fog = value);
                changed |= Assign(RenderSettings.fogMode, FogMode.ExponentialSquared, value => RenderSettings.fogMode = value);
                changed |= Assign(RenderSettings.fogColor, Dust, value => RenderSettings.fogColor = value);
                changed |= Assign(RenderSettings.fogDensity, DustDensity, value => RenderSettings.fogDensity = value);

                var player = PlayerCamera(station);
                if (player != null && player.clearFlags != CameraClearFlags.Skybox)
                {
                    player.clearFlags = CameraClearFlags.Skybox;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(player);
                    changed = true;
                }
                return changed;
            }
            finally
            {
                if (previous != station && previous.IsValid() && previous.isLoaded)
                    SceneManager.SetActiveScene(previous);
            }
        }

        [MenuItem("LKS/World/W16/Verify Station Lighting")]
        public static void Verify()
        {
            bool opened = SceneManager.GetActiveScene().path != W04StationGenerator.ScenePath;
            if (opened && !Application.isBatchMode)
                throw new InvalidOperationException("Open Station before verifying W16.");
            var scene = opened
                ? EditorSceneManager.OpenScene(W04StationGenerator.ScenePath, OpenSceneMode.Single)
                : SceneManager.GetActiveScene();
            var manifest = W04StationGenerator.FindManifest(scene) ??
                throw new InvalidOperationException("The station manifest is missing.");
            var lights = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Light>()).ToArray();
            var sun = manifest.Entries.Single(entry => entry.id.value == "exterior.sun").node.GetComponent<Light>();
            var sky = LoadSky();
            if (RenderSettings.skybox != sky || !sky.shader.isSupported || RenderSettings.sun != sun ||
                RenderSettings.ambientMode != AmbientMode.Trilight || !RenderSettings.fog ||
                RenderSettings.fogMode != FogMode.ExponentialSquared ||
                !Mathf.Approximately(RenderSettings.fogDensity, DustDensity) ||
                PlayerCamera(scene)?.clearFlags != CameraClearFlags.Skybox)
                throw new InvalidOperationException("The station sky, ambient light, fog or player camera is not configured.");

            var diffuser = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/W15A/Materials/Diffuser.mat");
            if (diffuser == null || !diffuser.IsKeywordEnabled("_EMISSION"))
                throw new InvalidOperationException("The W16 ceiling diffuser emission is missing.");

            Vector4 skyDirection = sky.GetVector("_SunDirection");
            if (lights.Length != 7 || lights.Count(light => light.type == LightType.Spot && light.shadows == LightShadows.None) != 6 ||
                sun.type != LightType.Directional || sun.shadows != LightShadows.Soft ||
                Vector3.Dot((-sun.transform.forward).normalized, ((Vector3)skyDirection).normalized) < .999f)
                throw new InvalidOperationException("Unexpected W16 light count, shadows or sun direction.");
            Debug.Log("W16_VERIFY_PASS Six shadowless practicals, one shadowed sun, player sky and restrained fog.");
        }

        private static Camera PlayerCamera(Scene station) => station.GetRootGameObjects()
            .Select(root => root.GetComponent<StationFirstPerson>())
            .SingleOrDefault(player => player != null)?.GetComponentInChildren<Camera>();

        private static Material LoadSky() => AssetDatabase.LoadAssetAtPath<Material>(SkyPath) ??
            throw new InvalidOperationException("Missing W16 sky material: " + SkyPath);

        private static bool Assign<T>(T current, T target, Action<T> setter)
        {
            if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(current, target)) return false;
            setter(target);
            return true;
        }
    }
}
