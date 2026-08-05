using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class MobileScenePerformanceOptimizer
{
    private static readonly string[] NoShadowPrefixes =
    {
        "cobble_stones",
        "grass_",
        "flower_",
        "bush",
        "bird",
        "trashcan"
    };

    private static readonly string[] NoReceiveShadowPrefixes =
    {
        "cobble_stones",
        "grass_",
        "flower_",
        "bird"
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        if (!Application.isMobilePlatform)
        {
            return;
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            DisableMobilePostProcessing(root);
            ReduceDecorativeShadowCost(root);
        }
    }

    private static void DisableMobilePostProcessing(GameObject root)
    {
        foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
        {
            if (camera.TryGetComponent(out UniversalAdditionalCameraData cameraData))
            {
                cameraData.renderPostProcessing = false;
            }
        }
    }

    private static void ReduceDecorativeShadowCost(GameObject root)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            string objectName = renderer.gameObject.name;
            if (StartsWithAny(objectName, NoShadowPrefixes))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }

            if (StartsWithAny(objectName, NoReceiveShadowPrefixes))
            {
                renderer.receiveShadows = false;
            }
        }
    }

    private static bool StartsWithAny(string value, string[] prefixes)
    {
        foreach (string prefix in prefixes)
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
