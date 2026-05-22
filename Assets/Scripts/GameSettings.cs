using System;
using UnityEngine;

public enum GameQualityPreset
{
    Performance,
    Quality
}

public static class GameSettings
{
    private const string MasterVolumeKey = "settings.masterVolume";
    private const string MusicVolumeKey = "settings.musicVolume";
    private const string SfxVolumeKey = "settings.sfxVolume";
    private const string QualityPresetKey = "settings.qualityPreset";
    private const string FpsLimitKey = "settings.fpsLimit";
    private const string VSyncKey = "settings.vSync";
    private const string FullscreenKey = "settings.fullscreen";
    private const string ShowFpsKey = "settings.showFps";
    private const string CameraDistanceKey = "settings.cameraDistance";

    public const float MinCameraDistance = 0.75f;
    public const float MaxCameraDistance = 1.35f;
    public static readonly int[] SupportedFpsLimits = { 30, 60, 120, 0 };

    private static bool initialized;

    public static event Action Changed;

    private static float masterVolume;
    private static float musicVolume;
    private static float sfxVolume;
    private static GameQualityPreset qualityPreset;
    private static int fpsLimit;
    private static bool vSync;
    private static bool fullscreen;
    private static bool showFps;
    private static float cameraDistance;

    public static float MasterVolume => masterVolume;
    public static float MusicVolume => musicVolume;
    public static float SfxVolume => sfxVolume;
    public static GameQualityPreset QualityPreset => qualityPreset;
    public static int FpsLimit => fpsLimit;
    public static bool VSync => vSync;
    public static bool Fullscreen => fullscreen;
    public static bool ShowFps => showFps;
    public static float CameraDistance => cameraDistance;

    public static bool SupportsFullscreen => !Application.isMobilePlatform;
    public static bool SupportsVSync => !Application.isMobilePlatform;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializeOnLoad()
    {
        Initialize();
    }

    public static void Initialize()
    {
        if (!initialized)
        {
            Load();
            initialized = true;
        }

        ApplyAll();
    }

    public static void SetMasterVolume(float value) => SetFloat(ref masterVolume, MasterVolumeKey, value, 0f, 1f);
    public static void SetMusicVolume(float value) => SetFloat(ref musicVolume, MusicVolumeKey, value, 0f, 1f);
    public static void SetSfxVolume(float value) => SetFloat(ref sfxVolume, SfxVolumeKey, value, 0f, 1f);
    public static void SetCameraDistance(float value) => SetFloat(ref cameraDistance, CameraDistanceKey, value, MinCameraDistance, MaxCameraDistance);

    public static void SetQualityPreset(GameQualityPreset value)
    {
        InitializeIfNeeded();
        if (qualityPreset == value)
        {
            return;
        }

        qualityPreset = value;
        PlayerPrefs.SetInt(QualityPresetKey, (int)value);
        ApplyQuality();
        SaveAndNotify();
    }

    public static void SetFpsLimit(int value)
    {
        InitializeIfNeeded();
        value = NormalizeFpsLimit(value);
        if (fpsLimit == value)
        {
            return;
        }

        fpsLimit = value;
        PlayerPrefs.SetInt(FpsLimitKey, value);
        ApplyFrameRate();
        SaveAndNotify();
    }

    public static void SetVSync(bool value)
    {
        InitializeIfNeeded();
        if (!SupportsVSync)
        {
            value = false;
        }

        if (vSync == value)
        {
            return;
        }

        vSync = value;
        PlayerPrefs.SetInt(VSyncKey, value ? 1 : 0);
        ApplyFrameRate();
        SaveAndNotify();
    }

    public static void SetFullscreen(bool value)
    {
        InitializeIfNeeded();
        if (!SupportsFullscreen)
        {
            value = true;
        }

        if (fullscreen == value)
        {
            return;
        }

        fullscreen = value;
        PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
        ApplyFullscreen();
        SaveAndNotify();
    }

    public static void SetShowFps(bool value)
    {
        InitializeIfNeeded();
        if (showFps == value)
        {
            return;
        }

        showFps = value;
        PlayerPrefs.SetInt(ShowFpsKey, value ? 1 : 0);
        SaveAndNotify();
    }

    private static void Load()
    {
        GameQualityPreset defaultQuality = Application.isMobilePlatform ? GameQualityPreset.Performance : GameQualityPreset.Quality;

        masterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
        musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 0.8f);
        sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, 1f);
        qualityPreset = (GameQualityPreset)PlayerPrefs.GetInt(QualityPresetKey, (int)defaultQuality);
        fpsLimit = NormalizeFpsLimit(PlayerPrefs.GetInt(FpsLimitKey, Application.isMobilePlatform ? 60 : 0));
        vSync = SupportsVSync && PlayerPrefs.GetInt(VSyncKey, 0) == 1;
        fullscreen = !SupportsFullscreen || PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
        showFps = PlayerPrefs.GetInt(ShowFpsKey, 0) == 1;
        cameraDistance = Mathf.Clamp(PlayerPrefs.GetFloat(CameraDistanceKey, 1f), MinCameraDistance, MaxCameraDistance);
    }

    private static void ApplyAll()
    {
        AudioListener.volume = masterVolume;
        ApplyQuality();
        ApplyFrameRate();
        ApplyFullscreen();
        Changed?.Invoke();
    }

    private static void ApplyQuality()
    {
        string qualityName = qualityPreset == GameQualityPreset.Performance ? "Mobile" : "PC";
        string[] names = QualitySettings.names;
        int qualityIndex = Array.IndexOf(names, qualityName);
        if (qualityIndex < 0)
        {
            qualityIndex = qualityPreset == GameQualityPreset.Performance ? 0 : Mathf.Max(0, names.Length - 1);
        }

        if (qualityIndex >= 0 && qualityIndex < names.Length)
        {
            QualitySettings.SetQualityLevel(qualityIndex, true);
        }
    }

    private static void ApplyFrameRate()
    {
        QualitySettings.vSyncCount = SupportsVSync && vSync ? 1 : 0;
        Application.targetFrameRate = QualitySettings.vSyncCount > 0 ? -1 : fpsLimit;
    }

    private static void ApplyFullscreen()
    {
        if (!SupportsFullscreen)
        {
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        if (fullscreen)
        {
            WebGLSupport.WebGLWindow.MakeFullscreen();
        }
        else
        {
            WebGLSupport.WebGLWindow.ExitFullscreen();
        }
#else
        Screen.fullScreen = Fullscreen;
#endif
    }

    private static void SetFloat(ref float field, string key, float value, float min, float max)
    {
        InitializeIfNeeded();
        value = Mathf.Clamp(value, min, max);
        if (Mathf.Approximately(field, value))
        {
            return;
        }

        field = value;
        PlayerPrefs.SetFloat(key, value);
        if (key == MasterVolumeKey)
        {
            AudioListener.volume = field;
        }

        SaveAndNotify();
    }

    private static int NormalizeFpsLimit(int value)
    {
        foreach (int supported in SupportedFpsLimits)
        {
            if (supported == value)
            {
                return value;
            }
        }

        return 0;
    }

    private static void InitializeIfNeeded()
    {
        if (!initialized)
        {
            Initialize();
        }
    }

    private static void SaveAndNotify()
    {
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
