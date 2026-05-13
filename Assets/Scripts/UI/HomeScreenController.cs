using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
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

        matchmakingContainer = root.Q<VisualElement>("Matchmaking");
        matchTimer = root.Q<Label>("MatchTimer");
        cancelMatchmaking = root.Q<Button>("CancelMatchmaking");

        RefreshPlayerProfileDisplay();

        // Callbacks
        playButton.clicked += OnClickPlay;
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
            vecAmountLabel.text = FormatCurrency(PlayerInventory.VecBalance);
        if (coinAmountLabel != null)
            coinAmountLabel.text = FormatCurrency(PlayerInventory.Coins);
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

        // Check if Thirdweb wallet is connected
        if (ThirdwebManager.Instance != null && ThirdwebManager.Instance.ActiveWallet != null)
        {
            isWalletConnected = true;
            walletButton.AddToClassList("wallet-button--connected");
            // Show truncated address
            _ = UpdateWalletLabel();
        }
        else
        {
            isWalletConnected = false;
            walletButton.RemoveFromClassList("wallet-button--connected");
            walletLabel.text = "CONNECT";
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
        if (depositAmountField != null) depositAmountField.value = "10";
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

    // =============== MATCHMAKING ===============

    private void OnClickPlay()
    {
        VectoAudioManager.Play2D(VectoAudioId.EnterGame);
        matchmakingContainer.RemoveFromClassList("hidden");
        isMatchmaking = true;
        matchmakingStartTime = Time.time;
        timerSchedule.Resume();
        
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
