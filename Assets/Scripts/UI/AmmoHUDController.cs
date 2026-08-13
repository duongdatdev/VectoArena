using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VectoArena.Schema;

public class AmmoHUDController : MonoBehaviour
{
    private const float WeaponHudHorizontalOffset = 400f;

    [Header("Weapon Icons")]
    [SerializeField] private Sprite swordIcon;
    [SerializeField] private Sprite rifleIcon;
    [SerializeField] private Sprite shotgunIcon;
    [SerializeField] private Sprite burstRifleIcon;
    [SerializeField] private Sprite rebelRifleIcon;
    [SerializeField] private Sprite machineGunIcon;
    [SerializeField] private Sprite minigunIcon;
    [SerializeField] private Sprite pistolIcon;
    [SerializeField] private Sprite sniperIcon;
    [SerializeField] private Sprite launcherIcon;

    [Header("Colors")]
    [SerializeField] private Color readyAmmoColor = new Color(0.95f, 0.66f, 0.16f);
    [SerializeField] private Color emptyAmmoColor = new Color(0.86f, 0.27f, 0.22f);

    private static Sprite circleSprite;
    private static Sprite softCircleSprite;

    private NetworkPlayerSync localPlayerSync;

    private RectTransform hudRoot;
    private CanvasGroup hudCanvasGroup;
    private Image activeSlotBg;
    private Image inactiveSlotBg;
    private Image inactiveWeaponIcon;
    private Image inactiveWeaponShadow;
    private TextMeshProUGUI inactiveWeaponFallback;
    private Image activeWeaponIcon;
    private Image activeWeaponShadow;
    private TextMeshProUGUI activeWeaponFallback;
    private TextMeshProUGUI ammoBadge;
    private GameObject switchPrompt;

    public RectTransform SwitchPromptRect => switchPrompt != null && switchPrompt.activeInHierarchy
        ? switchPrompt.transform as RectTransform
        : null;
    public bool CanSwitchWeapon => switchPrompt != null && switchPrompt.activeInHierarchy;

    private void Awake()
    {
        LoadBlastRoyaleWeaponIcons();
        BuildBlastStyleHud();
    }

