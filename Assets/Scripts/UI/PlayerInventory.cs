using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using UnityEngine;

public static class PlayerInventory
{
    public const string DefaultSkinId = "Female01";
    private const string PendingNftPurchaseKeyPrefix = "nft.pendingPurchase.";
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
    private static string lastOperationError;

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
    public static string LastOperationError => lastOperationError;

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
        lastOperationError = null;
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
            lastOperationError = null;
            ApplyProfile(await NetworkManager.Instance.BuyPlayerSkin(item.Id));
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to buy skin: " + ex.Message);
            lastOperationError = ex.Message;
            Changed?.Invoke();
            return false;
        }
    }

    public static async Task<bool> TryBuyNftSkinAsync(SkinCatalogItem item)
    {
        return await TryBuyNftSkinAsync(item, null);
    }

    public static async Task<bool> TryBuyNftSkinAsync(SkinCatalogItem item, Action<string> setStatus)
    {
        lastOperationError = null;

        if (item == null)
        {
            lastOperationError = "Invalid NFT skin.";
            Changed?.Invoke();
            return false;
        }

        if (!loadedFromServer)
        {
            await LoadFromServer();
        }

        SkinOwnershipState state = GetSkinState(item);
        if (state == null || !state.IsNft)
        {
            lastOperationError = "This skin is not an NFT skin.";
            Changed?.Invoke();
            return false;
        }

        if (state.Owned && state.CanEquip)
        {
            return await EquipSkinAsync(item.Id);
        }

        if (Web3Manager.Instance == null)
        {
            lastOperationError = "Web3Manager not found.";
            Changed?.Invoke();
            return false;
        }

        if (NetworkManager.Instance == null)
        {
            lastOperationError = "NetworkManager not found.";
            Changed?.Invoke();
            return false;
        }

        string walletAddress;
        ulong chainId;
        string vecTokenAddress;
        string nftContractAddress;
        BigInteger tokenId;
        BigInteger priceWei;
        string purchaseTxHash;

        try
        {
            ResolveNftPurchaseConfig(item, state, out chainId, out vecTokenAddress, out nftContractAddress, out tokenId, out priceWei);

            setStatus?.Invoke("CONNECTING WALLET");
            walletAddress = await Web3Manager.Instance.EnsureWalletConnectedAsync();

            string pendingPurchaseKey = GetPendingNftPurchaseKey(item.Id);
            string pendingTxHash = PlayerPrefs.GetString(pendingPurchaseKey, null);
            if (!string.IsNullOrWhiteSpace(pendingTxHash))
            {
                setStatus?.Invoke("CONFIRMING PURCHASE");
                try
                {
                    await NetworkManager.Instance.ConfirmNftPurchaseAsync(item.Id, pendingTxHash);
                    PlayerPrefs.DeleteKey(pendingPurchaseKey);
                    PlayerPrefs.Save();
                    await LoadFromServer();
                    return IsSkinOwned(item.Id) && GetSkinState(item)?.CanEquip == true;
                }
                catch (Exception confirmEx)
                {
                    Debug.LogWarning("Pending NFT purchase could not be confirmed yet: " + confirmEx.Message);
                }
            }

            setStatus?.Invoke("CHECKING OWNERSHIP");
            await NetworkManager.Instance.SyncNftOwnershipAsync();
            await LoadFromServer();
            SkinOwnershipState refreshedState = GetSkinState(item);
            if (refreshedState != null && refreshedState.Owned && refreshedState.CanEquip)
            {
                PlayerPrefs.DeleteKey(pendingPurchaseKey);
                PlayerPrefs.Save();
                setStatus?.Invoke("NFT OWNED");
                return await EquipSkinAsync(item.Id);
            }

            setStatus?.Invoke("CHECKING NETWORK");
            await Web3Manager.Instance.EnsureCorrectChainAsync(chainId);

            BigInteger balance = await Web3Manager.Instance.GetErc20BalanceAsync(vecTokenAddress, walletAddress, chainId);
            if (balance < priceWei)
            {
                lastOperationError = "Insufficient VEC.";
                Changed?.Invoke();
                return false;
            }

            BigInteger allowance = await Web3Manager.Instance.GetErc20AllowanceAsync(vecTokenAddress, walletAddress, nftContractAddress, chainId);
            if (allowance < priceWei)
            {
                setStatus?.Invoke("APPROVING VEC");
                await Web3Manager.Instance.ApproveErc20Async(vecTokenAddress, nftContractAddress, priceWei, chainId);
            }

            setStatus?.Invoke("BUYING NFT");
            purchaseTxHash = await Web3Manager.Instance.BuySkinNftAsync(nftContractAddress, tokenId, chainId);
            PlayerPrefs.SetString(GetPendingNftPurchaseKey(item.Id), purchaseTxHash);
            PlayerPrefs.Save();
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to buy NFT skin: " + ex.Message);
            lastOperationError = GetFriendlyNftError(ex);
            Changed?.Invoke();
            return false;
        }

        try
        {
            setStatus?.Invoke("CONFIRMING PURCHASE");
            await NetworkManager.Instance.ConfirmNftPurchaseAsync(item.Id, purchaseTxHash);
            PlayerPrefs.DeleteKey(GetPendingNftPurchaseKey(item.Id));
            PlayerPrefs.Save();
            await LoadFromServer();
            lastOperationError = null;
            return IsSkinOwned(item.Id) && GetSkinState(item)?.CanEquip == true;
        }
        catch (Exception ex)
        {
            setStatus?.Invoke("RECOVERING OWNERSHIP");

            try
            {
                await NetworkManager.Instance.SyncNftOwnershipAsync();
                await LoadFromServer();
                if (IsSkinOwned(item.Id) && GetSkinState(item)?.CanEquip == true)
                {
                    PlayerPrefs.DeleteKey(GetPendingNftPurchaseKey(item.Id));
                    PlayerPrefs.Save();
                    lastOperationError = null;
                    Debug.LogWarning("NFT purchase was recovered through on-chain ownership sync after confirmation failed: " + ex.Message);
                    return true;
                }
            }
            catch (Exception syncEx)
            {
                Debug.LogError("NFT ownership recovery failed: " + syncEx.Message);
            }

            Debug.LogError("NFT purchase completed on-chain but backend confirmation failed: " + ex.Message);
            lastOperationError = "Purchase completed on-chain, but confirmation is pending. Retry to refresh ownership.";
            Changed?.Invoke();
            return false;
        }
    }

    private static string GetPendingNftPurchaseKey(string skinId)
    {
        return PendingNftPurchaseKeyPrefix + skinId;
    }

    public static async Task<bool> EquipSkinAsync(string skinId)
    {
        if (!loadedFromServer)
        {
            await LoadFromServer();
        }

        try
        {
            lastOperationError = null;
            ApplyProfile(await NetworkManager.Instance.EquipPlayerSkin(skinId));
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to equip skin: " + ex.Message);
            lastOperationError = ex.Message;
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

    private static void ResolveNftPurchaseConfig(
        SkinCatalogItem item,
        SkinOwnershipState state,
        out ulong chainId,
        out string vecTokenAddress,
        out string nftContractAddress,
        out BigInteger tokenId,
        out BigInteger priceWei)
    {
        AppConfig config = ConfigManager.Config;
        NetworkManager.NftSkinInfoResponse nftInfo = state.NftInfo;
        NetworkManager.SkinNftMappingResponse nft = state.Nft;
        NftSkinConfig configuredSkin = GetConfiguredNftSkin(item.Id);

        int? profileChainId = nftInfo?.chainId ?? nft?.chainId;
        chainId = profileChainId.HasValue && profileChainId.Value > 0 ? (ulong)profileChainId.Value : config.nftChainId;

        nftContractAddress = FirstNonEmpty(nftInfo?.contractAddress, nft?.contractAddress, config.skinNftContractAddress);
        vecTokenAddress = FirstNonEmpty(config.vecTokenAddress, config.tokenContractAddress);

        string tokenIdText = FirstNonEmpty(nftInfo?.tokenId, nft?.tokenId, configuredSkin?.tokenId);

        if (chainId == 0)
        {
            throw new InvalidOperationException("NFT chainId is missing.");
        }

        if (string.IsNullOrWhiteSpace(nftContractAddress))
        {
            throw new InvalidOperationException("NFT contract address is missing.");
        }

        if (string.IsNullOrWhiteSpace(vecTokenAddress))
        {
            throw new InvalidOperationException("VEC token address is missing.");
        }

        if (!BigInteger.TryParse(tokenIdText, out tokenId))
        {
            throw new InvalidOperationException("NFT tokenId is missing or invalid.");
        }

        priceWei = ResolveNftPriceWei(item, state, configuredSkin, config);
        if (priceWei <= BigInteger.Zero)
        {
            throw new InvalidOperationException("NFT price is missing or invalid.");
        }
    }

    private static BigInteger ResolveNftPriceWei(
        SkinCatalogItem item,
        SkinOwnershipState state,
        NftSkinConfig configuredSkin,
        AppConfig config)
    {
        int vecPrice = state != null && state.Price > 0 ? state.Price : item.Price;
        if (vecPrice > 0)
        {
            return BigInteger.Pow(10, 18) * vecPrice;
        }

        string priceWeiText = FirstNonEmpty(configuredSkin?.priceWei, config.nftSkinPriceWei);
        return BigInteger.TryParse(priceWeiText, out BigInteger configuredPriceWei)
            ? configuredPriceWei
            : BigInteger.Zero;
    }

    private static NftSkinConfig GetConfiguredNftSkin(string skinId)
    {
        NftSkinConfig[] configuredSkins = ConfigManager.Config.nftSkins;
        if (configuredSkins == null)
        {
            return null;
        }

        foreach (NftSkinConfig configuredSkin in configuredSkins)
        {
            if (configuredSkin != null && string.Equals(configuredSkin.skinId, skinId, StringComparison.OrdinalIgnoreCase))
            {
                return configuredSkin;
            }
        }

        return null;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (string value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string GetFriendlyNftError(Exception ex)
    {
        string message = ex.Message ?? string.Empty;
        string lower = message.ToLowerInvariant();

        if (lower.Contains("insufficient"))
        {
            return "Insufficient VEC.";
        }

        if (lower.Contains("user rejected") || lower.Contains("denied") || lower.Contains("cancel"))
        {
            return "Wallet request was rejected.";
        }

        if (lower.Contains("network") || lower.Contains("chain") || lower.Contains("sepolia"))
        {
            return "Please switch wallet network to Sepolia.";
        }

        if (lower.Contains("wallet"))
        {
            return message;
        }

        if (lower.Contains("revert") || lower.Contains("failed"))
        {
            return "Transaction failed.";
        }

        return string.IsNullOrWhiteSpace(message) ? "NFT purchase failed." : message;
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
