using System.Numerics;
using System.Threading.Tasks;
using UnityEngine;
using Thirdweb.Unity;
using Thirdweb;
#if THIRDWEB_REOWN
using Reown.AppKit.Unity;
#endif

// Run BEFORE ThirdwebManager's Awake so we can set ClientId in time
[DefaultExecutionOrder(-200)]
public class Web3Manager : MonoBehaviour
{
    public static Web3Manager Instance { get; private set; }
    private const string ThirdwebAutoConnectOptionsKey = "ThirdwebAutoConnectOptions";
    private const int DepositTransactionTimeoutSeconds = 180;

    [Header("Web3 Configuration (Loaded from Config)")]
    private ulong chainId => ConfigManager.Config.chainId;
    private string tokenContractAddress => ConfigManager.Config.tokenContractAddress;
    private string treasuryWalletAddress => ConfigManager.Config.treasuryWalletAddress;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        // Inject ClientId into ThirdwebManager BEFORE it initializes in its own Awake
        InjectClientId();
    }

    /// <summary>
    /// Injects the ClientId from our config into ThirdwebManager via reflection.
    /// This must run before ThirdwebManager.Awake() calls Initialize().
    /// </summary>
    private void InjectClientId()
    {
        string clientId = ConfigManager.Config.thirdwebClientId;
        if (string.IsNullOrEmpty(clientId))
        {
            Debug.LogWarning("[Web3Manager] thirdwebClientId is empty in appsettings.json! Thirdweb will fail to initialize.");
            return;
        }

        // Find ThirdwebManager in scene (it may not have run Awake yet)
        var twManager = FindAnyObjectByType<ThirdwebManager>();
        if (twManager == null)
        {
            Debug.LogError("[Web3Manager] ThirdwebManager prefab not found in scene!");
            return;
        }

        try
        {
            // Try to set ClientId property first
            var clientProp = typeof(ThirdwebManager).GetProperty("ClientId",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (clientProp != null)
            {
                clientProp.SetValue(twManager, clientId);
                Debug.Log($"[Web3Manager] ClientId injected successfully via property.");
            }
            else
            {
                // Fallback to backing field if property not found
                var clientField = typeof(ThirdwebManager).GetField("<ClientId>k__BackingField",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (clientField != null)
                {
                    clientField.SetValue(twManager, clientId);
                    Debug.Log($"[Web3Manager] ClientId injected successfully via field.");
                }
            }

            // Set BundleId
            var bundleProp = typeof(ThirdwebManager).GetProperty("BundleId",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (bundleProp != null)
            {
                bundleProp.SetValue(twManager, Application.identifier);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Web3Manager] Failed to inject ClientId: {e.Message}");
        }
    }

    /// <summary>
    /// Connects to a user's Web3 Wallet and links it to the VectoArena backend.
    /// </summary>
    public async Task<bool> ConnectAndLinkWallet()
    {
        try
        {
            if (ThirdwebManager.Instance == null)
            {
                Debug.LogError("ThirdwebManager prefab is missing from the scene!");
                return false;
            }

            // Wait for initialization if not yet done
            if (!ThirdwebManager.Instance.Initialized)
            {
                Debug.Log("[Web3Manager] Waiting for ThirdwebManager to initialize...");
                int timeout = 0;
                while (!ThirdwebManager.Instance.Initialized && timeout < 50) 
                {
                    await Task.Delay(100);
                    timeout++;
                }
            }

            if (ThirdwebManager.Instance.Client == null)
            {
                Debug.LogError("Thirdweb Client is null. Check your Client ID in appsettings.json!");
                return false;
            }

            Debug.Log($"[Web3Manager] Connecting to Chain: {this.chainId} using ClientID: {ConfigManager.Config.thirdwebClientId}");

            string linkedWalletAddress = NormalizeWalletAddress(PlayerInventory.LinkedWalletAddress);
#if UNITY_EDITOR
            await DisconnectWalletSession("Editor wallet link starts from a clean Reown session.");
#endif

            bool shouldResumeWalletSession = !Application.isEditor && !string.IsNullOrEmpty(linkedWalletAddress);
            var wallet = await ConnectReownWallet(shouldResumeWalletSession);
            string address = await wallet.GetAddress();
            string normalizedAddress = NormalizeWalletAddress(address);

            Debug.Log($"Connected Wallet: {address}");

            if (!string.IsNullOrEmpty(linkedWalletAddress))
            {
                if (normalizedAddress != linkedWalletAddress && shouldResumeWalletSession)
                {
                    await DisconnectWalletSession("Resumed wallet does not match this account; opening a fresh Reown connection.");
                    wallet = await ConnectReownWallet(false);
                    address = await wallet.GetAddress();
                    normalizedAddress = NormalizeWalletAddress(address);
                }

                if (normalizedAddress == linkedWalletAddress)
                {
                    Debug.Log("Connected wallet matches linked account wallet.");
                    return true;
                }

                Debug.LogError("Connected wallet does not match this account's linked wallet.");
                await DisconnectWalletSession("Wrong wallet connected; clearing wallet session.");
                return false;
            }

            bool linked = await NetworkManager.Instance.LinkWallet(normalizedAddress);
            if (linked)
            {
                Debug.Log("Wallet successfully linked to account!");
                await PlayerInventory.LoadFromServer();
                return true;
            }
            else
            {
                Debug.LogError("Failed to link wallet to backend.");
                await DisconnectWalletSession("Wallet link failed; clearing unlinked wallet session.");
                return false;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error connecting wallet: {e.Message}");
            return false;
        }
    }

    private Task<IThirdwebWallet> ConnectReownWallet(bool tryResumeSession)
    {
        // MetaMask wallet ID for Reown (WalletConnect)
        const string METAMASK_WALLET_ID = "c57ca95b47569778a828d19178114f4db188b89b763c899ba0be274e97267d96";

        var walletOptions = new WalletOptions(
            provider: WalletProvider.ReownWallet,
            chainId: this.chainId,
            reownOptions: new ReownOptions(
                singleWalletId: METAMASK_WALLET_ID,
                tryResumeSession: tryResumeSession
            )
        );

        return ThirdwebManager.Instance.ConnectWallet(walletOptions);
    }

    public async Task DisconnectWalletSession(string reason = null)
    {
        if (!string.IsNullOrEmpty(reason))
        {
            Debug.Log($"[Web3Manager] {reason}");
        }

        PlayerPrefs.DeleteKey(ThirdwebAutoConnectOptionsKey);
        PlayerPrefs.Save();

        if (ThirdwebManager.Instance != null && ThirdwebManager.Instance.ActiveWallet != null)
        {
            await ThirdwebManager.Instance.DisconnectWallet();
        }

        await DisconnectReownSessionIfNeeded();
    }

    private async Task DisconnectReownSessionIfNeeded()
    {
#if THIRDWEB_REOWN
        if (!AppKit.IsInitialized || AppKit.ConnectorController == null)
        {
            return;
        }

        if (!AppKit.ConnectorController.IsAccountConnected)
        {
            return;
        }

        try
        {
            Debug.Log("[Web3Manager] Disconnecting existing Reown session before wallet link.");
            await AppKit.ConnectorController.DisconnectAsync();
            AppKit.CloseModal();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Web3Manager] Failed to disconnect existing Reown session: {e.Message}");
        }
#else
        await Task.CompletedTask;
#endif
    }

    private string NormalizeWalletAddress(string walletAddress)
    {
        return string.IsNullOrWhiteSpace(walletAddress) ? null : walletAddress.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Deposits tokens by transferring them to the treasury wallet and notifying the backend.
    /// </summary>
    public async Task<bool> DepositTokens(BigInteger weiAmount)
    {
        try
        {
            if (ThirdwebManager.Instance.ActiveWallet == null)
            {
                Debug.LogError("Wallet is not connected.");
                return false;
            }

            if (string.IsNullOrEmpty(tokenContractAddress))
            {
                Debug.LogError("Token contract address is empty. Check tokenContractAddress in appsettings.json.");
                return false;
            }

            if (string.IsNullOrEmpty(treasuryWalletAddress))
            {
                Debug.LogError("Treasury wallet address is empty. Check treasuryWalletAddress in appsettings.json.");
                return false;
            }

            Debug.Log($"Transferring tokens to {treasuryWalletAddress}...");

            var transferTask = ThirdwebManager.Instance.ActiveWallet.Transfer(
                chainId: this.chainId, 
                toAddress: treasuryWalletAddress, 
                weiAmount: weiAmount, 
                tokenAddress: tokenContractAddress
            );

            var completedTask = await Task.WhenAny(transferTask, Task.Delay(DepositTransactionTimeoutSeconds * 1000));
            if (completedTask != transferTask)
            {
                Debug.LogError($"Deposit transaction timed out after {DepositTransactionTimeoutSeconds} seconds. Check the wallet prompt and network/RPC status.");
                return false;
            }

            var receipt = await transferTask;

            if (receipt != null)
            {
                string txHash = receipt.TransactionHash;
                Debug.Log($"Transfer successful! TxHash: {txHash}");

                bool verified = await NetworkManager.Instance.VerifyDeposit(txHash);
                if (verified)
                {
                    Debug.Log("Deposit verified and credited in-game!");
                    return true;
                }
                else
                {
                    Debug.LogError("Transfer happened but backend verification failed.");
                    return false;
                }
            }
            else
            {
                Debug.LogError("Transfer transaction failed on-chain (receipt was null).");
                return false;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error depositing tokens: {e.Message}");
            return false;
        }
    }
}
