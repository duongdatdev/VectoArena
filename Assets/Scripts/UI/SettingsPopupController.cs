using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using System.Collections;

public class SettingsPopupController : MonoBehaviour
{
    private UIDocument document;
    private VisualElement root;

    private Button closeButton;
    private Button tabSound;
    private Button tabGraphics;
    private Button tabControls;
    private Button tabAccount;

    private VisualElement panelSound;
    private VisualElement panelGraphics;
    private VisualElement panelControls;
    private VisualElement panelAccount;

    private Slider masterVolumeSlider;
    private Slider musicVolumeSlider;
    private Slider sfxVolumeSlider;
    private Label masterVolumeValue;
    private Label musicVolumeValue;
    private Label sfxVolumeValue;

    private Button qualityPerformanceButton;
    private Button qualityQualityButton;
    private Button fps30Button;
    private Button fps60Button;
    private Button fps120Button;
    private Button fpsUnlimitedButton;
    private Toggle vSyncToggle;
    private Toggle fullscreenToggle;
    private Toggle showFpsToggle;
    private VisualElement vSyncRow;
    private VisualElement fullscreenRow;

    private Slider cameraDistanceSlider;
    private Label cameraDistanceValue;

    private Label accountNameLabel;
    private Label walletStatusLabel;
    private Button logoutButton;
    private bool refreshing;
    private bool logoutInProgress;

    private void OnEnable()
    {
        document = GetComponent<UIDocument>();
        if (document == null) return;
        
        root = document.rootVisualElement;

        // Start hidden
        root.AddToClassList("hidden");

        closeButton = root.Q<Button>("CloseButton");
        tabSound = root.Q<Button>("TabSound");
        tabGraphics = root.Q<Button>("TabGraphics");
        tabControls = root.Q<Button>("TabControls");
        tabAccount = root.Q<Button>("TabAccount");

        panelSound = root.Q<VisualElement>("PanelSound");
        panelGraphics = root.Q<VisualElement>("PanelGraphics");
        panelControls = root.Q<VisualElement>("PanelControls");
        panelAccount = root.Q<VisualElement>("PanelAccount");

        masterVolumeSlider = root.Q<Slider>("MasterVolumeSlider");
        musicVolumeSlider = root.Q<Slider>("MusicVolumeSlider");
        sfxVolumeSlider = root.Q<Slider>("SfxVolumeSlider");
        masterVolumeValue = root.Q<Label>("MasterVolumeValue");
        musicVolumeValue = root.Q<Label>("MusicVolumeValue");
        sfxVolumeValue = root.Q<Label>("SfxVolumeValue");

        qualityPerformanceButton = root.Q<Button>("QualityPerformanceButton");
        qualityQualityButton = root.Q<Button>("QualityQualityButton");
        fps30Button = root.Q<Button>("Fps30Button");
        fps60Button = root.Q<Button>("Fps60Button");
        fps120Button = root.Q<Button>("Fps120Button");
        fpsUnlimitedButton = root.Q<Button>("FpsUnlimitedButton");
        vSyncToggle = root.Q<Toggle>("VSyncToggle");
        fullscreenToggle = root.Q<Toggle>("FullscreenToggle");
        showFpsToggle = root.Q<Toggle>("ShowFpsToggle");
        vSyncRow = root.Q<VisualElement>("VSyncRow");
        fullscreenRow = root.Q<VisualElement>("FullscreenRow");

        cameraDistanceSlider = root.Q<Slider>("CameraDistanceSlider");
        cameraDistanceValue = root.Q<Label>("CameraDistanceValue");

        accountNameLabel = root.Q<Label>("AccountNameLabel");
        walletStatusLabel = root.Q<Label>("WalletStatusLabel");
        logoutButton = root.Q<Button>("LogoutButton");

        UnregisterCallbacks();
        RegisterCallbacks();
        GameSettings.Initialize();
        RefreshUi();
        GameSettings.Changed += RefreshUi;
    }

    private void OnDisable()
    {
        GameSettings.Changed -= RefreshUi;
        UnregisterCallbacks();
    }

    private void RegisterCallbacks()
    {
        closeButton.clicked += Hide;
        
        tabSound.clicked += ShowSoundTab;
        tabGraphics.clicked += ShowGraphicsTab;
        tabControls.clicked += ShowControlsTab;
        tabAccount.clicked += ShowAccountTab;

        masterVolumeSlider.RegisterValueChangedCallback(OnMasterVolumeChanged);
        musicVolumeSlider.RegisterValueChangedCallback(OnMusicVolumeChanged);
        sfxVolumeSlider.RegisterValueChangedCallback(OnSfxVolumeChanged);
        qualityPerformanceButton.clicked += OnQualityPerformance;
        qualityQualityButton.clicked += OnQualityQuality;
        fps30Button.clicked += OnFps30;
        fps60Button.clicked += OnFps60;
        fps120Button.clicked += OnFps120;
        fpsUnlimitedButton.clicked += OnFpsUnlimited;
        vSyncToggle.RegisterValueChangedCallback(OnVSyncChanged);
        fullscreenToggle.RegisterValueChangedCallback(OnFullscreenChanged);
        showFpsToggle.RegisterValueChangedCallback(OnShowFpsChanged);
        cameraDistanceSlider.RegisterValueChangedCallback(OnCameraDistanceChanged);
        logoutButton.clicked += OnLogout;
    }

