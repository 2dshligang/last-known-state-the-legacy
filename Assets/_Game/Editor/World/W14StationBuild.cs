using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LKS.Editor.World
{
    public static class W14StationBuild
    {
        [MenuItem("LKS/World/Build Station for Windows")]
        public static void BuildWindows()
        {
            const string fallback = "Builds/Station/LastKnownState.exe";
            string output = Application.isBatchMode
                ? Environment.GetEnvironmentVariable("LKS_STATION_BUILD_PATH") ?? fallback
                : EditorUtility.SaveFilePanel("Build Station for Windows", "", "LastKnownState", "exe");
            if (string.IsNullOrWhiteSpace(output)) return;

            output = Path.GetFullPath(output);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { W04StationGenerator.ScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Windows station build failed: {report.summary.result}.");
            Debug.Log($"W14_STATION_BUILD_PASS {output}");
        }
    }
}
