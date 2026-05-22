using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VectoArena.Schema;

public class CombatStatsHUDController : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private RectTransform minimapBorder;
    [SerializeField] private Vector2 offsetFromMinimapTopLeft = new Vector2(-20f, -10f);
    [SerializeField] private Vector2 rowSize = new Vector2(128f, 54f);
    [SerializeField] private float rowGap = 4f;

    [Header("Style")]
    [SerializeField] private Sprite skullIcon;
    [SerializeField] private Sprite playerIcon;
    [SerializeField] private Color textColor = new Color(0.79f, 0.79f, 0.84f, 1f);
    [SerializeField] private Color textOutlineColor = new Color(0.18f, 0.16f, 0.20f, 0.95f);
    [SerializeField] private Color iconColor = new Color(0.76f, 0.76f, 0.80f, 1f);
    [SerializeField] private Color iconShadowColor = new Color(0.18f, 0.16f, 0.20f, 0.85f);

    private static Sprite skullSprite;
    private static Sprite playerSprite;

    private RectTransform root;
    private TextMeshProUGUI killsLabel;
    private TextMeshProUGUI aliveLabel;
    private NetworkPlayerSync localPlayerSync;
    private int displayedKills = -1;
    private int displayedAlive = -1;

    private void Awake()
    {
        BuildHud();
    }

    private void Update()
    {
        if (localPlayerSync == null)
        {
            localPlayerSync = FindLocalPlayerSync();
        }

        int kills = 0;
        PlayerState state = localPlayerSync != null ? localPlayerSync.GetState() : null;
        if (state != null)
        {
            kills = Mathf.Max(0, Mathf.RoundToInt(state.kills));
        }

        int alive = GetAliveCountFallback();
        if (killsLabel != null && kills != displayedKills)
        {
            killsLabel.text = kills.ToString();
            displayedKills = kills;
        }

        if (aliveLabel != null && alive != displayedAlive)
        {
            aliveLabel.text = alive.ToString();
            displayedAlive = alive;
        }
    }

    private void BuildHud()
    {
        EnsureGeneratedSprites();

        Canvas canvas = GetComponentInParent<Canvas>();
        RectTransform parent = canvas != null ? canvas.GetComponent<RectTransform>() : GetComponent<RectTransform>();
        if (parent == null)
        {
            return;
        }

        if (minimapBorder == null)
        {
            Transform minimap = parent.Find("MinimapBorder");
            if (minimap != null)
            {
                minimapBorder = minimap as RectTransform;
            }
        }

        root = new GameObject("BlastCombatStatsHud", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(parent, false);
        root.anchorMin = new Vector2(1f, 1f);
        root.anchorMax = new Vector2(1f, 1f);
        root.pivot = new Vector2(1f, 1f);
        root.sizeDelta = new Vector2(rowSize.x, rowSize.y * 2f + rowGap);
        root.localScale = Vector3.one;
        root.localRotation = Quaternion.identity;

        PositionBesideMinimap();

        Sprite resolvedSkullIcon = skullIcon != null ? skullIcon : Resources.Load<Sprite>("HudIcons/icon-players-killed");
        Sprite resolvedPlayerIcon = playerIcon != null ? playerIcon : Resources.Load<Sprite>("HudIcons/icon-players-alive");

        killsLabel = CreateStatRow("Kills", 0, resolvedSkullIcon != null ? resolvedSkullIcon : skullSprite);
        aliveLabel = CreateStatRow("AlivePlayers", 1, resolvedPlayerIcon != null ? resolvedPlayerIcon : playerSprite);
    }

    private void PositionBesideMinimap()
    {
        Vector2 anchoredPosition = new Vector2(-338f, -32f);
        if (minimapBorder != null)
        {
            float minimapLeft = minimapBorder.anchoredPosition.x - minimapBorder.sizeDelta.x;
            float minimapTop = minimapBorder.anchoredPosition.y + minimapBorder.sizeDelta.y;
            anchoredPosition = new Vector2(
                minimapLeft + offsetFromMinimapTopLeft.x,
                minimapTop + offsetFromMinimapTopLeft.y
            );
        }

        root.anchoredPosition = anchoredPosition;
    }

    private TextMeshProUGUI CreateStatRow(string rowName, int index, Sprite icon)
    {
        RectTransform row = new GameObject(rowName, typeof(RectTransform)).GetComponent<RectTransform>();
        row.SetParent(root, false);
        row.anchorMin = new Vector2(1f, 1f);
        row.anchorMax = new Vector2(1f, 1f);
        row.pivot = new Vector2(1f, 1f);
        row.sizeDelta = rowSize;
        row.anchoredPosition = new Vector2(0f, -index * (rowSize.y + rowGap));

        Image shadow = CreateIcon(rowName + "IconShadow", row, icon, iconShadowColor);
        SetupRect(shadow.rectTransform, new Vector2(46f, 46f), new Vector2(-13f, -5f), new Vector2(1f, 0.5f));

        Image iconImage = CreateIcon(rowName + "Icon", row, icon, iconColor);
        SetupRect(iconImage.rectTransform, new Vector2(46f, 46f), new Vector2(-17f, -1f), new Vector2(1f, 0.5f));

        TextMeshProUGUI label = CreateLabel(rowName + "Value", row);
        SetupRect(label.rectTransform, new Vector2(62f, 50f), new Vector2(-70f, -1f), new Vector2(1f, 0.5f));
        return label;
    }

    private Image CreateIcon(string objectName, RectTransform parent, Sprite sprite, Color color)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private TextMeshProUGUI CreateLabel(string objectName, RectTransform parent)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        TextMeshProUGUI label = obj.GetComponent<TextMeshProUGUI>();
        label.text = "0";
        label.fontSize = 42f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.MidlineRight;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.color = textColor;
        label.outlineColor = textOutlineColor;
        label.outlineWidth = 0.22f;
        label.raycastTarget = false;
        return label;
    }

    private static void SetupRect(RectTransform rect, Vector2 size, Vector2 anchoredPosition, Vector2 pivot)
    {
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private int GetAliveCountFallback()
    {
        if (NetworkManager.Instance != null)
        {
            int count = NetworkManager.Instance.GetAlivePlayerCount();
            if (count > 0)
            {
                return count;
            }
        }

        int alive = 0;
        NetworkPlayerSync[] players = FindObjectsByType<NetworkPlayerSync>(FindObjectsInactive.Exclude);
        foreach (NetworkPlayerSync player in players)
        {
            PlayerState state = player != null ? player.GetState() : null;
            if (state != null && !state.isDead && state.hp > 0)
            {
                alive++;
            }
        }

        return alive;
    }

    private NetworkPlayerSync FindLocalPlayerSync()
    {
        NetworkPlayerSync[] players = FindObjectsByType<NetworkPlayerSync>(FindObjectsInactive.Exclude);
        foreach (NetworkPlayerSync player in players)
        {
            if (player != null && player.isLocalPlayer)
            {
                return player;
            }
        }

        return null;
    }

    private static void EnsureGeneratedSprites()
    {
        if (skullSprite != null && playerSprite != null)
        {
            return;
        }

        skullSprite = CreateIconSprite(DrawSkullPixel);
        playerSprite = CreateIconSprite(DrawPlayerPixel);
    }

    private static Sprite CreateIconSprite(System.Func<int, int, int, Color> draw)
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                texture.SetPixel(x, y, draw(x, y, size));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static Color DrawSkullPixel(int x, int y, int size)
    {
        Vector2 p = new Vector2(x, y);
        Vector2 head = new Vector2(size * 0.5f, size * 0.57f);
        bool cranium = Vector2.Distance(p, head) <= size * 0.31f;
        bool jaw = Mathf.Abs(x - size * 0.5f) <= size * 0.20f && y >= size * 0.19f && y <= size * 0.43f;
        bool eyeLeft = Vector2.Distance(p, new Vector2(size * 0.39f, size * 0.61f)) <= size * 0.07f;
        bool eyeRight = Vector2.Distance(p, new Vector2(size * 0.61f, size * 0.61f)) <= size * 0.07f;
        bool nose = Mathf.Abs(x - size * 0.5f) <= size * 0.04f && y >= size * 0.43f && y <= size * 0.54f;
        bool teethGap = y >= size * 0.22f && y <= size * 0.33f && Mathf.Abs((x % 8) - 4) <= 1;

        if ((cranium || jaw) && !eyeLeft && !eyeRight && !nose && !teethGap)
        {
            return Color.white;
        }

        return Color.clear;
    }

    private static Color DrawPlayerPixel(int x, int y, int size)
    {
        Vector2 p = new Vector2(x, y);
        bool head = Vector2.Distance(p, new Vector2(size * 0.5f, size * 0.71f)) <= size * 0.15f;
        bool neck = Mathf.Abs(x - size * 0.5f) <= size * 0.07f && y >= size * 0.47f && y <= size * 0.61f;
        bool body = Mathf.Abs(x - size * 0.5f) <= size * 0.18f && y >= size * 0.22f && y <= size * 0.48f;
        bool shoulders = Vector2.Distance(p, new Vector2(size * 0.5f, size * 0.39f)) <= size * 0.30f && y <= size * 0.44f && y >= size * 0.25f;

        return head || neck || body || shoulders ? Color.white : Color.clear;
    }
}
