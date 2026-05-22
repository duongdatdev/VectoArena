using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [Header("Waiting Panel")]
    [SerializeField]private GameObject waitingPanel;
    [SerializeField] private GameObject playBtn;

    private void Start()
    {
        waitingPanel.SetActive(false);

        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnGameStart += GoToGameplay;
        }
    }

    private void GoToGameplay()
    {
        HideWaitingPanel();
        SceneManager.LoadScene("GameplayScene");
    }

    void ShowWaitingPanel()
    {
        waitingPanel.SetActive(true);
    }

    void HideWaitingPanel()
    {
        waitingPanel.SetActive(false);
    }

    public void OnClickPlay()
    {
        waitingPanel.SetActive(true);
        playBtn.SetActive(false);
        
        _ = NetworkManager.Instance.ConnectAndJoinBattle();
    }

    public void OnClickCancelFindBattle()
    {
        HideWaitingPanel();
        if (playBtn != null)
        {
            playBtn.SetActive(true);
        }
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.CancelMatchmaking();
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnGameStart -= GoToGameplay;
        }
    }
}
