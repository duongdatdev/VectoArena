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
    private const int ContractTransactionTimeoutSeconds = 240;

    [Header("Web3 Configuration (Loaded from Config)")]
    private ulong chainId => ConfigManager.Config.chainId;
    private string vecTokenAddress => ConfigManager.Config.vecTokenAddress;
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

            NetworkManager.WalletNonceResponse nonce = await NetworkManager.Instance.GetWalletNonceAsync();
            if (nonce == null || string.IsNullOrEmpty(nonce.nonce) || string.IsNullOrEmpty(nonce.message))
            {
                Debug.LogError("Failed to create wallet verification nonce.");
                await DisconnectWalletSession("Wallet verification nonce failed; clearing unlinked wallet session.");
                return false;
            }

            string signature = await wallet.PersonalSign(nonce.message);
            NetworkManager.WalletVerifyResponse verified = await NetworkManager.Instance.VerifyWalletAsync(address, signature, nonce.nonce);
            if (verified != null && verified.success)
            {
                Debug.Log("Wallet successfully verified and linked to account!");
                await PlayerInventory.LoadFromServer();
                return true;
            }
            else
            {
                Debug.LogError("Failed to verify wallet with backend.");
                await DisconnectWalletSession("Wallet verification failed; clearing unlinked wallet session.");
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

    public void ForgetWalletSession(string reason = null)
    {
        if (!string.IsNullOrEmpty(reason))
        {
            Debug.Log($"[Web3Manager] {reason}");
        }

        PlayerPrefs.DeleteKey(ThirdwebAutoConnectOptionsKey);
        PlayerPrefs.Save();

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

    public async Task<string> EnsureWalletConnectedAsync()
    {
        if (ThirdwebManager.Instance == null)
        {
            throw new System.InvalidOperationException("ThirdwebManager prefab is missing from the scene.");
        }

        if (!ThirdwebManager.Instance.Initialized)
        {
            int timeout = 0;
            while (!ThirdwebManager.Instance.Initialized && timeout < 50)
            {
                await Task.Delay(100);
                timeout++;
            }
        }

        if (ThirdwebManager.Instance.Client == null)
        {
            throw new System.InvalidOperationException("Thirdweb Client is not ready. Check thirdwebClientId in appsettings.json.");
        }

        if (ThirdwebManager.Instance.ActiveWallet == null || !await ThirdwebManager.Instance.ActiveWallet.IsConnected())
        {
            bool connected = await ConnectAndLinkWallet();
            if (!connected)
            {
                throw new System.InvalidOperationException("Wallet connection or backend verification failed.");
            }
        }

        string address = await ThirdwebManager.Instance.ActiveWallet.GetAddress();
        string normalizedAddress = NormalizeWalletAddress(address);
        string linkedWalletAddress = NormalizeWalletAddress(PlayerInventory.LinkedWalletAddress);

        if (string.IsNullOrEmpty(linkedWalletAddress))
        {
            await PlayerInventory.LoadFromServer();
            linkedWalletAddress = NormalizeWalletAddress(PlayerInventory.LinkedWalletAddress);
        }

        if (string.IsNullOrEmpty(linkedWalletAddress))
        {
            throw new System.InvalidOperationException("Wallet is connected but not linked to this account.");
        }

        if (normalizedAddress != linkedWalletAddress)
        {
            await DisconnectWalletSession("Connected wallet does not match this account's linked wallet.");
            throw new System.InvalidOperationException("Connected wallet does not match this account.");
        }

        return address;
    }

    public async Task EnsureCorrectChainAsync(ulong expectedChainId)
    {
        if (ThirdwebManager.Instance == null || ThirdwebManager.Instance.ActiveWallet == null)
        {
            throw new System.InvalidOperationException("Wallet is not connected.");
        }

        try
        {
            await ThirdwebManager.Instance.ActiveWallet.SwitchNetwork(expectedChainId);
        }
        catch (System.Exception e)
        {
            throw new System.InvalidOperationException($"Please switch wallet network to Sepolia ({expectedChainId}).", e);
        }
    }

    public async Task<BigInteger> GetErc20AllowanceAsync(string tokenAddress, string owner, string spender, ulong expectedChainId)
    {
        ValidateAddress(tokenAddress, "VEC token address");
        ValidateAddress(owner, "wallet address");
        ValidateAddress(spender, "NFT contract address");

        ThirdwebContract token = await ThirdwebManager.Instance.GetContract(tokenAddress, expectedChainId);
        return await token.Read<BigInteger>("function allowance(address owner, address spender) view returns (uint256)", owner, spender);
    }

    public async Task<BigInteger> GetErc20BalanceAsync(string tokenAddress, string owner, ulong expectedChainId)
    {
        ValidateAddress(tokenAddress, "VEC token address");
        ValidateAddress(owner, "wallet address");

        ThirdwebContract token = await ThirdwebManager.Instance.GetContract(tokenAddress, expectedChainId);
        return await token.Read<BigInteger>("function balanceOf(address owner) view returns (uint256)", owner);
    }

    public async Task ApproveErc20Async(string tokenAddress, string spender, BigInteger amount, ulong expectedChainId)
    {
        ValidateAddress(tokenAddress, "VEC token address");
        ValidateAddress(spender, "NFT contract address");

        ThirdwebContract token = await ThirdwebManager.Instance.GetContract(tokenAddress, expectedChainId);
        ThirdwebTransactionReceipt receipt = await WaitForTransactionAsync(
            token.Write(
                wallet: ThirdwebManager.Instance.ActiveWallet,
                method: "function approve(address spender, uint256 amount) returns (bool)",
                weiValue: 0,
                parameters: new object[] { spender, amount }
            ),
            "VEC approval"
        );

        EnsureSuccessfulReceipt(receipt, "VEC approval");
    }

    public async Task<string> BuySkinNftAsync(string contractAddress, BigInteger tokenId, ulong expectedChainId)
    {
        ValidateAddress(contractAddress, "NFT contract address");

        ThirdwebContract contract = await ThirdwebManager.Instance.GetContract(contractAddress, expectedChainId);
        ThirdwebTransactionReceipt receipt = await WaitForTransactionAsync(
            contract.Write(
                wallet: ThirdwebManager.Instance.ActiveWallet,
                method: "function buySkin(uint256 tokenId)",
                weiValue: 0,
                parameters: new object[] { tokenId }
            ),
            "NFT purchase"
        );

        EnsureSuccessfulReceipt(receipt, "NFT purchase");
        if (string.IsNullOrWhiteSpace(receipt.TransactionHash))
        {
            throw new System.InvalidOperationException("NFT purchase receipt is missing its transaction hash.");
        }

        return receipt.TransactionHash;
    }

    private async Task<ThirdwebTransactionReceipt> WaitForTransactionAsync(Task<ThirdwebTransactionReceipt> transactionTask, string operationName)
    {
        Task completedTask = await Task.WhenAny(transactionTask, Task.Delay(ContractTransactionTimeoutSeconds * 1000));
        if (completedTask != transactionTask)
        {
            throw new System.TimeoutException($"{operationName} transaction timed out. Check the wallet prompt and network status.");
        }

        return await transactionTask;
    }

    private void EnsureSuccessfulReceipt(ThirdwebTransactionReceipt receipt, string operationName)
    {
        if (receipt == null)
        {
            throw new System.InvalidOperationException($"{operationName} transaction failed.");
        }

        if (receipt.Status != null && receipt.Status.Value == BigInteger.Zero)
        {
            throw new System.InvalidOperationException($"{operationName} transaction reverted.");
        }
    }

    private void ValidateAddress(string address, string label)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new System.InvalidOperationException($"{label} is missing from config or profile.");
        }
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

            if (string.IsNullOrEmpty(vecTokenAddress))
            {
                Debug.LogError("VEC token address is empty. Check vecTokenAddress in appsettings.json.");
                return false;
            }

            if (string.IsNullOrEmpty(treasuryWalletAddress))
            {
                Debug.LogError("Treasury wallet address is empty. Check treasuryWalletAddress in appsettings.json.");
                return false;
            }

            ValidateAddress(vecTokenAddress, "VEC token address");
            ValidateAddress(treasuryWalletAddress, "treasury wallet address");

            await EnsureCorrectChainAsync(this.chainId);

            Debug.Log($"Transferring VEC to {treasuryWalletAddress}...");

            ThirdwebContract token = await ThirdwebManager.Instance.GetContract(vecTokenAddress, this.chainId);
            var transferTask = token.Write(
                wallet: ThirdwebManager.Instance.ActiveWallet,
                method: "function transfer(address to, uint256 amount) returns (bool)",
                weiValue: 0,
                parameters: new object[] { treasuryWalletAddress, weiAmount }
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
