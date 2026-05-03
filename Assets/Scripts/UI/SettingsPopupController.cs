using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

public class SettingsPopupController : MonoBehaviour
{
    private UIDocument document;
    private VisualElement root;

    private Button closeButton;
    private Button tabSound;
    private Button tabGraphics;
    private Button tabAccount;

    private VisualElement panelSound;
    private VisualElement panelGraphics;
    private VisualElement panelAccount;

    private Button logoutButton;

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
        tabAccount = root.Q<Button>("TabAccount");

        panelSound = root.Q<VisualElement>("PanelSound");
        panelGraphics = root.Q<VisualElement>("PanelGraphics");
        panelAccount = root.Q<VisualElement>("PanelAccount");

        logoutButton = root.Q<Button>("LogoutButton");

        closeButton.clicked += Hide;
        
        tabSound.clicked += () => ShowTab("Sound");
        tabGraphics.clicked += () => ShowTab("Graphics");
        tabAccount.clicked += () => ShowTab("Account");

        logoutButton.clicked += OnLogout;
    }

    public void Show()
    {
        root.RemoveFromClassList("hidden");
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
        tabAccount.RemoveFromClassList("settings-tab-button--active");

        panelSound.AddToClassList("hidden");
        panelGraphics.AddToClassList("hidden");
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
        else if (tabName == "Account")
        {
            tabAccount.AddToClassList("settings-tab-button--active");
            panelAccount.RemoveFromClassList("hidden");
        }
    }

    private void OnLogout()
    {
        // In real game, clear token from PlayerPrefs/NetworkManager
        SceneManager.LoadScene("AuthScene");
    }
}
