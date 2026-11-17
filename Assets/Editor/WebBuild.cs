using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HockeyGame.Editor
{
    /// <summary>
    /// Web build for the Workshop, run in the open Editor with the Unity CLI:
    /// unity command --project-path &lt;project&gt; eval --code 'return HockeyGame.Editor.WebBuild.BuildTo(@"C:\path\to\HockeyWeb");'
    /// It refuses to build unless Decompression Fallback is on: the Workshop's nginx sends no
    /// Content-Encoding header, so a build without the fallback does not load.
    /// </summary>
    public static class WebBuild
    {
        /// <summary>Builds into <paramref name="outDir"/> and returns a one-line result. Does not close the Editor.</summary>
        public static string BuildTo(string outDir)
        {
            if (!PlayerSettings.WebGL.decompressionFallback)
            {
                return "WebBuild: Decompression Fallback is off. Turn it on (Player Settings > Publishing Settings).";
            }

            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = outDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };
            BuildSummary summary = BuildPipeline.BuildPlayer(options).summary;
            string line = "WebBuild: " + summary.result + ", " + summary.totalSize + " bytes, " + summary.totalErrors + " errors.";
            Debug.Log(line);
            return line;
        }

        /// <summary>Command-line entry (-executeMethod ... -webOut &lt;folder&gt;). Exits the Editor when done.</summary>
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
            string line = BuildTo(args[i + 1]);
            EditorApplication.Exit(line.StartsWith("WebBuild: Succeeded") ? 0 : 1);
        }
    }
}
