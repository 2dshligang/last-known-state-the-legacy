using System;
using System.Linq;
using UnityEngine.SceneManagement;

namespace LKS.World
{
    /// <summary>The saved scene and root binding used by the Windows player and route tests.</summary>
    public static class StationSceneEntry
    {
        public const string Name = "Station";
        public const string Path = "Assets/_Game/Scenes/Station.unity";

        public static StationInteraction Resolve(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                throw new ArgumentException("Load the station scene before resolving its entry.", nameof(scene));

            var entries = scene.GetRootGameObjects()
                .Select(root => root.GetComponent<StationInteraction>())
                .Where(entry => entry != null)
                .ToArray();
            if (entries.Length != 1 || !entries[0].isActiveAndEnabled)
                throw new InvalidOperationException("Station requires exactly one active interaction entry on a scene root.");

            return entries[0];
        }
    }
}
