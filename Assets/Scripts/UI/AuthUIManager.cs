using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class AuthUIManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject loginPanel;
    public GameObject signupPanel;

    [Header("Login Inputs")]
    public TMP_InputField loginUsernameInput;
    public TMP_InputField loginPasswordInput;

    [Header("Signup Inputs")]
    public TMP_InputField signupUsernameInput;
    public TMP_InputField signupPasswordInput;
    public TMP_InputField signupRePasswordInput;

    [Header("Status UI")]
    public TMP_Text loginStatusText;
    public TMP_Text signupStatusText;
    public Button loginButton;
    public Button signupButton;

    private void Start()
    {
        ShowLoginPanel();
    }

    public void ShowLoginPanel()
    {
        loginPanel.SetActive(true);
        signupPanel.SetActive(false);
        SetLoginStatus("", Color.white);
        SetSignupStatus("", Color.white);
        SetButtonsState(true);
    }

    public void ShowSignupPanel()
    {
        loginPanel.SetActive(false);
        signupPanel.SetActive(true);
        SetLoginStatus("", Color.white);
        SetSignupStatus("", Color.white);
        SetButtonsState(true);
    }

    public void OnLoginClick()
    {
        string username = loginUsernameInput.text;
        string password = loginPasswordInput.text;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            SetLoginStatus("Username and password are required.", Color.red);
            return;
        }

        SetLoginStatus("Logging in...", Color.blue);
        SetButtonsState(false);
        ProcessLogin(username, password);
    }

    private async void ProcessLogin(string username, string password)
    {
        bool success = await NetworkManager.Instance.Login(username, password);
        if (success)
        {
            SetLoginStatus("Login Successful!", Color.green);
            Invoke("LoadMainScene", 1.0f);
        }
        else
        {
            string message = NetworkManager.Instance != null && !string.IsNullOrEmpty(NetworkManager.Instance.LastErrorMessage)
                ? NetworkManager.Instance.LastErrorMessage
                : "Login Failed. Check credentials.";
            SetLoginStatus(message, Color.red);
            SetButtonsState(true);
        }
    }

    public void OnSignupClick()
    {
        string username = signupUsernameInput.text;
        string password = signupPasswordInput.text;
        string rePassword = signupRePasswordInput.text;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            SetSignupStatus("Username and password are required.", Color.red);
            return;
        }

        if (password != rePassword)
        {
            SetSignupStatus("Passwords do not match.", Color.red);
            return;
        }

        SetSignupStatus("Registering...", Color.blue);
        SetButtonsState(false);
        ProcessRegister(username, password);
    }

    private async void ProcessRegister(string username, string password)
    {
        bool success = await NetworkManager.Instance.Register(username, password);
        if (success)
        {
            SetSignupStatus("Registration Successful!", Color.green);
            Invoke("ShowLoginPanel", 1.5f);
        }
        else
        {
            SetSignupStatus("Registration Failed. Username might exist.", Color.red);
            SetButtonsState(true);
        }
    }

    private void SetLoginStatus(string msg, Color color)
    {
        if (loginStatusText != null)
        {
            loginStatusText.text = msg;
            loginStatusText.color = color;
        }
    }

    private void SetSignupStatus(string msg, Color color)
    {
        if (signupStatusText != null)
        {
            signupStatusText.text = msg;
            signupStatusText.color = color;
        }
    }

    private void SetButtonsState(bool state)
    {
        if (loginButton != null) loginButton.interactable = state;
        if (signupButton != null) signupButton.interactable = state;
    }

    private void LoadMainScene()
    {
        SceneManager.LoadScene("MainScene");
    }
}
