using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using System;
using System.Globalization;
using System.Numerics;
using Thirdweb.Unity;

public class HomeScreenController : MonoBehaviour
{
    private UIDocument document;
    private VisualElement root;

    private Label playerNameText;
    private Label playerLevelLabel;
    private Label playerXpLabel;
    private VisualElement xpFill;
    private Button playButton;
    private Button airdropButton;
    private Label airdropLockLabel;
    private Label lockedVecAmountLabel;
    private Button settingsButton;
    private Button shopButton;
    private Button collectionButton;

    // Currencies
    private Label vecAmountLabel;
    private Label coinAmountLabel;
    private Button vecDisplay;

    // Wallet
    private Button walletButton;
    private Label walletLabel;
    private bool isWalletConnected;

    // Deposit Popup
    private VisualElement depositPopup;
    private Button depositCloseButton;
    private Button depositConfirmButton;
    private TextField depositAmountField;
    private Label depositStatus;
    private Label depositLockedVecLabel;
    private Button transactionHistoryButton;
    private Label transactionHistoryStatus;
    private VisualElement transactionHistoryList;

    // Popups
    public SettingsPopupController settingsController;
    public StoreScreenController storeController;
    private CollectionScreenController collectionController;

    // Matchmaking
    private VisualElement matchmakingContainer;
    private Label matchTimer;
    private Button cancelMatchmaking;

    private float matchmakingStartTime;
    private bool isMatchmaking = false;
    private IVisualElementScheduledItem timerSchedule;

    private void OnEnable()
    {
        document = GetComponent<UIDocument>();
        if (document == null) return;

        root = document.rootVisualElement;
        collectionController = new CollectionScreenController(root);
        VectoAudioManager.PlayMainMenuMusic();

        playerNameText = root.Q<Label>("PlayerName");
        playerLevelLabel = root.Q<Label>("PlayerLevel");
        playerXpLabel = root.Q<Label>("PlayerXp");
        xpFill = root.Q<VisualElement>("XpFill");
        playButton = root.Q<Button>("PlayButton");
        airdropButton = root.Q<Button>("GameModeButton");
        airdropLockLabel = root.Q<Label>("AirdropLockLabel");
        lockedVecAmountLabel = root.Q<Label>("LockedVecAmount");
        settingsButton = root.Q<Button>("SettingsButton");
        shopButton = root.Q<Button>("ShopButton");
        collectionButton = root.Q<Button>("CollectionButton");

        // Currency displays
        vecAmountLabel = root.Q<Label>("VecAmount");
        coinAmountLabel = root.Q<Label>("CoinAmount");
        vecDisplay = root.Q<Button>("VecDisplay");

        // Wallet
        walletButton = root.Q<Button>("WalletButton");
        walletLabel = root.Q<Label>("WalletLabel");

        // Deposit Popup
        depositPopup = root.Q<VisualElement>("DepositPopup");
        depositCloseButton = root.Q<Button>("DepositCloseButton");
        depositConfirmButton = root.Q<Button>("DepositConfirmButton");
        depositAmountField = root.Q<TextField>("DepositAmountField");
        depositStatus = root.Q<Label>("DepositStatus");
        depositLockedVecLabel = root.Q<Label>("DepositLockedVec");
        transactionHistoryButton = root.Q<Button>("TransactionHistoryButton");
        transactionHistoryStatus = root.Q<Label>("TransactionHistoryStatus");
        transactionHistoryList = root.Q<VisualElement>("TransactionHistoryList");

        matchmakingContainer = root.Q<VisualElement>("Matchmaking");
        matchTimer = root.Q<Label>("MatchTimer");
        cancelMatchmaking = root.Q<Button>("CancelMatchmaking");

        RefreshPlayerProfileDisplay();

        // Callbacks
        playButton.clicked += OnClickPlay;
        if (airdropButton != null) airdropButton.clicked += OnClickAirdrop;
        cancelMatchmaking.clicked += OnClickCancel;
        
        settingsButton.clicked += () => {
            if (settingsController != null) settingsController.Show();
        };
        shopButton.clicked += () => {
            if (storeController != null) storeController.Show();
        };
        collectionButton.clicked += () => {
            collectionController.Show();
        };

        // Wallet / Web3 callbacks
        walletButton.clicked += OnClickWallet;
        if (vecDisplay != null) vecDisplay.clicked += OnClickVecDeposit;
        if (depositCloseButton != null) depositCloseButton.clicked += CloseDepositPopup;
        if (depositConfirmButton != null) depositConfirmButton.clicked += OnClickDepositConfirm;
        if (transactionHistoryButton != null) transactionHistoryButton.clicked += LoadTransactionHistory;

        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnGameStart += GoToGameplay;
        }

