using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using System.Collections;
using System;

public class DeathScreenManager : MonoBehaviour
{
    private UIDocument document;
    private VisualElement deathScreenContainer;
    private VisualElement winCelebrationPanel;
    private VisualElement spectatingPanel;
    private VisualElement resultPanel;
    private Label titleLabel;
    private Label statsLabel;
    private Label subtitleLabel;
    private Label placementValue;
    private Label localPlacementLarge;
    private Label killsValue;
    private Label xpValue;
    private Label vecRewardValue;
    private Label vecRewardValueRow;
    private Label vecRewardHint;
    private Label levelValue;
    private Label levelUpValue;
    private VisualElement vecRewardCard;
    private VisualElement xpBarFill;
    private VisualElement levelRow;
    private Label defeatedByName;
    private Label localPlayerName;
    private Label localPlayerNameRow;
    private Button spectatingLeaveButton;
    private Button returnToLobbyButton;

    private bool isDead = false;
    private bool isReturningToLobby = false;
    private bool isViewingResults = false;
    private bool isCelebratingWin = false;
    private Coroutine winCelebrationRoutine;
    private NetworkPlayerSync localPlayerSync;
    private NetworkManager.MatchResultMessage lastMatchResult;

    private void OnEnable()
    {
        document = GetComponent<UIDocument>();
        if (document != null && document.rootVisualElement != null)
        {
            deathScreenContainer = document.rootVisualElement.Q<VisualElement>("DeathScreenContainer");
            winCelebrationPanel = document.rootVisualElement.Q<VisualElement>("WinCelebrationPanel");
            spectatingPanel = document.rootVisualElement.Q<VisualElement>("SpectatingPanel");
            resultPanel = document.rootVisualElement.Q<VisualElement>("ResultPanel");
            titleLabel = document.rootVisualElement.Q<Label>("DeathTitle");
            statsLabel = document.rootVisualElement.Q<Label>("DeathStats");
            subtitleLabel = document.rootVisualElement.Q<Label>("ResultSubtitle");
            placementValue = document.rootVisualElement.Q<Label>("PlacementValue");
            localPlacementLarge = document.rootVisualElement.Q<Label>("LocalPlacementLarge");
            killsValue = document.rootVisualElement.Q<Label>("KillsValue");
            xpValue = document.rootVisualElement.Q<Label>("XpValue");
            vecRewardValue = document.rootVisualElement.Q<Label>("VecRewardValue");
            vecRewardValueRow = document.rootVisualElement.Q<Label>("VecRewardValueRow");
            vecRewardHint = document.rootVisualElement.Q<Label>("VecRewardHint");
            levelValue = document.rootVisualElement.Q<Label>("LevelValue");
            levelUpValue = document.rootVisualElement.Q<Label>("LevelUpValue");
            vecRewardCard = document.rootVisualElement.Q<VisualElement>("VecRewardCard");
            xpBarFill = document.rootVisualElement.Q<VisualElement>("XpBarFill");
            levelRow = levelValue != null ? levelValue.parent : null;
            defeatedByName = document.rootVisualElement.Q<Label>("DefeatedByName");
            localPlayerName = document.rootVisualElement.Q<Label>("LocalPlayerName");
            localPlayerNameRow = document.rootVisualElement.Q<Label>("LocalPlayerNameRow");
            spectatingLeaveButton = document.rootVisualElement.Q<Button>("SpectatingLeaveBtn");
            returnToLobbyButton = document.rootVisualElement.Q<Button>("ReturnToLobbyBtn");

            if (spectatingLeaveButton != null)
            {
                spectatingLeaveButton.clicked += LeaveSpectating;
            }

            if (returnToLobbyButton != null)
            {
                returnToLobbyButton.clicked += ReturnToLobby;
            }

            if (deathScreenContainer != null)
            {
                deathScreenContainer.style.display = DisplayStyle.None;
            }
        }

        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnGameOver += HandleGameOver;
            NetworkManager.Instance.OnKillFeedReceived += HandleKillFeed;
            NetworkManager.Instance.OnMatchResultReceived += HandleMatchResult;
        }
    }

    private void OnDisable()
    {
        if (spectatingLeaveButton != null)
        {
            spectatingLeaveButton.clicked -= LeaveSpectating;
        }

        if (returnToLobbyButton != null)
        {
            returnToLobbyButton.clicked -= ReturnToLobby;
        }

        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnGameOver -= HandleGameOver;
            NetworkManager.Instance.OnKillFeedReceived -= HandleKillFeed;
            NetworkManager.Instance.OnMatchResultReceived -= HandleMatchResult;
        }
    }

    private void Update()
    {
        if (!isDead && localPlayerSync == null)
        {
            // Try to find the local player
            var syncs = FindObjectsByType<NetworkPlayerSync>(FindObjectsInactive.Exclude);
            foreach (var sync in syncs)
            {
                if (sync.isLocalPlayer)
                {
                    localPlayerSync = sync;
                    break;
                }
            }
        }

        if (!isDead && localPlayerSync != null && localPlayerSync.GetState() != null)
        {
            if (localPlayerSync.GetState().isDead)
            {
                isDead = true;
                ShowSpectatingScreen("");
            }
        }
    }

    private void HandleKillFeed(NetworkManager.KillFeedMessage msg)
    {
        if (localPlayerSync != null && localPlayerSync.GetState() != null)
        {
            if (msg.victimName == localPlayerSync.GetState().username)
            {
                // We got killed by someone.
                SpectatePlayer(msg.killerName);
                if (isDead)
                {
                    if (defeatedByName != null)
                    {
                        defeatedByName.text = msg.killerName;
                    }
                    return;
                }

                isDead = true;
                ShowSpectatingScreen(msg.killerName);
            }
        }
    }

    private void HandleGameOver()
    {
        if (lastMatchResult != null)
        {
            if (!isDead || isViewingResults)
            {
                ShowMatchResultOrCelebrate(lastMatchResult);
            }
            return;
        }

        if (localPlayerSync != null && localPlayerSync.GetState() != null && !localPlayerSync.GetState().isDead)
        {
            // We survived!
            ShowMatchResultOrCelebrate(CreateFallbackResult(true));
        }
        else if (!isDead)
        {
            ShowMatchResultScreen(CreateFallbackResult(false));
        }
    }

    private void HandleMatchResult(NetworkManager.MatchResultMessage result)
    {
        lastMatchResult = result;
        if (!isDead || isViewingResults)
        {
            ShowMatchResultOrCelebrate(result);
        }
    }

    private void ShowMatchResultOrCelebrate(NetworkManager.MatchResultMessage result)
    {
        if (result != null && result.isWinner && !isViewingResults)
        {
            ShowWinCelebration(result);
            return;
        }

        ShowMatchResultScreen(result);
    }

    private void ShowWinCelebration(NetworkManager.MatchResultMessage result)
    {
        if (isCelebratingWin)
        {
            lastMatchResult = result;
            return;
        }

        lastMatchResult = result;
        isCelebratingWin = true;
        if (winCelebrationRoutine != null)
        {
            StopCoroutine(winCelebrationRoutine);
        }

        winCelebrationRoutine = StartCoroutine(WinCelebrationThenResults());
    }

    private IEnumerator WinCelebrationThenResults()
    {
        if (deathScreenContainer != null)
        {
            deathScreenContainer.style.display = DisplayStyle.Flex;
            deathScreenContainer.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));
        }

        if (winCelebrationPanel != null) winCelebrationPanel.style.display = DisplayStyle.Flex;
        if (spectatingPanel != null) spectatingPanel.style.display = DisplayStyle.None;
        if (resultPanel != null) resultPanel.style.display = DisplayStyle.None;

        yield return new WaitForSeconds(5f);

        isCelebratingWin = false;
        winCelebrationRoutine = null;
        ShowMatchResultScreen(lastMatchResult ?? CreateFallbackResult(true));
    }

    private void ShowMatchResultScreen(NetworkManager.MatchResultMessage result)
    {
        isViewingResults = true;
        string title = result.isWinner ? "VICTORY" : "ELIMINATED";
        ShowResultContainer();

        if (titleLabel != null) titleLabel.text = title;
        if (subtitleLabel != null)
        {
            subtitleLabel.text = result.isWinner ? "LAST SURVIVOR BONUS CLAIMED" : "MATCH REWARDS";
        }

        string placementText = result.placement > 0 ? $"{result.placement}." : "--";
        if (placementValue != null) placementValue.text = placementText;
        if (localPlacementLarge != null) localPlacementLarge.text = placementText;
        if (killsValue != null) killsValue.text = result.kills.ToString("N0");
        if (xpValue != null) xpValue.text = $"+{result.xpEarned:N0}";
        if (vecRewardValue != null) vecRewardValue.text = $"+{result.vecEarned:N0}";
        if (vecRewardValueRow != null) vecRewardValueRow.text = $"+{result.vecEarned:N0} VEC";
        if (vecRewardHint != null)
        {
            vecRewardHint.text = result.vecEarned > 0
                ? "Added to your VEC balance when you return"
                : "No VEC earned this match";
        }

        if (levelValue != null)
        {
            levelValue.text = result.level <= 0 ? "LV --" : (result.xpToNextLevel <= 0 ? $"LV {result.level} MAX" : $"LV {result.level}");
        }
        if (levelUpValue != null) levelUpValue.text = result.levelsGained > 0 ? $"LEVEL UP +{result.levelsGained}" : "";
        if (xpBarFill != null)
        {
            float progress = result.xpToNextLevel <= 0 ? 1f : Mathf.Clamp01(result.xpProgress);
            xpBarFill.style.width = Length.Percent(progress * 100f);
        }

        if (returnToLobbyButton != null)
        {
            returnToLobbyButton.SetEnabled(true);
            returnToLobbyButton.text = "NEXT";
        }

        SetLocalPlayerLabels();
        HideCombatHud();
    }

    private void ShowSpectatingScreen(string killerName)
    {
        if (deathScreenContainer == null) return;

        isViewingResults = false;
        deathScreenContainer.style.display = DisplayStyle.Flex;
        deathScreenContainer.style.flexDirection = new StyleEnum<FlexDirection>(FlexDirection.Column);
        deathScreenContainer.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));
        if (winCelebrationPanel != null) winCelebrationPanel.style.display = DisplayStyle.None;
        if (spectatingPanel != null) spectatingPanel.style.display = DisplayStyle.Flex;
        if (resultPanel != null) resultPanel.style.display = DisplayStyle.None;
        if (defeatedByName != null)
        {
            defeatedByName.text = string.IsNullOrEmpty(killerName) ? "YOU WERE" : killerName;
        }
        if (spectatingLeaveButton != null)
        {
            spectatingLeaveButton.SetEnabled(true);
            spectatingLeaveButton.text = "LEAVE";
        }
    }

    private void SpectatePlayer(string playerName)
    {
        if (NetworkManager.Instance == null || string.IsNullOrEmpty(playerName))
        {
            return;
        }

        GameObject targetPlayer = NetworkManager.Instance.FindPlayerObjectByUsername(playerName);
        if (targetPlayer == null)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        CameraFollow cameraFollow = mainCamera.GetComponent<CameraFollow>();
        if (cameraFollow != null)
        {
            cameraFollow.target = targetPlayer.transform;
        }
    }

    private void LeaveSpectating()
    {
        if (spectatingLeaveButton != null)
        {
            spectatingLeaveButton.SetEnabled(false);
            spectatingLeaveButton.text = "LOADING...";
        }

        ShowMatchResultScreen(lastMatchResult ?? CreateFallbackResult(false));
    }

    private void ShowResultContainer()
    {
        if (deathScreenContainer == null) return;

        deathScreenContainer.style.display = DisplayStyle.Flex;
        deathScreenContainer.style.backgroundColor = new StyleColor(new Color(21f / 255f, 99f / 255f, 221f / 255f, 1f));
        if (winCelebrationPanel != null) winCelebrationPanel.style.display = DisplayStyle.None;
        if (spectatingPanel != null) spectatingPanel.style.display = DisplayStyle.None;
        if (resultPanel != null) resultPanel.style.display = DisplayStyle.Flex;
        if (statsLabel != null) statsLabel.style.display = DisplayStyle.None;
        if (vecRewardCard != null) vecRewardCard.style.display = DisplayStyle.Flex;
        if (levelRow != null) levelRow.style.display = DisplayStyle.Flex;
    }

    private NetworkManager.MatchResultMessage CreateFallbackResult(bool isWinner)
    {
        int kills = 0;
        if (localPlayerSync != null && localPlayerSync.GetState() != null)
        {
            kills = Mathf.RoundToInt(localPlayerSync.GetState().kills);
        }

        return new NetworkManager.MatchResultMessage
        {
            placement = isWinner ? 1 : 0,
            kills = kills,
            xpEarned = 0,
            level = 0,
            xp = 0,
            xpToNextLevel = 0,
            xpProgress = 0f,
            levelsGained = 0,
            vecEarned = 0,
            isWinner = isWinner
        };
    }

    private void SetLocalPlayerLabels()
    {
        string playerName = "YOU";
        if (localPlayerSync != null && localPlayerSync.GetState() != null && !string.IsNullOrEmpty(localPlayerSync.GetState().username))
        {
            playerName = localPlayerSync.GetState().username;
        }

        if (localPlayerName != null) localPlayerName.text = playerName;
        if (localPlayerNameRow != null) localPlayerNameRow.text = playerName;
    }

    private void HideCombatHud()
    {
        var zoneUIManager = FindAnyObjectByType<ZoneUIManager>();
        if (zoneUIManager != null)
        {
            var canvas = zoneUIManager.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                canvas.enabled = false;
            }
            else
            {
                zoneUIManager.gameObject.SetActive(false);
            }
        }
        
        var ammoHud = FindAnyObjectByType<AmmoHUDController>();
        if (ammoHud != null)
        {
            ammoHud.gameObject.SetActive(false);
        }
    }

    private void ReturnToLobby()
    {
        if (isReturningToLobby)
        {
            return;
        }

        isReturningToLobby = true;

        if (returnToLobbyButton != null)
        {
            returnToLobbyButton.SetEnabled(false);
            returnToLobbyButton.text = "RETURNING...";
        }

        if (NetworkManager.Instance != null)
        {
            _ = CleanupMatchSession();
        }

        SceneManager.LoadScene("MainScene");
    }

    private async System.Threading.Tasks.Task CleanupMatchSession()
    {
        try
        {
            if (NetworkManager.Instance != null)
            {
                await NetworkManager.Instance.CancelMatchmaking();
                await PlayerInventory.LoadFromServer();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Return to lobby cleanup failed: " + ex.Message);
        }
    }
}