    private void Update()
    {
        if (localPlayerSync == null)
        {
            localPlayerSync = FindLocalPlayerSync();
        }

        if (localPlayerSync == null)
        {
            SetVisible(false);
            return;
        }

        PlayerState state = localPlayerSync.GetState();
        if (state == null || string.IsNullOrEmpty(state.currentWeapon))
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);
        UpdateBlastStyleHud(state);
    }

    private void BuildBlastStyleHud()
    {
        EnsureGeneratedSprites();

        Canvas canvas = GetComponentInParent<Canvas>();
        RectTransform parent = canvas != null
            ? canvas.GetComponent<RectTransform>()
            : GetComponent<RectTransform>();
        if (parent == null)
        {
            return;
        }

        hudRoot = CreateRect("BlastWeaponHud", parent, new Vector2(190f, 126f), new Vector2(0f, 0f));
        hudRoot.anchorMin = new Vector2(0.5f, 0f);
        hudRoot.anchorMax = new Vector2(0.5f, 0f);
        hudRoot.pivot = new Vector2(0.5f, 0.5f);
        // Leave enough room between the ammo badge and the centered health bar.
        hudRoot.anchoredPosition = new Vector2(WeaponHudHorizontalOffset, 96f);
        hudCanvasGroup = hudRoot.gameObject.AddComponent<CanvasGroup>();

        inactiveSlotBg = CreateImage("InactiveSlot", hudRoot, softCircleSprite, new Color(0.09f, 0.09f, 0.12f, 0.72f));
        SetupRect(inactiveSlotBg.rectTransform, new Vector2(76f, 76f), new Vector2(54f, -8f));
        inactiveSlotBg.transform.SetAsFirstSibling();

        inactiveWeaponShadow = CreateImage("InactiveWeaponShadow", inactiveSlotBg.rectTransform, null, new Color(0f, 0f, 0f, 0.42f));
        inactiveWeaponShadow.preserveAspect = true;
        SetupRect(inactiveWeaponShadow.rectTransform, new Vector2(56f, 56f), new Vector2(2f, -3f));

        inactiveWeaponIcon = CreateImage("InactiveWeaponIcon", inactiveSlotBg.rectTransform, null, Color.white);
        inactiveWeaponIcon.preserveAspect = true;
        SetupRect(inactiveWeaponIcon.rectTransform, new Vector2(56f, 56f), Vector2.zero);

        inactiveWeaponFallback = CreateLabel("InactiveWeaponFallback", inactiveSlotBg.rectTransform, string.Empty, 10, Color.white);
        inactiveWeaponFallback.alignment = TextAlignmentOptions.Center;
        inactiveWeaponFallback.fontStyle = FontStyles.Bold;
        inactiveWeaponFallback.textWrappingMode = TextWrappingModes.NoWrap;
        SetupRect(inactiveWeaponFallback.rectTransform, new Vector2(60f, 24f), Vector2.zero);

        activeSlotBg = CreateImage("ActiveSlot", hudRoot, circleSprite, new Color(0.77f, 0.80f, 0.82f, 0.90f));
        SetupRect(activeSlotBg.rectTransform, new Vector2(96f, 96f), new Vector2(0f, 0f));

        Image activeSlotBorder = CreateImage("ActiveSlotBorder", activeSlotBg.rectTransform, circleSprite, new Color(0.10f, 0.10f, 0.13f, 0.96f));
        SetupRect(activeSlotBorder.rectTransform, new Vector2(108f, 108f), Vector2.zero);
        activeSlotBorder.transform.SetAsFirstSibling();

        activeWeaponShadow = CreateImage("WeaponIconShadow", activeSlotBg.rectTransform, null, new Color(0f, 0f, 0f, 0.35f));
        activeWeaponShadow.preserveAspect = true;
        SetupRect(activeWeaponShadow.rectTransform, new Vector2(76f, 76f), new Vector2(3f, -4f));

        activeWeaponIcon = CreateImage("WeaponIcon", activeSlotBg.rectTransform, null, Color.white);
        activeWeaponIcon.preserveAspect = true;
        SetupRect(activeWeaponIcon.rectTransform, new Vector2(76f, 76f), Vector2.zero);

        activeWeaponFallback = CreateLabel("WeaponFallback", activeSlotBg.rectTransform, "MELEE", 14, Color.white);
        activeWeaponFallback.alignment = TextAlignmentOptions.Center;
        activeWeaponFallback.fontStyle = FontStyles.Bold;
        activeWeaponFallback.textWrappingMode = TextWrappingModes.NoWrap;
        SetupRect(activeWeaponFallback.rectTransform, new Vector2(78f, 34f), Vector2.zero);

        ammoBadge = CreateLabel("Ammo", hudRoot, "0", 30, readyAmmoColor);
        ammoBadge.alignment = TextAlignmentOptions.Center;
        ammoBadge.fontStyle = FontStyles.Bold;
        ammoBadge.textWrappingMode = TextWrappingModes.NoWrap;
        SetupRect(ammoBadge.rectTransform, new Vector2(62f, 44f), new Vector2(-84f, -2f));

        switchPrompt = new GameObject("SwitchKeyPrompt", typeof(RectTransform));
        switchPrompt.transform.SetParent(hudRoot, false);
        SetupRect((RectTransform)switchPrompt.transform, new Vector2(40f, 40f), new Vector2(48f, 48f));
    }

    public void SetMobileSwitchPromptReplacement(bool replacedByMobileButton)
    {
        // The prompt itself is only an invisible anchor used by the mobile switch button.
    }

    private void UpdateBlastStyleHud(PlayerState state)
    {
        bool hasRangedWeapon = !string.IsNullOrEmpty(state.rangedWeapon);
        bool isMeleeWeapon = !string.IsNullOrEmpty(state.currentWeapon) && state.currentWeapon == state.meleeWeapon;

        string inactiveWeapon = isMeleeWeapon ? state.rangedWeapon : state.meleeWeapon;

        UpdateWeaponIcon(state.currentWeapon, activeWeaponIcon, activeWeaponShadow, activeWeaponFallback);
        UpdateWeaponIcon(inactiveWeapon, inactiveWeaponIcon, inactiveWeaponShadow, inactiveWeaponFallback);
        UpdateAmmo(state, isMeleeWeapon);

        if (switchPrompt != null)
        {
            switchPrompt.SetActive(hasRangedWeapon);
        }

        if (inactiveSlotBg != null)
        {
            inactiveSlotBg.gameObject.SetActive(hasRangedWeapon);
        }

        if (activeSlotBg != null)
        {
            activeSlotBg.color = isMeleeWeapon
                ? new Color(0.78f, 0.82f, 0.83f, 0.92f)
                : new Color(0.88f, 0.88f, 0.88f, 0.94f);
        }
    }

    private void UpdateAmmo(PlayerState state, bool isMeleeWeapon)
    {
        int ammo = Mathf.Max(0, Mathf.RoundToInt(state.ammo));

        if (ammoBadge == null)
        {
            return;
        }

        ammoBadge.gameObject.SetActive(!isMeleeWeapon);
        ammoBadge.text = ammo.ToString();
        ammoBadge.color = ammo > 0 ? readyAmmoColor : emptyAmmoColor;
    }

    private void UpdateWeaponIcon(string weaponName, Image targetIcon, Image targetShadow, TextMeshProUGUI targetFallback)
    {
        Sprite icon = GetIconForWeapon(weaponName);
        string fallbackText = GetFallbackText(weaponName);

        if (targetIcon != null)
        {
            targetIcon.sprite = icon;
            targetIcon.enabled = icon != null;
        }

        if (targetShadow != null)
        {
            targetShadow.sprite = icon;
            targetShadow.enabled = icon != null;
        }

        if (targetFallback != null)
        {
            targetFallback.gameObject.SetActive(icon == null && !string.IsNullOrEmpty(fallbackText));
            targetFallback.text = fallbackText;
        }
    }

    private Sprite GetIconForWeapon(string weaponName)
    {
        switch (weaponName)
        {
            case "Sword":
                return swordIcon;
            case "Rifle":
                return rifleIcon;
            case "BurstRifle":
                return burstRifleIcon != null ? burstRifleIcon : rifleIcon;
            case "RebelRifle":
                return rebelRifleIcon != null ? rebelRifleIcon : rifleIcon;
            case "MachineGun":
                return machineGunIcon != null ? machineGunIcon : rifleIcon;
            case "Minigun":
                return minigunIcon != null ? minigunIcon : machineGunIcon;
            case "Pistol":
                return pistolIcon != null ? pistolIcon : rifleIcon;
            case "Sniper":
            case "HunterSniper":
                return sniperIcon != null ? sniperIcon : rifleIcon;
            case "Launcher":
                return launcherIcon != null ? launcherIcon : rifleIcon;
            case "Shotgun":
            case "BlasterShotgun":
                return shotgunIcon != null ? shotgunIcon : rifleIcon;
            default:
                return null;
        }
    }

    private string GetFallbackText(string weaponName)
    {
        if (string.IsNullOrEmpty(weaponName))
        {
            return string.Empty;
        }

        if (weaponName == "Sword")
        {
            return "MELEE";
        }

        return weaponName.Length <= 6 ? weaponName.ToUpperInvariant() : weaponName.Substring(0, 6).ToUpperInvariant();
    }

    private void SetVisible(bool visible)
    {
        if (hudCanvasGroup != null)
        {
            hudCanvasGroup.alpha = visible ? 1f : 0f;
            hudCanvasGroup.interactable = visible;
            hudCanvasGroup.blocksRaycasts = visible;
        }

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

    private void LoadBlastRoyaleWeaponIcons()
    {
        swordIcon = LoadWeaponIcon("Hammer") ?? swordIcon;
        rifleIcon = rifleIcon != null ? rifleIcon : LoadWeaponIcon("ModRifle");
        shotgunIcon = shotgunIcon != null ? shotgunIcon : LoadWeaponIcon("ModShotgun");
        burstRifleIcon = burstRifleIcon != null ? burstRifleIcon : LoadWeaponIcon("GunARBurst");
        rebelRifleIcon = rebelRifleIcon != null ? rebelRifleIcon : LoadWeaponIcon("GunARRebel");
        machineGunIcon = machineGunIcon != null ? machineGunIcon : LoadWeaponIcon("ModMachineGun");
        minigunIcon = minigunIcon != null ? minigunIcon : LoadWeaponIcon("ApoMinigun");
        pistolIcon = pistolIcon != null ? pistolIcon : LoadWeaponIcon("ModPistol");
        sniperIcon = sniperIcon != null ? sniperIcon : LoadWeaponIcon("GunSniperHeavy");
        launcherIcon = launcherIcon != null ? launcherIcon : LoadWeaponIcon("ApoRifle");
    }

    private static Sprite LoadWeaponIcon(string resourceName)
    {
        return Resources.Load<Sprite>("WeaponIcons/" + resourceName);
    }

    private static RectTransform CreateRect(string objectName, RectTransform parent, Vector2 size, Vector2 anchoredPosition)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)obj.transform;
        SetupRect(rect, size, anchoredPosition);
        return rect;
    }

    private static Image CreateImage(string objectName, RectTransform parent, Sprite sprite, Color color)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI CreateLabel(string objectName, RectTransform parent, string text, int fontSize, Color color)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        TextMeshProUGUI label = obj.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.raycastTarget = false;
        return label;
    }

    private static void SetupRect(RectTransform rect, Vector2 size, Vector2 anchoredPosition)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static void EnsureGeneratedSprites()
    {
        if (circleSprite != null && softCircleSprite != null)
        {
            return;
        }

        circleSprite = CreateCircleSprite(96, 1f);
        softCircleSprite = CreateCircleSprite(96, 0.85f);
    }

    private static Sprite CreateCircleSprite(int size, float edgeAlpha)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        float radius = (size - 2) * 0.5f;
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        Color solid = Color.white;
        Color clear = new Color(1f, 1f, 1f, 0f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(radius - distance + 1f) * edgeAlpha;
                texture.SetPixel(x, y, alpha > 0f ? new Color(solid.r, solid.g, solid.b, alpha) : clear);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

}