        // Subscribe to inventory changes
        PlayerInventory.Changed += RefreshPlayerProfileDisplay;
        RefreshPlayerProfileDisplay();
        _ = LoadPlayerProfileForMainScene();
        RefreshWalletState();

        // Setup timer
        timerSchedule = matchTimer.schedule.Execute(UpdateTimer).Every(100);
        timerSchedule.Pause();
    }

    private void OnDisable()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnGameStart -= GoToGameplay;
        }

        PlayerInventory.Changed -= RefreshPlayerProfileDisplay;
        collectionController?.Dispose();
    }

    // =============== CURRENCY DISPLAY ===============

    private void RefreshCurrencyDisplay()
    {
        if (vecAmountLabel != null)
            vecAmountLabel.text = FormatCurrency(PlayerInventory.VecUnlockedBalance);
        if (coinAmountLabel != null)
            coinAmountLabel.text = FormatCurrency(PlayerInventory.Coins);
        if (lockedVecAmountLabel != null)
            lockedVecAmountLabel.text = $"LOCKED VEC: {FormatCurrency(PlayerInventory.VecLockedBalance)}";
        if (depositLockedVecLabel != null)
            depositLockedVecLabel.text = $"Locked VEC: {PlayerInventory.VecLockedBalance:N0}";
    }

    private void RefreshPlayerProfileDisplay()
    {
        if (playerNameText != null)
            playerNameText.text = PlayerInventory.Username.ToUpperInvariant();

        if (playerLevelLabel != null)
            playerLevelLabel.text = $"LV {PlayerInventory.Level}";

        if (playerXpLabel != null)
        {
            playerXpLabel.text = PlayerInventory.XpToNextLevel <= 0
                ? "MAX"
                : $"{PlayerInventory.Xp:N0} / {PlayerInventory.XpToNextLevel:N0} XP";
        }

        if (xpFill != null)
            xpFill.style.width = Length.Percent(Mathf.Clamp01(PlayerInventory.XpProgress) * 100f);

        RefreshCurrencyDisplay();
        RefreshAirdropAccess();
    }

    private void RefreshAirdropAccess()
    {
        bool unlocked = PlayerInventory.Level >= 5;
        if (airdropButton != null)
        {
            airdropButton.SetEnabled(unlocked);
        }

        if (airdropLockLabel != null)
        {
            airdropLockLabel.text = unlocked ? "VEC DROPS ENABLED" : "UNLOCKS AT LV 5";
        }
    }

    private async System.Threading.Tasks.Task LoadPlayerProfileForMainScene()
    {
        if (NetworkManager.Instance == null || PlayerInventory.LoadedFromServer)
        {
            return;
        }

        await PlayerInventory.LoadFromServer();
    }

    private string FormatCurrency(int amount)
    {
        if (amount >= 1000000)
            return (amount / 1000000f).ToString("0.#") + "M";
        if (amount >= 1000)
            return (amount / 1000f).ToString("0.#") + "K";
        return amount.ToString("N0");
    }

    // =============== WALLET ===============

    private void RefreshWalletState()
    {
        if (walletButton == null || walletLabel == null) return;

        _ = RefreshWalletStateAsync();
    }

    private async System.Threading.Tasks.Task RefreshWalletStateAsync()
    {
        if (walletButton == null || walletLabel == null) return;

        string linkedWalletAddress = NormalizeWalletAddress(PlayerInventory.LinkedWalletAddress);
        if (ThirdwebManager.Instance == null || ThirdwebManager.Instance.ActiveWallet == null)
        {
            isWalletConnected = false;
            walletButton.RemoveFromClassList("wallet-button--connected");
            walletLabel.text = string.IsNullOrEmpty(linkedWalletAddress) ? "CONNECT" : ShortenWalletAddress(linkedWalletAddress);
            return;
        }

        try
        {
            string activeWalletAddress = NormalizeWalletAddress(await ThirdwebManager.Instance.ActiveWallet.GetAddress());
            if (!string.IsNullOrEmpty(linkedWalletAddress) && activeWalletAddress != linkedWalletAddress)
            {
                isWalletConnected = false;
                walletButton.RemoveFromClassList("wallet-button--connected");
                walletLabel.text = ShortenWalletAddress(linkedWalletAddress);

                if (Web3Manager.Instance != null)
                {
                    await Web3Manager.Instance.DisconnectWalletSession("Active wallet does not match this account's linked wallet.");
                }
                return;
            }

            if (string.IsNullOrEmpty(linkedWalletAddress))
            {
                isWalletConnected = false;
                walletButton.RemoveFromClassList("wallet-button--connected");
                walletLabel.text = "CONNECT";
                return;
            }

            isWalletConnected = true;
            walletButton.AddToClassList("wallet-button--connected");
            walletLabel.text = ShortenWalletAddress(activeWalletAddress);
        }
        catch
        {
            isWalletConnected = false;
            walletButton.RemoveFromClassList("wallet-button--connected");
            walletLabel.text = string.IsNullOrEmpty(linkedWalletAddress) ? "CONNECT" : ShortenWalletAddress(linkedWalletAddress);
        }
    }

    private async System.Threading.Tasks.Task UpdateWalletLabel()
    {
        try
        {
            string address = await ThirdwebManager.Instance.ActiveWallet.GetAddress();
            walletLabel.text = address.Substring(0, 6) + "..." + address.Substring(address.Length - 4);
        }
        catch
        {
            walletLabel.text = "CONNECTED";
        }
    }

    private string NormalizeWalletAddress(string walletAddress)
    {
        return string.IsNullOrWhiteSpace(walletAddress) ? null : walletAddress.Trim().ToLowerInvariant();
    }

    private string ShortenWalletAddress(string walletAddress)
    {
        if (string.IsNullOrEmpty(walletAddress) || walletAddress.Length <= 14)
        {
            return string.IsNullOrEmpty(walletAddress) ? "CONNECT" : walletAddress;
        }

        return walletAddress.Substring(0, 6) + "..." + walletAddress.Substring(walletAddress.Length - 4);
    }

    private async void OnClickWallet()
    {
        VectoAudioManager.Play2D(VectoAudioId.ButtonClickForward);

        if (isWalletConnected)
        {
            // Already connected - open deposit popup
            OpenDepositPopup();
            return;
        }

        // Connect wallet
        if (Web3Manager.Instance == null)
        {
            Debug.LogError("Web3Manager not found in scene.");
            return;
        }

        walletLabel.text = "...";
        bool success = await Web3Manager.Instance.ConnectAndLinkWallet();
        
        RefreshWalletState();
        if (success)
        {
            await PlayerInventory.LoadFromServer();
            RefreshCurrencyDisplay();
        }
    }

    // =============== DEPOSIT POPUP ===============

    private void OnClickVecDeposit()
    {
        VectoAudioManager.Play2D(VectoAudioId.ButtonClickForward);
        if (!isWalletConnected)
        {
            OnClickWallet();
            return;
        }
        OpenDepositPopup();
    }

    private void OpenDepositPopup()
    {
        if (depositPopup == null) return;
        depositPopup.RemoveFromClassList("hidden");
        if (depositStatus != null) depositStatus.text = "";
        if (depositLockedVecLabel != null) depositLockedVecLabel.text = $"Locked VEC: {PlayerInventory.VecLockedBalance:N0}";
        if (depositAmountField != null) depositAmountField.value = "10";
        LoadTransactionHistory();
    }

    private void CloseDepositPopup()
    {
        VectoAudioManager.Play2D(VectoAudioId.ButtonClickBackward);
        if (depositPopup == null) return;
        depositPopup.AddToClassList("hidden");
    }

    private async void OnClickDepositConfirm()
    {
        if (Web3Manager.Instance == null)
        {
            if (depositStatus != null) depositStatus.text = "Web3Manager not found.";
            return;
        }

        string amountText = depositAmountField?.value ?? "0";
        if (!int.TryParse(amountText, out int amount) || amount <= 0)
        {
            if (depositStatus != null) depositStatus.text = "Invalid amount.";
            return;
        }

        if (depositStatus != null) depositStatus.text = "Waiting for wallet confirmation...";
        if (depositConfirmButton != null) depositConfirmButton.SetEnabled(false);

        try
        {
            // Convert to wei (18 decimals): amount * 10^18
            BigInteger weiAmount = BigInteger.Pow(10, 18) * amount;
            bool success = await Web3Manager.Instance.DepositTokens(weiAmount);

            if (success)
            {
                if (depositStatus != null) depositStatus.text = $"Successfully deposited {amount} VEC!";
                RefreshCurrencyDisplay();
                LoadTransactionHistory();
            }
            else
            {
                if (depositStatus != null) depositStatus.text = "Deposit failed. Please try again.";
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Deposit error: " + e.Message);
            if (depositStatus != null) depositStatus.text = "Error: " + e.Message;
        }
        finally
        {
            if (depositConfirmButton != null) depositConfirmButton.SetEnabled(true);
        }
    }

    // =============== TRANSACTION HISTORY ===============

    private async void LoadTransactionHistory()
    {
        if (transactionHistoryList == null || NetworkManager.Instance == null)
        {
            return;
        }

        transactionHistoryList.Clear();
        if (transactionHistoryStatus != null)
        {
            transactionHistoryStatus.text = "LOADING HISTORY...";
        }

        if (transactionHistoryButton != null)
        {
            transactionHistoryButton.SetEnabled(false);
        }

        try
        {
            NetworkManager.TransactionHistoryResponse response = await NetworkManager.Instance.LoadTransactions("VEC", null, 20, 0);
            RenderTransactionHistory(response);
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to load transaction history: " + e.Message);
            if (transactionHistoryStatus != null)
            {
                transactionHistoryStatus.text = "UNABLE TO LOAD HISTORY";
            }
        }
        finally
        {
            if (transactionHistoryButton != null)
            {
                transactionHistoryButton.SetEnabled(true);
            }
        }
    }

    private void RenderTransactionHistory(NetworkManager.TransactionHistoryResponse response)
    {
        transactionHistoryList.Clear();

        if (response?.transactions == null || response.transactions.Length == 0)
        {
            if (transactionHistoryStatus != null)
            {
                transactionHistoryStatus.text = "NO VEC TRANSACTIONS YET";
            }
            return;
        }

        if (transactionHistoryStatus != null)
        {
            transactionHistoryStatus.text = $"{response.transactions.Length} RECENT VEC TRANSACTIONS";
        }

        foreach (NetworkManager.CurrencyTransactionResponse transaction in response.transactions)
        {
            transactionHistoryList.Add(CreateTransactionRow(transaction));
        }
    }

    private VisualElement CreateTransactionRow(NetworkManager.CurrencyTransactionResponse transaction)
    {
        VisualElement row = new VisualElement();
        row.AddToClassList("transaction-row");

        VisualElement mainColumn = new VisualElement();
        mainColumn.AddToClassList("transaction-row__main");

        Label title = new Label(FormatTransactionTitle(transaction));
        title.AddToClassList("transaction-row__title");

        Label meta = new Label(FormatTransactionMeta(transaction));
        meta.AddToClassList("transaction-row__meta");

        Label balance = new Label($"BAL {transaction.balanceBefore:N0} -> {transaction.balanceAfter:N0}");
        balance.AddToClassList("transaction-row__balance");

        mainColumn.Add(title);
        mainColumn.Add(meta);
        mainColumn.Add(balance);

        VisualElement rightColumn = new VisualElement();
        rightColumn.AddToClassList("transaction-row__right");

        Label amount = new Label(FormatTransactionAmount(transaction));
        amount.AddToClassList("transaction-row__amount");
        amount.AddToClassList(transaction.amount >= 0 ? "transaction-row__amount--positive" : "transaction-row__amount--negative");
        rightColumn.Add(amount);

        string explorerUrl = GetExplorerUrl(transaction);
        if (!string.IsNullOrEmpty(explorerUrl))
        {
            Button explorerButton = new Button();
            explorerButton.text = ShortenHash(transaction.txHash);
            explorerButton.AddToClassList("transaction-row__explorer");
            explorerButton.clicked += () => Application.OpenURL(explorerUrl);
            rightColumn.Add(explorerButton);
        }

        row.Add(mainColumn);
        row.Add(rightColumn);
        return row;
    }

    private string FormatTransactionTitle(NetworkManager.CurrencyTransactionResponse transaction)
    {
        string type = string.IsNullOrEmpty(transaction.type) ? "TRANSACTION" : transaction.type.Replace("_", " ");
        string status = string.IsNullOrEmpty(transaction.status) ? "UNKNOWN" : transaction.status.Replace("_", " ");
        return $"{type} - {status}";
    }

    private string FormatTransactionMeta(NetworkManager.CurrencyTransactionResponse transaction)
    {
        string time = FormatTransactionTime(transaction.createdAt);
        if (!string.IsNullOrEmpty(transaction.note))
        {
            return $"{time} - {transaction.note}";
        }

        return time;
    }

    private string FormatTransactionTime(string createdAt)
    {
        if (DateTime.TryParse(createdAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime parsed))
        {
            return parsed.ToLocalTime().ToString("MMM d, HH:mm", CultureInfo.InvariantCulture).ToUpperInvariant();
        }

        return "UNKNOWN TIME";
    }

    private string FormatTransactionAmount(NetworkManager.CurrencyTransactionResponse transaction)
    {
        string sign = transaction.amount > 0 ? "+" : "";
        string currency = string.IsNullOrEmpty(transaction.currencyType) ? "VEC" : transaction.currencyType;
        string bucket = currency == "VEC" && !string.IsNullOrEmpty(transaction.vecBucket)
            ? $" {transaction.vecBucket}"
            : "";
        return $"{sign}{transaction.amount:N0} {currency}{bucket}";
    }

    private string GetExplorerUrl(NetworkManager.CurrencyTransactionResponse transaction)
    {
        if (string.IsNullOrEmpty(transaction.txHash) || !transaction.chainId.HasValue)
        {
            return null;
        }

        switch (transaction.chainId.Value)
        {
            case 11155111:
                return $"https://sepolia.etherscan.io/tx/{transaction.txHash}";
            case 84532:
                return $"https://sepolia.basescan.org/tx/{transaction.txHash}";
            default:
                return null;
        }
    }

    private string ShortenHash(string txHash)
    {
        if (string.IsNullOrEmpty(txHash) || txHash.Length <= 14)
        {
            return txHash;
        }

        return txHash.Substring(0, 6) + "..." + txHash.Substring(txHash.Length - 4);
    }

    // =============== MATCHMAKING ===============

    private void OnClickPlay()
    {
        VectoAudioManager.Play2D(VectoAudioId.EnterGame);
        StartMatchmaking(false);
    }

    private void OnClickAirdrop()
    {
        if (PlayerInventory.Level < 5)
        {
            RefreshAirdropAccess();
            return;
        }

        VectoAudioManager.Play2D(VectoAudioId.EnterGame);
        StartMatchmaking(true);
    }

    private void StartMatchmaking(bool playToAirdrop)
    {
        matchmakingContainer.RemoveFromClassList("hidden");
        isMatchmaking = true;
        matchmakingStartTime = Time.time;
        timerSchedule.Resume();

        if (playToAirdrop)
        {
            _ = NetworkManager.Instance.ConnectAndJoinAirdrop();
            return;
        }

        _ = NetworkManager.Instance.ConnectAndJoinBattle();
    }

    private void OnClickCancel()
    {
        VectoAudioManager.Play2D(VectoAudioId.ButtonClickBackward);
        matchmakingContainer.AddToClassList("hidden");
        isMatchmaking = false;
        timerSchedule.Pause();

        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.CancelMatchmaking();
        }
    }

    private void UpdateTimer()
    {
        if (!isMatchmaking) return;
        
        float elapsed = Time.time - matchmakingStartTime;
        int minutes = (int)(elapsed / 60);
        int seconds = (int)(elapsed % 60);
        matchTimer.text = $"{minutes:00}:{seconds:00}";
    }

    private void GoToGameplay()
    {
        isMatchmaking = false;
        timerSchedule.Pause();
        matchmakingContainer.AddToClassList("hidden");
        VectoAudioManager.PlayBattleMusic();
        SceneManager.LoadScene("GameplayScene");
    }
}