    private void UnregisterCallbacks()
    {
        if (closeButton != null) closeButton.clicked -= Hide;
        if (tabSound != null) tabSound.clicked -= ShowSoundTab;
        if (tabGraphics != null) tabGraphics.clicked -= ShowGraphicsTab;
        if (tabControls != null) tabControls.clicked -= ShowControlsTab;
        if (tabAccount != null) tabAccount.clicked -= ShowAccountTab;
        if (masterVolumeSlider != null) masterVolumeSlider.UnregisterValueChangedCallback(OnMasterVolumeChanged);
        if (musicVolumeSlider != null) musicVolumeSlider.UnregisterValueChangedCallback(OnMusicVolumeChanged);
        if (sfxVolumeSlider != null) sfxVolumeSlider.UnregisterValueChangedCallback(OnSfxVolumeChanged);
        if (qualityPerformanceButton != null) qualityPerformanceButton.clicked -= OnQualityPerformance;
        if (qualityQualityButton != null) qualityQualityButton.clicked -= OnQualityQuality;
        if (fps30Button != null) fps30Button.clicked -= OnFps30;
        if (fps60Button != null) fps60Button.clicked -= OnFps60;
        if (fps120Button != null) fps120Button.clicked -= OnFps120;
        if (fpsUnlimitedButton != null) fpsUnlimitedButton.clicked -= OnFpsUnlimited;
        if (vSyncToggle != null) vSyncToggle.UnregisterValueChangedCallback(OnVSyncChanged);
        if (fullscreenToggle != null) fullscreenToggle.UnregisterValueChangedCallback(OnFullscreenChanged);
        if (showFpsToggle != null) showFpsToggle.UnregisterValueChangedCallback(OnShowFpsChanged);
        if (cameraDistanceSlider != null) cameraDistanceSlider.UnregisterValueChangedCallback(OnCameraDistanceChanged);
        if (logoutButton != null) logoutButton.clicked -= OnLogout;
    }

    public void Show()
    {
        root.RemoveFromClassList("hidden");
        RefreshUi();
        ShowTab("Sound");
    }

    public void Hide()
    {
        root.AddToClassList("hidden");
    }

    private void ShowTab(string tabName)
    {
        // Reset tabs
        tabSound.RemoveFromClassList("settings-tab-button--active");
        tabGraphics.RemoveFromClassList("settings-tab-button--active");
        tabControls.RemoveFromClassList("settings-tab-button--active");
        tabAccount.RemoveFromClassList("settings-tab-button--active");

        panelSound.AddToClassList("hidden");
        panelGraphics.AddToClassList("hidden");
        panelControls.AddToClassList("hidden");
        panelAccount.AddToClassList("hidden");

        // Set active tab
        if (tabName == "Sound")
        {
            tabSound.AddToClassList("settings-tab-button--active");
            panelSound.RemoveFromClassList("hidden");
        }
        else if (tabName == "Graphics")
        {
            tabGraphics.AddToClassList("settings-tab-button--active");
            panelGraphics.RemoveFromClassList("hidden");
        }
        else if (tabName == "Controls")
        {
            tabControls.AddToClassList("settings-tab-button--active");
            panelControls.RemoveFromClassList("hidden");
        }
        else if (tabName == "Account")
        {
            tabAccount.AddToClassList("settings-tab-button--active");
            panelAccount.RemoveFromClassList("hidden");
        }
    }

    private void RefreshUi()
    {
        if (root == null)
        {
            return;
        }

        refreshing = true;
        masterVolumeSlider.SetValueWithoutNotify(GameSettings.MasterVolume);
        musicVolumeSlider.SetValueWithoutNotify(GameSettings.MusicVolume);
        sfxVolumeSlider.SetValueWithoutNotify(GameSettings.SfxVolume);
        cameraDistanceSlider.SetValueWithoutNotify(GameSettings.CameraDistance);
        vSyncToggle.SetValueWithoutNotify(GameSettings.VSync);
        fullscreenToggle.SetValueWithoutNotify(GameSettings.Fullscreen);
        showFpsToggle.SetValueWithoutNotify(GameSettings.ShowFps);
        refreshing = false;

        masterVolumeValue.text = ToPercent(GameSettings.MasterVolume);
        musicVolumeValue.text = ToPercent(GameSettings.MusicVolume);
        sfxVolumeValue.text = ToPercent(GameSettings.SfxVolume);
        cameraDistanceValue.text = $"{GameSettings.CameraDistance:0.00}x";

        SetActiveChoice(qualityPerformanceButton, GameSettings.QualityPreset == GameQualityPreset.Performance);
        SetActiveChoice(qualityQualityButton, GameSettings.QualityPreset == GameQualityPreset.Quality);
        SetActiveChoice(fps30Button, GameSettings.FpsLimit == 30);
        SetActiveChoice(fps60Button, GameSettings.FpsLimit == 60);
        SetActiveChoice(fps120Button, GameSettings.FpsLimit == 120);
        SetActiveChoice(fpsUnlimitedButton, GameSettings.FpsLimit == 0);

        if (vSyncRow != null)
        {
            vSyncRow.SetEnabled(GameSettings.SupportsVSync);
            vSyncRow.EnableInClassList("settings-row--disabled", !GameSettings.SupportsVSync);
        }

        if (fullscreenRow != null)
        {
            fullscreenRow.SetEnabled(GameSettings.SupportsFullscreen);
            fullscreenRow.EnableInClassList("settings-row--disabled", !GameSettings.SupportsFullscreen);
        }

        RefreshAccountInfo();
    }

