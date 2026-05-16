using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public static class PlayerInventory
{
    public const string DefaultSkinId = "Female01";
    private static readonly HashSet<string> ownedSkins = new HashSet<string> { DefaultSkinId };
    private static int coins;
    private static int vecBalance;
    private static int level = 1;
    private static int xp;
    private static int xpToNextLevel = 100;
    private static float xpProgress;
    private static string equippedSkinId = DefaultSkinId;
    private static string username;
    private static string linkedWalletAddress;
    private static bool loadedFromServer;

    public static event Action Changed;

    public static int Coins => coins;
    public static int VecBalance => vecBalance;
    public static int Level => level;
    public static int Xp => xp;
    public static int XpToNextLevel => xpToNextLevel;
    public static float XpProgress => xpProgress;
    public static string Username => string.IsNullOrWhiteSpace(username) ? "GUEST" : username;
    public static string LinkedWalletAddress => linkedWalletAddress;
    public static bool LoadedFromServer => loadedFromServer;

    public static string EquippedSkinId => string.IsNullOrEmpty(equippedSkinId) ? DefaultSkinId : equippedSkinId;

    public static void EnsureInitialized()
    {
        ownedSkins.Add(DefaultSkinId);
    }

    public static bool IsSkinOwned(string skinId)
    {
        EnsureInitialized();
        return ownedSkins.Contains(skinId);
    }

    public static async Task LoadFromServer()
    {
        if (NetworkManager.Instance == null)
        {
            return;
        }

        try
        {
            ApplyProfile(await NetworkManager.Instance.LoadPlayerProfile());
            loadedFromServer = true;
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to load player inventory: " + ex.Message);
            EnsureInitialized();
            Changed?.Invoke();
        }
    }

    public static async Task<bool> TryBuySkinAsync(SkinCatalogItem item)
    {
        if (!loadedFromServer)
        {
            await LoadFromServer();
        }

        try
        {
            ApplyProfile(await NetworkManager.Instance.BuyPlayerSkin(item.Id));
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to buy skin: " + ex.Message);
            Changed?.Invoke();
            return false;
        }
    }

    public static async Task<bool> EquipSkinAsync(string skinId)
    {
        if (!loadedFromServer)
        {
            await LoadFromServer();
        }

        try
        {
            ApplyProfile(await NetworkManager.Instance.EquipPlayerSkin(skinId));
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to equip skin: " + ex.Message);
            Changed?.Invoke();
            return false;
        }
    }

    private static void ApplyProfile(NetworkManager.PlayerProfileResponse profile)
    {
        if (profile == null)
        {
            return;
        }

        coins = profile.coinBalance;
        vecBalance = profile.vecBalance;
        level = Mathf.Max(1, profile.level);
        xp = Mathf.Max(0, profile.xp);
        xpToNextLevel = Mathf.Max(0, profile.xpToNextLevel);
        xpProgress = Mathf.Clamp01(profile.xpProgress);
        username = profile.username;
        linkedWalletAddress = NormalizeWalletAddress(profile.walletAddress);
        equippedSkinId = string.IsNullOrEmpty(profile.equippedPlayerSkin) ? DefaultSkinId : profile.equippedPlayerSkin;
        ownedSkins.Clear();
        ownedSkins.Add(DefaultSkinId);

        if (profile.ownedSkins != null)
        {
            foreach (string skinId in profile.ownedSkins)
            {
                if (!string.IsNullOrEmpty(skinId))
                {
                    ownedSkins.Add(skinId);
                }
            }
        }

        Changed?.Invoke();
    }

    private static string NormalizeWalletAddress(string walletAddress)
    {
        return string.IsNullOrWhiteSpace(walletAddress) ? null : walletAddress.Trim().ToLowerInvariant();
    }
}
