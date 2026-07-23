#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RaccoonHeist.World.Editor
{
    public static class ItchWebGLBuilder
    {
        private const string OutputPath = "Builds/Itch-WebGL";

        [MenuItem("Raccoon Heist/Build/Build Itch.io WebGL")]
        public static void Build()
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                throw new InvalidOperationException("No enabled scenes are configured in Build Profiles.");
            }

            if (!scenes.Contains("Assets/Scenes/SampleScene.unity"))
            {
                throw new InvalidOperationException("SampleScene must be enabled before building Raccoon Heist.");
            }

            Directory.CreateDirectory(OutputPath);

            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.dataCaching = true;

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Itch.io WebGL build failed: {summary.result} " +
                    $"({summary.totalErrors} errors, {summary.totalWarnings} warnings).");
            }

            Debug.Log(
                $"Raccoon Heist: itch.io WebGL build completed at {Path.GetFullPath(OutputPath)} " +
                $"({summary.totalSize / (1024f * 1024f):F1} MB).");
        }
    }
}
#endif
