using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public static class PlayerInventory
{
    public const string DefaultSkinId = "Female01";
    private static readonly HashSet<string> ownedSkins = new HashSet<string> { DefaultSkinId };
    private static readonly Dictionary<string, SkinOwnershipState> skinStates = new Dictionary<string, SkinOwnershipState>(StringComparer.OrdinalIgnoreCase);
    private static int coins;
    private static int vecUnlockedBalance;
    private static int vecLockedBalance;
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
    public static int VecUnlockedBalance => vecUnlockedBalance;
    public static int VecLockedBalance => vecLockedBalance;
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
        if (!skinStates.ContainsKey(DefaultSkinId))
        {
            SkinCatalogItem defaultSkin = SkinCatalog.GetById(DefaultSkinId);
            skinStates[DefaultSkinId] = CreateFallbackState(defaultSkin);
        }
    }

    public static void ResetToGuest()
    {
        coins = 0;
        vecUnlockedBalance = 0;
        vecLockedBalance = 0;
        level = 1;
        xp = 0;
        xpToNextLevel = 100;
        xpProgress = 0f;
        username = null;
        linkedWalletAddress = null;
        equippedSkinId = DefaultSkinId;
        loadedFromServer = false;
        ownedSkins.Clear();
        ownedSkins.Add(DefaultSkinId);
        skinStates.Clear();
        SkinCatalogItem defaultSkin = SkinCatalog.GetById(DefaultSkinId);
        skinStates[DefaultSkinId] = CreateFallbackState(defaultSkin);
        Changed?.Invoke();
    }

    public static bool IsSkinOwned(string skinId)
    {
        EnsureInitialized();
        return ownedSkins.Contains(skinId);
    }

    public static SkinOwnershipState GetSkinState(SkinCatalogItem item)
    {
        EnsureInitialized();
        if (item == null)
        {
            return null;
        }

        if (skinStates.TryGetValue(item.Id, out SkinOwnershipState state))
        {
            return state;
        }

        state = CreateFallbackState(item);
        skinStates[item.Id] = state;
        return state;
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

        SkinOwnershipState state = GetSkinState(item);
        if (state != null && state.IsNft)
        {
            Changed?.Invoke();
            return false;
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
        vecUnlockedBalance = profile.vecUnlockedBalance;
        vecLockedBalance = profile.vecLockedBalance;
        level = Mathf.Max(1, profile.level);
        xp = Mathf.Max(0, profile.xp);
        xpToNextLevel = Mathf.Max(0, profile.xpToNextLevel);
        xpProgress = Mathf.Clamp01(profile.xpProgress);
        username = profile.username;
        linkedWalletAddress = NormalizeWalletAddress(profile.walletAddress);
        equippedSkinId = string.IsNullOrEmpty(profile.equippedPlayerSkin) ? DefaultSkinId : profile.equippedPlayerSkin;
        ownedSkins.Clear();
        ownedSkins.Add(DefaultSkinId);
        skinStates.Clear();

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

        ApplySkinResponses(profile.shopSkins);
        ApplySkinResponses(profile.skinOwnership);

        foreach (SkinCatalogItem item in SkinCatalog.Items)
        {
            if (!skinStates.ContainsKey(item.Id))
            {
                skinStates[item.Id] = CreateFallbackState(item);
            }
        }

        Changed?.Invoke();
    }

    private static void ApplySkinResponses(NetworkManager.ShopSkinResponse[] skins)
    {
        if (skins == null)
        {
            return;
        }

        foreach (NetworkManager.ShopSkinResponse skin in skins)
        {
            if (skin == null)
            {
                continue;
            }

            string skinId = !string.IsNullOrEmpty(skin.skinId) ? skin.skinId : skin.id;
            if (string.IsNullOrEmpty(skinId))
            {
                continue;
            }

            SkinCatalogItem catalogItem = SkinCatalog.GetById(skinId);
            string ownershipType = string.IsNullOrEmpty(skin.ownershipType) ? catalogItem.OwnershipType : skin.ownershipType;
            bool isNft = IsNftOwnership(ownershipType);
            bool owned = isNft ? skin.owned : skin.owned || ownedSkins.Contains(skinId);
            bool canEquip = isNft ? skin.canEquip : skin.canEquip || owned;
            if (owned)
            {
                ownedSkins.Add(skinId);
            }

            skinStates[skinId] = new SkinOwnershipState
            {
                Id = skinId,
                DisplayName = string.IsNullOrEmpty(skin.displayName) ? catalogItem.DisplayName : skin.displayName,
                PrefabKey = string.IsNullOrEmpty(skin.prefabKey) ? catalogItem.PrefabKey : skin.prefabKey,
                Price = skin.price,
                CurrencyType = string.IsNullOrEmpty(skin.currencyType) ? catalogItem.CurrencyType : skin.currencyType,
                OwnershipType = ownershipType,
                Owned = owned,
                CanEquip = canEquip,
                Source = skin.source,
                Equipped = skin.equipped || EquippedSkinId == skinId,
                NftInfo = skin.nftInfo,
                Nft = skin.nft
            };
        }
    }

    private static SkinOwnershipState CreateFallbackState(SkinCatalogItem item)
    {
        bool owned = ownedSkins.Contains(item.Id);
        return new SkinOwnershipState
        {
            Id = item.Id,
            DisplayName = item.DisplayName,
            PrefabKey = item.PrefabKey,
            Price = item.Price,
            CurrencyType = item.CurrencyType,
            OwnershipType = string.IsNullOrEmpty(item.OwnershipType) ? "OFFCHAIN" : item.OwnershipType,
            Owned = owned,
            CanEquip = owned,
            Source = owned ? "LOCAL" : "NONE",
            Equipped = EquippedSkinId == item.Id,
            Nft = item.Nft == null ? null : new NetworkManager.SkinNftMappingResponse
            {
                chainId = item.Nft.ChainId,
                contractAddress = item.Nft.ContractAddress,
                tokenId = item.Nft.TokenId,
                collectionKey = item.Nft.CollectionKey
            }
        };
    }

    private static bool IsNftOwnership(string ownershipType)
    {
        return string.Equals(ownershipType, "NFT", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeWalletAddress(string walletAddress)
    {
        return string.IsNullOrWhiteSpace(walletAddress) ? null : walletAddress.Trim().ToLowerInvariant();
    }
}

public class SkinOwnershipState
{
    public string Id;
    public string DisplayName;
    public string PrefabKey;
    public int Price;
    public string CurrencyType;
    public string OwnershipType;
    public bool Owned;
    public bool CanEquip;
    public string Source;
    public bool Equipped;
    public NetworkManager.SkinNftMappingResponse Nft;
    public NetworkManager.NftSkinInfoResponse NftInfo;

    public bool IsNft => string.Equals(OwnershipType, "NFT", StringComparison.OrdinalIgnoreCase);
}
