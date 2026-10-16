using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ColaCola.Editor
{
    public static class ColaNormalFrameBuildTools
    {
        public static void BuildAndRun()
        {
            try
            {
                string root = Path.GetFullPath("output/shake-animation/normal-render-player");
                Directory.CreateDirectory(root);
                string executable = Path.Combine(root, "COLA_COLA_FrameProbe.exe");
                var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { MainSceneTools.ScenePath }, locationPathName = executable,
                    target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
                });
                if (result.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Normal-frame player build failed: " + result.summary.result);
                string capture = Path.GetFullPath("output/shake-animation/normal-render-baseline");
                Directory.CreateDirectory(capture);
                var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = "-batchmode -screen-width 563 -screen-height 460 -screen-fullscreen 0 -cola-frame-probe \"" + capture + "\" -logFile \"" + Path.Combine(capture, "Player.log") + "\"",
                    WorkingDirectory = Path.GetFullPath("."), UseShellExecute = false, CreateNoWindow = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                });
                Debug.Log("COLA_NORMAL_PLAYER_STARTED " + process.Id);
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(2);
            }
        }
    }
}
