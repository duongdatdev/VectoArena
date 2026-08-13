#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class VectoWebGLBuildWindow : EditorWindow
{
    [MenuItem("VectoArena/Build/WebGL Build Tool", priority = 100)]
    public static void Open()
    {
        GetWindow<VectoWebGLBuildWindow>("VectoArena WebGL");
    }

    private void OnGUI()
    {
        GUILayout.Space(8);
        EditorGUILayout.LabelField("VectoArena WebGL Builder", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Output: " + VectoWebGLBuild.OutputPath +
            "\nRelease builds use Brotli with decompression fallback, so they can be hosted on most static web servers.",
            MessageType.Info);

        DrawStatus();

        GUILayout.Space(8);
        if (GUILayout.Button("Prepare WebGL Settings", GUILayout.Height(28)))
        {
            VectoWebGLBuild.PrepareWebGL();
        }

        GUILayout.Space(6);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer))
        {
            if (GUILayout.Button("Build Development", GUILayout.Height(30)))
            {
                VectoWebGLBuild.BuildDevelopment();
            }

            if (GUILayout.Button("Build Development & Run", GUILayout.Height(30)))
            {
                VectoWebGLBuild.BuildAndRunDevelopment();
            }

            GUILayout.Space(6);
            if (GUILayout.Button("Build Release", GUILayout.Height(30)))
            {
                VectoWebGLBuild.BuildRelease();
            }

            if (GUILayout.Button("Clean Build Release", GUILayout.Height(30)))
            {
                VectoWebGLBuild.BuildCleanRelease();
            }
        }

        GUILayout.Space(8);
        using (new EditorGUI.DisabledScope(!Directory.Exists(VectoWebGLBuild.FullOutputPath)))
        {
            if (GUILayout.Button("Open Output Folder"))
            {
                EditorUtility.RevealInFinder(VectoWebGLBuild.FullOutputPath);
            }
        }

        EditorGUILayout.HelpBox(
            "CI: Unity.exe -batchmode -quit -projectPath <path> -executeMethod VectoWebGLBuild.BuildRelease",
            MessageType.None);
    }

    private static void DrawStatus()
    {
        bool targetInstalled = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL);
        bool defineReady = VectoWebGLBuild.HasRequiredDefine();
        string[] scenes = VectoWebGLBuild.GetEnabledScenes();

        EditorGUILayout.LabelField("Active target", EditorUserBuildSettings.activeBuildTarget.ToString());
        EditorGUILayout.LabelField("WebGL module", targetInstalled ? "Installed" : "Missing");
        EditorGUILayout.LabelField("THIRDWEB_REOWN", defineReady ? "Enabled" : "Missing");
        EditorGUILayout.LabelField("Enabled scenes", scenes.Length.ToString());

        if (!targetInstalled)
        {
            EditorGUILayout.HelpBox("Install Web Build Support from Unity Hub.", MessageType.Error);
        }
        else if (!defineReady)
        {
            EditorGUILayout.HelpBox(
                "Click Prepare WebGL Settings, then wait for Unity to finish compiling before building.",
                MessageType.Warning);
        }
    }
}

public static class VectoWebGLBuild
{
    public const string OutputPath = "Build/WebGL";
    private const string ApplicationId = "com.vectoarena.game";
    private const string RequiredDefine = "THIRDWEB_REOWN";
    private const string ConfigPath = "Assets/Resources/Config/appsettings.json";

    private static readonly string[] RequiredScenes =
    {
        "Assets/Scenes/AuthScene.unity",
        "Assets/Scenes/MainScene.unity",
        "Assets/Scenes/GameplayScene.unity"
    };

    public static string FullOutputPath => Path.GetFullPath(OutputPath);

