#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class AndroidBuild
{
    private const string ApplicationId = "com.vectoarena.game";
    private const string DevelopmentOutput = "Build/Android/VectoArena-dev.apk";
    private const string ReleaseOutput = "Build/Android/VectoArena-release.aab";
    private static readonly string[] BundledAndroidDependencyPaths =
    {
        "Assets/Plugins/Android/androidx.annotation.annotation-1.1.0.jar",
        "Assets/Plugins/Android/androidx.arch.core.core-common-2.0.0.jar",
        "Assets/Plugins/Android/androidx.browser.browser-1.4.0.aar",
        "Assets/Plugins/Android/androidx.collection.collection-1.1.0.jar",
        "Assets/Plugins/Android/androidx.concurrent.concurrent-futures-1.0.0.jar",
        "Assets/Plugins/Android/androidx.core.core-1.1.0.aar",
        "Assets/Plugins/Android/androidx.interpolator.interpolator-1.0.0.aar",
        "Assets/Plugins/Android/androidx.lifecycle.lifecycle-common-2.0.0.jar",
        "Assets/Plugins/Android/androidx.lifecycle.lifecycle-runtime-2.0.0.aar",
        "Assets/Plugins/Android/androidx.versionedparcelable.versionedparcelable-1.1.0.aar",
        "Assets/Plugins/Android/com.google.guava.listenablefuture-1.0.jar"
    };

    [MenuItem("VectoArena/Build/Android Development APK")]
    public static void BuildDevelopmentApk()
    {
        ConfigureAndroidPlayer(InsecureHttpOption.DevelopmentOnly);
        EditorUserBuildSettings.buildAppBundle = false;
        Build(DevelopmentOutput, BuildOptions.Development | BuildOptions.AllowDebugging);
    }

    [MenuItem("VectoArena/Build/Android Development APK (Build & Run)")]
    public static void BuildAndRunDevelopmentApk()
    {
        ConfigureAndroidPlayer(InsecureHttpOption.DevelopmentOnly);
        EditorUserBuildSettings.buildAppBundle = false;
        Build(
            DevelopmentOutput,
            BuildOptions.Development | BuildOptions.AllowDebugging | BuildOptions.AutoRunPlayer
        );
    }

    [MenuItem("VectoArena/Build/Android Release AAB")]
    public static void BuildReleaseAab()
    {
        string keystorePath = Environment.GetEnvironmentVariable("VECTO_ANDROID_KEYSTORE_PATH");
        string keystorePassword = Environment.GetEnvironmentVariable("VECTO_ANDROID_KEYSTORE_PASSWORD");
        string alias = Environment.GetEnvironmentVariable("VECTO_ANDROID_KEY_ALIAS");
        string aliasPassword = Environment.GetEnvironmentVariable("VECTO_ANDROID_KEY_PASSWORD");

        if (string.IsNullOrWhiteSpace(keystorePath) || !File.Exists(keystorePath) ||
            string.IsNullOrWhiteSpace(keystorePassword) || string.IsNullOrWhiteSpace(alias) ||
            string.IsNullOrWhiteSpace(aliasPassword))
        {
            throw new BuildFailedException(
                "Release signing is not configured. Set VECTO_ANDROID_KEYSTORE_PATH, " +
                "VECTO_ANDROID_KEYSTORE_PASSWORD, VECTO_ANDROID_KEY_ALIAS and VECTO_ANDROID_KEY_PASSWORD.");
        }

        ConfigureAndroidPlayer(InsecureHttpOption.NotAllowed);
        bool previousUseCustomKeystore = PlayerSettings.Android.useCustomKeystore;
        string previousKeystoreName = PlayerSettings.Android.keystoreName;
        string previousAliasName = PlayerSettings.Android.keyaliasName;

        try
        {
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = Path.GetFullPath(keystorePath);
            PlayerSettings.Android.keystorePass = keystorePassword;
            PlayerSettings.Android.keyaliasName = alias;
            PlayerSettings.Android.keyaliasPass = aliasPassword;
            EditorUserBuildSettings.buildAppBundle = true;
            Build(ReleaseOutput, BuildOptions.CleanBuildCache);
        }
        finally
        {
            PlayerSettings.Android.keystorePass = string.Empty;
            PlayerSettings.Android.keyaliasPass = string.Empty;
            PlayerSettings.Android.useCustomKeystore = previousUseCustomKeystore;
            PlayerSettings.Android.keystoreName = previousKeystoreName;
            PlayerSettings.Android.keyaliasName = previousAliasName;
        }
    }

    private static void ConfigureAndroidPlayer(InsecureHttpOption insecureHttpOption)
    {
        DisableBundledAndroidDependencies();
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationId);
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.insecureHttpOption = insecureHttpOption;
    }

    private static void DisableBundledAndroidDependencies()
    {
        foreach (string assetPath in BundledAndroidDependencyPaths)
        {
            if (AssetImporter.GetAtPath(assetPath) is not PluginImporter importer ||
                !importer.GetCompatibleWithPlatform(BuildTarget.Android))
            {
                continue;
            }

            importer.SetCompatibleWithPlatform(BuildTarget.Android, false);
            importer.SaveAndReimport();
        }
    }

    private static void Build(string outputPath, BuildOptions options)
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            throw new BuildFailedException("No enabled scenes were found in Build Settings.");
        }

        string fullOutputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath) ?? "Build");
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = fullOutputPath,
            target = BuildTarget.Android,
            options = options
        });

        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new BuildFailedException($"Android build failed: {report.summary.result}");
        }

        Debug.Log($"Android build completed: {fullOutputPath} ({report.summary.totalSize} bytes)");
    }
}
#endif