    private void OnLogout()
    {
        if (logoutInProgress)
        {
            return;
        }

        logoutInProgress = true;
        StartCoroutine(LogoutRoutine());
    }

    private IEnumerator LogoutRoutine()
    {
        VectoAudioManager.Play2D(VectoAudioId.ButtonClickBackward);

        if (logoutButton != null)
        {
            logoutButton.SetEnabled(false);
        }

        yield return null;

        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.LogoutLocal(false);
        }

        if (Web3Manager.Instance != null)
        {
            Web3Manager.Instance.ForgetWalletSession("Logging out; forgetting wallet session.");
        }

        PlayerInventory.ResetToGuest();

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync("AuthScene", LoadSceneMode.Single);

        if (loadOperation == null)
        {
            SceneManager.LoadScene("AuthScene");
            yield break;
        }

        while (!loadOperation.isDone)
        {
            yield return null;
        }
    }

    private void ShowSoundTab() => ShowTab("Sound");
    private void ShowGraphicsTab() => ShowTab("Graphics");
    private void ShowControlsTab() => ShowTab("Controls");
    private void ShowAccountTab() => ShowTab("Account");

    private void OnMasterVolumeChanged(ChangeEvent<float> evt)
    {
        if (refreshing) return;
        GameSettings.SetMasterVolume(evt.newValue);
    }

    private void OnMusicVolumeChanged(ChangeEvent<float> evt)
    {
        if (refreshing) return;
        GameSettings.SetMusicVolume(evt.newValue);
    }

    private void OnSfxVolumeChanged(ChangeEvent<float> evt)
    {
        if (refreshing) return;
        GameSettings.SetSfxVolume(evt.newValue);
    }

    private void OnCameraDistanceChanged(ChangeEvent<float> evt)
    {
        if (refreshing) return;
        GameSettings.SetCameraDistance(evt.newValue);
    }

    private void OnVSyncChanged(ChangeEvent<bool> evt)
    {
        if (refreshing) return;
        GameSettings.SetVSync(evt.newValue);
    }

    private void OnFullscreenChanged(ChangeEvent<bool> evt)
    {
        if (refreshing) return;
        GameSettings.SetFullscreen(evt.newValue);
    }

    private void OnShowFpsChanged(ChangeEvent<bool> evt)
    {
        if (refreshing) return;
        GameSettings.SetShowFps(evt.newValue);
    }

    private void OnQualityPerformance() => GameSettings.SetQualityPreset(GameQualityPreset.Performance);
    private void OnQualityQuality() => GameSettings.SetQualityPreset(GameQualityPreset.Quality);
    private void OnFps30() => GameSettings.SetFpsLimit(30);
    private void OnFps60() => GameSettings.SetFpsLimit(60);
    private void OnFps120() => GameSettings.SetFpsLimit(120);
    private void OnFpsUnlimited() => GameSettings.SetFpsLimit(0);

    private void RefreshAccountInfo()
    {
        if (accountNameLabel != null)
        {
            accountNameLabel.text = $"Logged in as {PlayerInventory.Username}";
        }

        if (walletStatusLabel != null)
        {
            walletStatusLabel.text = string.IsNullOrEmpty(PlayerInventory.LinkedWalletAddress)
                ? "Wallet not linked"
                : $"Wallet {ShortenWallet(PlayerInventory.LinkedWalletAddress)}";
        }
    }

    private static void SetActiveChoice(Button button, bool active)
    {
        button.EnableInClassList("settings-choice-button--active", active);
    }

    private static string ToPercent(float value)
    {
        return $"{Mathf.RoundToInt(value * 100f)}%";
    }

    private static string ShortenWallet(string walletAddress)
    {
        if (string.IsNullOrEmpty(walletAddress) || walletAddress.Length <= 14)
        {
            return walletAddress;
        }

        return walletAddress.Substring(0, 6) + "..." + walletAddress.Substring(walletAddress.Length - 4);
    }

}
