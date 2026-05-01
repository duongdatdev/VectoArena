using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VectoArena.Schema;

public class AmmoHUDController : MonoBehaviour
{
    [Header("HUD References")]
    [SerializeField] private TextMeshProUGUI ammoText;
    [SerializeField] private TextMeshProUGUI weaponLabel;
    [SerializeField] private Image weaponIcon;
    [SerializeField] private GameObject root;

    [Header("Weapon Icons")]
    [SerializeField] private Sprite swordIcon;
    [SerializeField] private Sprite rifleIcon;
    [SerializeField] private Sprite shotgunIcon;

    [Header("Colors")]
    [SerializeField] private Color readyAmmoColor = new Color(0.95f, 0.66f, 0.16f);
    [SerializeField] private Color emptyAmmoColor = new Color(0.86f, 0.27f, 0.22f);

    private NetworkPlayerSync localPlayerSync;
    private CanvasGroup rootCanvasGroup;
    private bool usesCanvasGroupVisibility;

    private void Awake()
    {
        if (root == null)
        {
            root = gameObject;
        }

        usesCanvasGroupVisibility = root == gameObject;
        if (usesCanvasGroupVisibility)
        {
            rootCanvasGroup = root.GetComponent<CanvasGroup>();
            if (rootCanvasGroup == null)
            {
                rootCanvasGroup = root.AddComponent<CanvasGroup>();
            }
        }
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
        UpdateAmmo(state);
        UpdateWeaponLabel(state.currentWeapon);
        UpdateWeaponIcon(state.currentWeapon);
    }

    private void UpdateAmmo(PlayerState state)
    {
        if (ammoText == null)
        {
            return;
        }

        bool isMeleeWeapon = !string.IsNullOrEmpty(state.currentWeapon) && state.currentWeapon == state.meleeWeapon;
        int ammo = Mathf.Max(0, Mathf.RoundToInt(state.ammo));
        ammoText.text = isMeleeWeapon ? "MELEE" : ammo.ToString();
        ammoText.color = isMeleeWeapon || ammo > 0 ? readyAmmoColor : emptyAmmoColor;
    }

    private void UpdateWeaponLabel(string weaponName)
    {
        if (weaponLabel == null)
        {
            return;
        }

        weaponLabel.text = weaponName.ToUpperInvariant();
    }

    private void UpdateWeaponIcon(string weaponName)
    {
        if (weaponIcon == null)
        {
            return;
        }

        Sprite icon = null;
        if (weaponName == "Sword")
        {
            icon = swordIcon;
        }
        else if (weaponName == "Rifle")
        {
            icon = rifleIcon;
        }
        else if (weaponName == "Shotgun")
        {
            icon = shotgunIcon;
        }

        weaponIcon.sprite = icon;
        weaponIcon.enabled = icon != null;
    }
    private void SetVisible(bool visible)
    {
        if (root == null)
        {
            return;
        }

        if (usesCanvasGroupVisibility && rootCanvasGroup != null)
        {
            rootCanvasGroup.alpha = visible ? 1f : 0f;
            rootCanvasGroup.interactable = visible;
            rootCanvasGroup.blocksRaycasts = visible;
            return;
        }

        if (root.activeSelf != visible)
        {
            root.SetActive(visible);
        }
    }

    private NetworkPlayerSync FindLocalPlayerSync()
    {
        NetworkPlayerSync[] players = FindObjectsByType<NetworkPlayerSync>(FindObjectsSortMode.None);
        foreach (NetworkPlayerSync player in players)
        {
            if (player != null && player.isLocalPlayer)
            {
                return player;
            }
        }

        return null;
    }
}