    [MenuItem("VectoArena/Build/WebGL/Prepare Settings", priority = 110)]
    public static void PrepareWebGL()
    {
        EnsureWebGLModuleInstalled();

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL &&
            !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL))
        {
            throw new BuildFailedException("Unity could not switch the active build target to WebGL.");
        }

        bool defineChanged = AddRequiredDefine();
        ConfigurePlayerSettings(development: false);
        AssetDatabase.SaveAssets();

        string message = defineChanged
            ? "WebGL settings prepared. Wait for script compilation to finish, then start the build."
            : "WebGL settings are ready.";
        Debug.Log("[VectoWebGLBuild] " + message);
    }

    [MenuItem("VectoArena/Build/WebGL/Development", priority = 120)]
    public static void BuildDevelopment()
    {
        Build(development: true, autoRun: false, cleanOutput: false);
    }

    [MenuItem("VectoArena/Build/WebGL/Development (Build & Run)", priority = 121)]
    public static void BuildAndRunDevelopment()
    {
        Build(development: true, autoRun: true, cleanOutput: false);
    }

    [MenuItem("VectoArena/Build/WebGL/Release", priority = 130)]
    public static void BuildRelease()
    {
        Build(development: false, autoRun: false, cleanOutput: false);
    }

    [MenuItem("VectoArena/Build/WebGL/Clean Release", priority = 131)]
    public static void BuildCleanRelease()
    {
        Build(development: false, autoRun: false, cleanOutput: true);
    }

    public static void Build(bool development, bool autoRun, bool cleanOutput)
    {
        Preflight(development);
        ConfigurePlayerSettings(development);

        string outputPath = FullOutputPath;
        if (cleanOutput)
        {
            DeleteOutputSafely(outputPath);
        }

        Directory.CreateDirectory(outputPath);

        BuildOptions options = development
            ? BuildOptions.Development | BuildOptions.AllowDebugging
            : BuildOptions.None;

        if (autoRun)
        {
            options |= BuildOptions.AutoRunPlayer;
        }

        if (cleanOutput)
        {
            options |= BuildOptions.CleanBuildCache;
        }

        string[] scenes = GetEnabledScenes();
        Debug.Log(
            $"[VectoWebGLBuild] Starting {(development ? "development" : "release")} build. " +
            $"Scenes: {string.Join(", ", scenes)}. Output: {outputPath}");

        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.WebGL,
            options = options
        });

        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new BuildFailedException(
                $"WebGL build failed: {report.summary.result}. " +
                $"Errors: {report.summary.totalErrors}, warnings: {report.summary.totalWarnings}.");
        }

        ConfigureGeneratedWebTemplate();
        WriteBuildInfo(report, development);
        Debug.Log(
            $"[VectoWebGLBuild] Build completed: {outputPath} " +
            $"({report.summary.totalSize:N0} bytes, {report.summary.totalTime}).");
    }

    public static string[] GetEnabledScenes()
    {
        return EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
    }

    public static bool HasRequiredDefine()
    {
        PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.WebGL, out string[] defines);
        return defines.Contains(RequiredDefine, StringComparer.Ordinal);
    }

    private static void Preflight(bool development)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new BuildFailedException("Exit Play Mode before building.");
        }

        if (BuildPipeline.isBuildingPlayer)
        {
            throw new BuildFailedException("Another player build is already running.");
        }

        EnsureWebGLModuleInstalled();

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
        {
            throw new BuildFailedException(
                "WebGL is not the active build target. Run VectoArena > Build > WebGL > Prepare Settings first.");
        }

        if (!HasRequiredDefine())
        {
            throw new BuildFailedException(
                $"Missing {RequiredDefine} for WebGL. Run Prepare Settings, wait for compilation, then build again.");
        }

        ValidateScenes();
        ValidateRuntimeConfig(development);
    }

    private static void EnsureWebGLModuleInstalled()
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
        {
            throw new BuildFailedException(
                "Web Build Support is not installed. Add it to this Unity Editor version from Unity Hub.");
        }
    }

    private static void ValidateScenes()
    {
        string[] enabledScenes = GetEnabledScenes();
        if (enabledScenes.Length == 0)
        {
            throw new BuildFailedException("No enabled scenes were found in Build Settings.");
        }

        if (!enabledScenes.SequenceEqual(RequiredScenes))
        {
            throw new BuildFailedException(
                "WebGL build scenes must be enabled in this order: " + string.Join(", ", RequiredScenes));
        }

        string missingScene = RequiredScenes.FirstOrDefault(scene => !File.Exists(scene));
        if (!string.IsNullOrEmpty(missingScene))
        {
            throw new BuildFailedException("Required scene is missing: " + missingScene);
        }
    }

    private static void ValidateRuntimeConfig(bool development)
    {
        if (!File.Exists(ConfigPath))
        {
            throw new BuildFailedException("Runtime config is missing: " + ConfigPath);
        }

        AppConfigProbe config = JsonUtility.FromJson<AppConfigProbe>(File.ReadAllText(ConfigPath));
        if (config == null ||
            string.IsNullOrWhiteSpace(config.serverUrl) ||
            string.IsNullOrWhiteSpace(config.httpUrl))
        {
            throw new BuildFailedException("Runtime config must contain serverUrl and httpUrl.");
        }

        if (string.IsNullOrWhiteSpace(config.thirdwebClientId))
        {
            throw new BuildFailedException("Runtime config must contain thirdwebClientId.");
        }

        if (!development &&
            (!config.serverUrl.StartsWith("wss://", StringComparison.OrdinalIgnoreCase) ||
             !config.httpUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            throw new BuildFailedException(
                "Release WebGL builds require wss:// serverUrl and https:// httpUrl.");
        }
    }

    private static void ConfigurePlayerSettings(bool development)
    {
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.WebGL, ApplicationId);
        PlayerSettings.insecureHttpOption = development
            ? InsecureHttpOption.DevelopmentOnly
            : InsecureHttpOption.NotAllowed;

        PlayerSettings.WebGL.compressionFormat = development
            ? WebGLCompressionFormat.Disabled
            : WebGLCompressionFormat.Brotli;
        PlayerSettings.WebGL.decompressionFallback = !development;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.threadsSupport = false;
        PlayerSettings.WebGL.exceptionSupport = development
            ? WebGLExceptionSupport.FullWithStacktrace
            : WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.WebGL.initialMemorySize = Math.Max(PlayerSettings.WebGL.initialMemorySize, 256);
        PlayerSettings.WebGL.maximumMemorySize = Math.Max(PlayerSettings.WebGL.maximumMemorySize, 2048);
    }

    private static bool AddRequiredDefine()
    {
        PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.WebGL, out string[] defines);
        if (defines.Contains(RequiredDefine, StringComparer.Ordinal))
        {
            return false;
        }

        PlayerSettings.SetScriptingDefineSymbols(
            NamedBuildTarget.WebGL,
            defines.Append(RequiredDefine).Distinct(StringComparer.Ordinal).ToArray());
        return true;
    }

    private static void DeleteOutputSafely(string outputPath)
    {
        string fullBuildRoot = Path.GetFullPath("Build")
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        string normalizedOutput = Path.GetFullPath(outputPath);

        if (!normalizedOutput.StartsWith(fullBuildRoot, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                normalizedOutput.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                fullBuildRoot.TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new BuildFailedException("Refusing to clean an output path outside the project's Build directory.");
        }

        if (Directory.Exists(normalizedOutput))
        {
            Directory.Delete(normalizedOutput, recursive: true);
        }
    }

    private static void WriteBuildInfo(BuildReport report, bool development)
    {
        BuildInfo buildInfo = new BuildInfo
        {
            product = PlayerSettings.productName,
            version = PlayerSettings.bundleVersion,
            environment = development ? "development" : "release",
            unityVersion = Application.unityVersion,
            builtAtUtc = DateTime.UtcNow.ToString("O"),
            sizeBytes = report.summary.totalSize.ToString(),
            scenes = GetEnabledScenes()
        };

        string path = Path.Combine(FullOutputPath, "build-info.json");
        File.WriteAllText(path, JsonUtility.ToJson(buildInfo, prettyPrint: true));
    }

    private static void ConfigureGeneratedWebTemplate()
    {
        string indexPath = Path.Combine(FullOutputPath, "index.html");
        if (!File.Exists(indexPath))
        {
            return;
        }

        const string disabledSetting = "// config.autoSyncPersistentDataPath = true;";
        const string enabledSetting = "config.autoSyncPersistentDataPath = true;";
        string html = File.ReadAllText(indexPath);
        html = html.Replace(disabledSetting, enabledSetting);

        string cacheVersion = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        html = html.Replace(
            "var loaderUrl = buildUrl + \"/WebGL.loader.js\";",
            $"var loaderUrl = buildUrl + \"/WebGL.loader.js?v={cacheVersion}\";");
        html = html.Replace(".unityweb\",", $".unityweb?v={cacheVersion}\",");

        File.WriteAllText(indexPath, html);
    }

    [Serializable]
    private sealed class AppConfigProbe
    {
        public string serverUrl;
        public string httpUrl;
        public string thirdwebClientId;
    }

    [Serializable]
    private sealed class BuildInfo
    {
        public string product;
        public string version;
        public string environment;
        public string unityVersion;
        public string builtAtUtc;
        public string sizeBytes;
        public string[] scenes;
    }
}
#endif
