using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

public class AuthScreenController : MonoBehaviour
{
    private UIDocument document;
    private VisualElement root;

    private VisualElement loginPanel;
    private VisualElement registerPanel;
    private VisualElement loadingOverlay;
    private VisualElement spinner;

    // Login Elements
    private TextField loginUsername;
    private TextField loginPassword;
    private Button loginButton;
    private Button switchToRegisterButton;
    private Label loginStatus;

    // Register Elements
    private TextField regUsername;
    private TextField regPassword;
    private Button registerButton;
    private Button switchToLoginButton;
    private Label registerStatus;

    private bool isLoading = false;
    private IVisualElementScheduledItem spinnerSchedule;

    private float spinnerAngle = 0f;

    private void OnEnable()
    {
        document = GetComponent<UIDocument>();
        if (document == null) return;
        
        root = document.rootVisualElement;

        // Query panels
        loginPanel = root.Q<VisualElement>("LoginPanel");
        registerPanel = root.Q<VisualElement>("RegisterPanel");
        loadingOverlay = root.Q<VisualElement>("LoadingOverlay");
        spinner = root.Q<VisualElement>("Spinner");

        // Login Query
        loginUsername = root.Q<TextField>("LoginUsername");
        loginPassword = root.Q<TextField>("LoginPassword");
        loginButton = root.Q<Button>("LoginButton");
        switchToRegisterButton = root.Q<Button>("SwitchToRegister");
        loginStatus = root.Q<Label>("LoginStatus");

        // Register Query
        regUsername = root.Q<TextField>("RegUsername");
        regPassword = root.Q<TextField>("RegPassword");
        registerButton = root.Q<Button>("RegisterButton");
        switchToLoginButton = root.Q<Button>("SwitchToLogin");
        registerStatus = root.Q<Label>("RegisterStatus");

        // Register Callbacks
        loginButton.clicked += OnLoginClick;
        switchToRegisterButton.clicked += ShowRegisterPanel;
        
        registerButton.clicked += OnRegisterClick;
        switchToLoginButton.clicked += ShowLoginPanel;

        // Setup Spinner
        spinnerSchedule = spinner.schedule.Execute(() =>
        {
            spinnerAngle -= 5f;
            spinner.style.rotate = new StyleRotate(new Rotate(new Angle(spinnerAngle, AngleUnit.Degree)));
        }).Every(16);
        spinnerSchedule.Pause();

        ShowLoginPanel();
    }

    private void ShowLoginPanel()
    {
        loginPanel.RemoveFromClassList("hidden");
        registerPanel.AddToClassList("hidden");
        loginStatus.text = "";
    }

    private void ShowRegisterPanel()
    {
        registerPanel.RemoveFromClassList("hidden");
        loginPanel.AddToClassList("hidden");
        registerStatus.text = "";
    }

    private void SetLoading(bool state)
    {
        isLoading = state;
        if (state)
        {
            loadingOverlay.RemoveFromClassList("hidden");
            spinnerSchedule.Resume();
        }
        else
        {
            loadingOverlay.AddToClassList("hidden");
            spinnerSchedule.Pause();
        }

        loginButton.SetEnabled(!state);
        registerButton.SetEnabled(!state);
    }

    private async void OnLoginClick()
    {
        if (isLoading) return;

        string user = loginUsername.value;
        string pass = loginPassword.value;

        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
        {
            loginStatus.text = "Username and password are required";
            loginStatus.style.color = new StyleColor(Color.red);
            return;
        }

        SetLoading(true);
        loginStatus.text = "Logging in...";
        loginStatus.style.color = new StyleColor(new Color(0.3f, 0.7f, 1f));

        bool success = await NetworkManager.Instance.Login(user, pass);
        
        SetLoading(false);

        if (success)
        {
            loginStatus.text = "Login Successful!";
            loginStatus.style.color = new StyleColor(Color.green);
            Invoke("LoadMainScene", 0.5f);
        }
        else
        {
            loginStatus.text = "Login Failed. Check credentials.";
            loginStatus.style.color = new StyleColor(Color.red);
        }
    }

    private async void OnRegisterClick()
    {
        if (isLoading) return;

        string user = regUsername.value;
        string pass = regPassword.value;

        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
        {
            registerStatus.text = "Username and password are required";
            registerStatus.style.color = new StyleColor(Color.red);
            return;
        }

        SetLoading(true);
        registerStatus.text = "Registering...";
        registerStatus.style.color = new StyleColor(new Color(0.3f, 0.7f, 1f));

        bool success = await NetworkManager.Instance.Register(user, pass);
        
        SetLoading(false);

        if (success)
        {
            registerStatus.text = "Registration Successful!";
            registerStatus.style.color = new StyleColor(Color.green);
            Invoke("ShowLoginPanel", 1.0f);
        }
        else
        {
            registerStatus.text = "Registration Failed. Username might exist.";
            registerStatus.style.color = new StyleColor(Color.red);
        }
    }

    private void LoadMainScene()
    {
        SceneManager.LoadScene("MainScene");
    }
}
