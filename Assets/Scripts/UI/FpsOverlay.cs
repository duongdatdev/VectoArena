using UnityEngine;

public class FpsOverlay : MonoBehaviour
{
    private const float RefreshInterval = 0.25f;

    private static FpsOverlay instance;
    private GUIStyle labelStyle;
    private int frameCount;
    private float elapsed;
    private int currentFps;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        GameObject overlayObject = new GameObject("FpsOverlay");
        instance = overlayObject.AddComponent<FpsOverlay>();
        DontDestroyOnLoad(overlayObject);
    }

    private void OnEnable()
    {
        GameSettings.Initialize();
    }

    private void Update()
    {
        if (!GameSettings.ShowFps)
        {
            frameCount = 0;
            elapsed = 0f;
            return;
        }

        frameCount++;
        elapsed += Time.unscaledDeltaTime;
        if (elapsed < RefreshInterval)
        {
            return;
        }

        float fps = frameCount / elapsed;
        currentFps = Mathf.RoundToInt(fps);
        frameCount = 0;
        elapsed = 0f;
    }

    private void OnGUI()
    {
        if (!GameSettings.ShowFps)
        {
            return;
        }

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 18;
            labelStyle.normal.textColor = Color.white;
            labelStyle.alignment = TextAnchor.MiddleLeft;
        }

        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(new Rect(12f, 12f, 86f, 32f), Texture2D.whiteTexture);
        GUI.color = previousColor;
        GUI.Label(new Rect(22f, 16f, 120f, 24f), $"{currentFps} FPS", labelStyle);
    }
}
