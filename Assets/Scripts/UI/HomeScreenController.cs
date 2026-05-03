using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

public class HomeScreenController : MonoBehaviour
{
    private UIDocument document;
    private VisualElement root;

    private Label playerNameText;
    private Button playButton;
    private Button settingsButton;
    private Button shopButton;

    // Popups
    public SettingsPopupController settingsController;
    public StoreScreenController storeController;

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

        playerNameText = root.Q<Label>("PlayerName");
        playButton = root.Q<Button>("PlayButton");
        settingsButton = root.Q<Button>("SettingsButton");
        shopButton = root.Q<Button>("ShopButton");

        matchmakingContainer = root.Q<VisualElement>("Matchmaking");
        matchTimer = root.Q<Label>("MatchTimer");
        cancelMatchmaking = root.Q<Button>("CancelMatchmaking");

        // Set player name if available (can fetch from NetworkManager later)
        playerNameText.text = "GUEST"; 

        // Callbacks
        playButton.clicked += OnClickPlay;
        cancelMatchmaking.clicked += OnClickCancel;
        
        settingsButton.clicked += () => {
            if (settingsController != null) settingsController.Show();
        };
        shopButton.clicked += () => {
            if (storeController != null) storeController.Show();
        };

        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnGameStart += GoToGameplay;
        }

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
    }

    private void OnClickPlay()
    {
        matchmakingContainer.RemoveFromClassList("hidden");
        isMatchmaking = true;
        matchmakingStartTime = Time.time;
        timerSchedule.Resume();
        
        _ = NetworkManager.Instance.ConnectAndJoinBattle();
    }

    private void OnClickCancel()
    {
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
        SceneManager.LoadScene("GameplayScene");
    }
}
