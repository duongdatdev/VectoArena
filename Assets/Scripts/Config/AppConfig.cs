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
}
