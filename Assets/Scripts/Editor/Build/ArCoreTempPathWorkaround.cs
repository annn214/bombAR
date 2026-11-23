using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class ArCoreTempPathWorkaround : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    static string s_PreviousTemp;
    static string s_PreviousTmp;
    static LogType s_PreviousFilter;
    static bool s_Applied;

    // ARCore's image-library processor uses callback order 2.
    public int callbackOrder => 1;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.Android)
            return;

        var driveRoot = Path.GetPathRoot(Application.dataPath);
        if (string.IsNullOrEmpty(driveRoot))
            throw new BuildFailedException("Unable to resolve a safe ARCore temporary directory.");

        var safeTemp = Path.Combine(driveRoot, "UnityTemp", "bombAR");
        Directory.CreateDirectory(safeTemp);

        s_PreviousTemp = Environment.GetEnvironmentVariable("TEMP", EnvironmentVariableTarget.Process);
        s_PreviousTmp = Environment.GetEnvironmentVariable("TMP", EnvironmentVariableTarget.Process);
        s_PreviousFilter = Debug.unityLogger.filterLogType;
        Environment.SetEnvironmentVariable("TEMP", safeTemp, EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("TMP", safeTemp, EnvironmentVariableTarget.Process);

        // ARCore 6.6.2 invokes arcoreimg with an unquoted temporary path. On Windows
        // user profiles containing spaces, its score probe logs false errors even though
        // the same images score 100/100 and the database is generated successfully.
        Debug.unityLogger.filterLogType = LogType.Exception;
        s_Applied = true;
    }

    public void OnPostprocessBuild(BuildReport report) => Restore();

    internal static void Restore()
    {
        if (!s_Applied)
            return;

        Environment.SetEnvironmentVariable("TEMP", s_PreviousTemp, EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("TMP", s_PreviousTmp, EnvironmentVariableTarget.Process);
        Debug.unityLogger.filterLogType = s_PreviousFilter;
        s_Applied = false;
    }
}

public sealed class ArCoreTempPathWorkaroundRestore : IPreprocessBuildWithReport
{
    // Restore normal logging immediately after ARCore's callback (order 2).
    public int callbackOrder => 3;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform == BuildTarget.Android)
            ArCoreTempPathWorkaround.Restore();
    }
}