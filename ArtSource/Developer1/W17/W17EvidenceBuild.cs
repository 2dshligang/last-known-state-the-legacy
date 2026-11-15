using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LKS.Editor.World
{
    public static class W17EvidenceBuild
    {
        public static void Build()
        {
            string output = Environment.GetEnvironmentVariable("LKS_W17_PLAYER");
            if (string.IsNullOrEmpty(output) || !Path.IsPathRooted(output)) throw new InvalidOperationException("Set an evidence build path.");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new[] { W04StationGenerator.ScenePath }, output,
                BuildTarget.StandaloneWindows64, BuildOptions.Development);
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException(report.summary.result.ToString());
            Debug.Log("W17_EVIDENCE_BUILD_PASS " + output);
        }
    }
}
