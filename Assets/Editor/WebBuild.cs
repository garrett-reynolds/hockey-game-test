using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HockeyGame.Editor
{
    /// <summary>
    /// Command-line Web build for the Workshop. Run with:
    /// Unity -batchmode -quit -projectPath . -executeMethod HockeyGame.Editor.WebBuild.Build -webOut "C:\path\to\HockeyWeb"
    /// It stops with a non-zero exit code unless Decompression Fallback is on: the Workshop's nginx sends no
    /// Content-Encoding header, so a build without the fallback does not load.
    /// </summary>
    public static class WebBuild
    {
        public static void Build()
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-webOut");
            if (i < 0 || i + 1 >= args.Length)
            {
                Debug.LogError("WebBuild: pass -webOut <folder>.");
                EditorApplication.Exit(2);
                return;
            }
            if (!PlayerSettings.WebGL.decompressionFallback)
            {
                Debug.LogError("WebBuild: Decompression Fallback is off. Turn it on (Player Settings > Publishing Settings).");
                EditorApplication.Exit(3);
                return;
            }

            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = args[i + 1],
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };
            BuildSummary summary = BuildPipeline.BuildPlayer(options).summary;
            Debug.Log("WebBuild: " + summary.result + ", " + summary.totalSize + " bytes, " + summary.totalErrors + " errors.");
            EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
