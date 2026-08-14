using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;

/// <summary>Applies device-safe insets to menu UI Toolkit documents and selected legacy gameplay HUD anchors.</summary>
public sealed class ResponsiveSafeAreaController : MonoBehaviour
{
    private UIDocument document;
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallBootstrap()
    {
        SceneManager.sceneLoaded -= InstallForScene;
        SceneManager.sceneLoaded += InstallForScene;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallCurrentScene()
    {
        InstallForScene(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void InstallForScene(Scene scene, LoadSceneMode mode)
    {
        UIDocument[] documents = FindObjectsByType<UIDocument>(FindObjectsInactive.Include);
        foreach (UIDocument uiDocument in documents)
        {
            if (uiDocument == null || uiDocument.GetComponent<DeathScreenManager>() != null)
            {
                continue;
            }

            if (!uiDocument.TryGetComponent(out ResponsiveSafeAreaController _))
            {
                uiDocument.gameObject.AddComponent<ResponsiveSafeAreaController>();
            }
        }
    }

    private void Awake()
    {
        document = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        Apply(true);
    }

    private void Update()
    {
        Apply(false);
    }

    private void Apply(bool force)
    {
        if (document == null || document.rootVisualElement == null || Screen.width <= 0 || Screen.height <= 0)
        {
            return;
        }

        Rect safeArea = Screen.safeArea;
        Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);
        if (!force && safeArea == lastSafeArea && screenSize == lastScreenSize)
        {
            return;
        }

        lastSafeArea = safeArea;
        lastScreenSize = screenSize;

        VisualElement root = document.rootVisualElement.Q<VisualElement>("root") ?? document.rootVisualElement;
        float panelWidth = Mathf.Max(1f, root.resolvedStyle.width);
        float panelHeight = Mathf.Max(1f, root.resolvedStyle.height);
        float scaleX = panelWidth / Screen.width;
        float scaleY = panelHeight / Screen.height;

        root.style.paddingLeft = safeArea.xMin * scaleX;
        root.style.paddingRight = (Screen.width - safeArea.xMax) * scaleX;
        root.style.paddingTop = (Screen.height - safeArea.yMax) * scaleY;
        root.style.paddingBottom = safeArea.yMin * scaleY;
        root.EnableInClassList("mobile-compact", panelHeight > 0f && panelHeight < 760f);
        root.EnableInClassList("mobile-wide", Screen.width / (float)Screen.height >= 2f);
    }
}

public sealed class CanvasSafeAreaController : MonoBehaviour
{
    private static readonly string[] TargetNames =
    {
        "MinimapBorder", "WeaponIcoBack", "HealthFill", "ZoneTimerCounter", "ZoneStatusNotification", "BlastCombatStatsHud"
    };

    private readonly Dictionary<RectTransform, Vector2> initialPositions = new Dictionary<RectTransform, Vector2>();
    private Canvas canvas;
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;

    public static void InstallInScene(Scene scene)
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
        foreach (Canvas candidate in canvases)
        {
            if (candidate != null && candidate.name == "UICanvas" && !candidate.TryGetComponent(out CanvasSafeAreaController _))
            {
                candidate.gameObject.AddComponent<CanvasSafeAreaController>();
            }
        }
    }

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
    }

    private void LateUpdate()
    {
        ApplySafeArea();
    }

    private void ApplySafeArea()
    {
        if (canvas == null || Screen.width <= 0 || Screen.height <= 0)
        {
            return;
        }

        CaptureTargets();
        Rect safeArea = Screen.safeArea;
        Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);
        if (safeArea == lastSafeArea && screenSize == lastScreenSize)
        {
            return;
        }

        lastSafeArea = safeArea;
        lastScreenSize = screenSize;

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        Vector2 reference = scaler != null ? scaler.referenceResolution : new Vector2(Screen.width, Screen.height);
        float scaleX = reference.x / Screen.width;
        float scaleY = reference.y / Screen.height;
        float left = safeArea.xMin * scaleX;
        float right = (Screen.width - safeArea.xMax) * scaleX;
        float bottom = safeArea.yMin * scaleY;
        float top = (Screen.height - safeArea.yMax) * scaleY;

        foreach (KeyValuePair<RectTransform, Vector2> pair in initialPositions)
        {
            RectTransform rect = pair.Key;
            if (rect == null)
            {
                continue;
            }

            Vector2 position = pair.Value;
            if (rect.anchorMin.x <= 0.1f && rect.anchorMax.x <= 0.5f) position.x += left;
            if (rect.anchorMin.x >= 0.5f && rect.anchorMax.x >= 0.9f) position.x -= right;
            if (rect.anchorMin.y <= 0.1f && rect.anchorMax.y <= 0.5f) position.y += bottom;
            if (rect.anchorMin.y >= 0.5f && rect.anchorMax.y >= 0.9f) position.y -= top;
            rect.anchoredPosition = position;
        }
    }

    private void CaptureTargets()
    {
        RectTransform root = canvas.transform as RectTransform;
        if (root == null)
        {
            return;
        }

        foreach (string targetName in TargetNames)
        {
            Transform target = root.Find(targetName);
            if (target is RectTransform rect && !initialPositions.ContainsKey(rect))
            {
                initialPositions.Add(rect, rect.anchoredPosition);
                lastScreenSize = Vector2Int.zero;
            }
        }
    }
}
