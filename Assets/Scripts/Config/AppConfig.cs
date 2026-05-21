using System;
using UnityEngine;

[Serializable]
public class AppConfig
{
    [Header("Server Connection")]
    public string serverUrl = "ws://localhost:2567";
    public string httpUrl = "http://localhost:2567";

    [Header("Web3 Configuration")]
    public ulong chainId = 11155111;
    public string tokenContractAddress = "";
    public string treasuryWalletAddress = "";
    public string thirdwebClientId = "";

    [Header("NFT Skin Configuration")]
    public ulong nftChainId = 11155111;
    public string vecTokenAddress = "";
    public string skinNftContractAddress = "";
    public string nftSkinPriceWei = "1000000000000000000";
    public NftSkinConfig[] nftSkins = Array.Empty<NftSkinConfig>();
}

[Serializable]
public class NftSkinConfig
{
    public string skinId;
    public string tokenId;
    public string priceWei;
}
