using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LKS.Editor.World
{
    public static class W18ProfileBuild
    {
        public static void Build()
        {
            string path = Environment.GetEnvironmentVariable("LKS_W18_PLAYER");
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
                throw new InvalidOperationException("Set an absolute W18 profile build path.");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            bool timing = PlayerSettings.enableFrameTimingStats;
            bool development = Environment.GetEnvironmentVariable("LKS_W18_DEVELOPMENT") == "1";
            try
            {
                PlayerSettings.enableFrameTimingStats = true;
                var report = BuildPipeline.BuildPlayer(new[] { W04StationGenerator.ScenePath }, path,
                    BuildTarget.StandaloneWindows64, development ? BuildOptions.Development : BuildOptions.None);
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Profile build failed: " + report.summary.result);
                Debug.Log("W18_BUILD_PASS " + path);
            }
            finally
            {
                PlayerSettings.enableFrameTimingStats = timing;
            }
        }
    }
}
