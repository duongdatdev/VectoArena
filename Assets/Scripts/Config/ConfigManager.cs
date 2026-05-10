using UnityEngine;
using System.IO;

public static class ConfigManager
{
    private static AppConfig _config;
    private const string ConfigPath = "Config/appsettings";

    public static AppConfig Config
    {
        get
        {
            if (_config == null)
            {
                LoadConfig();
            }
            return _config;
        }
    }

    public static void LoadConfig()
    {
        TextAsset configAsset = Resources.Load<TextAsset>(ConfigPath);
        if (configAsset != null)
        {
            _config = JsonUtility.FromJson<AppConfig>(configAsset.text);
            Debug.Log("[ConfigManager] Configuration loaded successfully.");
        }
        else
        {
            Debug.LogWarning($"[ConfigManager] Configuration file not found at Resources/{ConfigPath}.json. Using default values.");
            _config = new AppConfig();
        }
    }
}
