using System;
using UnityEngine;

public static class PlayerInventory
{
    public const string DefaultSkinId = "Female01";
    private const string CoinsKey = "vecto_coins";
    private const string EquippedSkinKey = "vecto_equipped_skin";
    private const string OwnedSkinPrefix = "vecto_owned_skin_";
    private const int StartingCoins = 1500;

    public static event Action Changed;

    public static int Coins
    {
        get
        {
            EnsureInitialized();
            return PlayerPrefs.GetInt(CoinsKey, StartingCoins);
        }
    }

    public static string EquippedSkinId
    {
        get
        {
            EnsureInitialized();
            return PlayerPrefs.GetString(EquippedSkinKey, DefaultSkinId);
        }
    }

    public static void EnsureInitialized()
    {
        if (!PlayerPrefs.HasKey(CoinsKey))
        {
            PlayerPrefs.SetInt(CoinsKey, StartingCoins);
        }

        if (!PlayerPrefs.HasKey(OwnedSkinPrefix + DefaultSkinId))
        {
            PlayerPrefs.SetInt(OwnedSkinPrefix + DefaultSkinId, 1);
        }

        if (!PlayerPrefs.HasKey(EquippedSkinKey))
        {
            PlayerPrefs.SetString(EquippedSkinKey, DefaultSkinId);
        }

        PlayerPrefs.Save();
    }

    public static bool IsSkinOwned(string skinId)
    {
        EnsureInitialized();
        return PlayerPrefs.GetInt(OwnedSkinPrefix + skinId, 0) == 1;
    }

    public static bool TryBuySkin(SkinCatalogItem item)
    {
        EnsureInitialized();

        if (IsSkinOwned(item.Id))
        {
            EquipSkin(item.Id);
            return true;
        }

        if (Coins < item.Price)
        {
            return false;
        }

        PlayerPrefs.SetInt(CoinsKey, Coins - item.Price);
        PlayerPrefs.SetInt(OwnedSkinPrefix + item.Id, 1);
        PlayerPrefs.SetString(EquippedSkinKey, item.Id);
        PlayerPrefs.Save();
        Changed?.Invoke();
        return true;
    }

    public static void EquipSkin(string skinId)
    {
        EnsureInitialized();

        if (!IsSkinOwned(skinId))
        {
            return;
        }

        PlayerPrefs.SetString(EquippedSkinKey, skinId);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
