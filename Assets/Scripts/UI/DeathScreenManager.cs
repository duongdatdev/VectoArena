using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using System.Collections;
using System;

public class DeathScreenManager : MonoBehaviour
{
    private UIDocument document;
    private VisualElement deathScreenContainer;
    private Label titleLabel;
    private Label statsLabel;
    private Button returnToLobbyButton;

    private bool isDead = false;
    private NetworkPlayerSync localPlayerSync;

    private void OnEnable()
    {
        document = GetComponent<UIDocument>();
        if (document != null && document.rootVisualElement != null)
        {
            deathScreenContainer = document.rootVisualElement.Q<VisualElement>("DeathScreenContainer");
            titleLabel = document.rootVisualElement.Q<Label>("DeathTitle");
            statsLabel = document.rootVisualElement.Q<Label>("DeathStats");
            returnToLobbyButton = document.rootVisualElement.Q<Button>("ReturnToLobbyBtn");

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
        }
    }

    private void OnDisable()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnGameOver -= HandleGameOver;
            NetworkManager.Instance.OnKillFeedReceived -= HandleKillFeed;
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
                ShowDeathScreen("YOU DIED", $"Placement: TBD");
            }
        }
    }

    private void HandleKillFeed(NetworkManager.KillFeedMessage msg)
    {
        if (isDead) return;

        if (localPlayerSync != null && localPlayerSync.GetState() != null)
        {
            if (msg.victimName == localPlayerSync.GetState().username)
            {
                // We got killed by someone
                isDead = true;
                ShowDeathScreen("YOU DIED", $"Killed by: {msg.killerName}\nWeapon: {msg.weapon}");
            }
        }
    }

    private void HandleGameOver()
    {
        if (localPlayerSync != null && localPlayerSync.GetState() != null && !localPlayerSync.GetState().isDead)
        {
            // We survived!
            ShowDeathScreen("VICTORY", $"Kills: {localPlayerSync.GetState().kills}\nYou are the last survivor!");
        }
        else if (!isDead)
        {
            ShowDeathScreen("MATCH OVER", "");
        }
    }

    private void ShowDeathScreen(string title, string stats)
    {
        if (deathScreenContainer == null) return;

        if (titleLabel != null) titleLabel.text = title;
        if (statsLabel != null) statsLabel.text = stats;

        deathScreenContainer.style.display = DisplayStyle.Flex;
        
        // Hide HUD
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
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.CancelMatchmaking();
        }
        SceneManager.LoadScene("MainScene");
    }
}
