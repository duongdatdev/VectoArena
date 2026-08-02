using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using VectoArena.UI;

/// <summary>Connects the UI Toolkit mobile HUD to the locally-owned PlayerController.</summary>
[RequireComponent(typeof(UIDocument))]
public sealed class MobileControlsController : MonoBehaviour
{
    public const string SimulationPreferenceKey = "mobile.controls.simulateInEditor";
    private const string GameplaySceneName = "GameplayScene";
    private const float ShootThreshold = 0.2f;

    private UIDocument document;
    private VisualElement controlsRoot;
    private VisualElement deathOverlay;
    private MobileJoystickElement movementJoystick;
    private MobileJoystickElement aimingJoystick;
    private Button weaponSwitchButton;
    private PlayerController localPlayer;
    private bool isFocused = true;
    private bool controlsEnabled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallBootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallCurrentScene()
    {
        TryInstall(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryInstall(scene);
    }

    private static void TryInstall(Scene scene)
    {
        if (!scene.IsValid() || scene.name != GameplaySceneName)
        {
            return;
        }

        DeathScreenManager deathScreen = FindAnyObjectByType<DeathScreenManager>();
        if (deathScreen == null || deathScreen.GetComponent<UIDocument>() == null)
        {
            Debug.LogWarning("Mobile controls were not installed because the GameplayUI document was not found.");
            return;
        }

        if (!deathScreen.TryGetComponent(out MobileControlsController _))
        {
            deathScreen.gameObject.AddComponent<MobileControlsController>();
        }

        CanvasSafeAreaController.InstallInScene(scene);
    }

    private void OnEnable()
    {
        document = GetComponent<UIDocument>();
        VisualTreeAsset controlsAsset = Resources.Load<VisualTreeAsset>("UI/Mobile/MobileControls");
        if (document == null || controlsAsset == null)
        {
            Debug.LogError("Mobile controls require a UIDocument and Resources/UI/Mobile/MobileControls.uxml.");
            enabled = false;
            return;
        }

        TemplateContainer instance = controlsAsset.Instantiate();
        instance.name = "MobileControlsInstance";
        // Every child in MobileControls.uxml is absolutely positioned, so it does
        // not contribute to TemplateContainer's flex layout size. Explicitly
        // stretch the template over the document; otherwise it resolves to a
        // zero-height element on device even though visibility is enabled.
        instance.style.position = Position.Absolute;
        instance.style.left = 0f;
        instance.style.right = 0f;
        instance.style.top = 0f;
        instance.style.bottom = 0f;
        document.rootVisualElement.Insert(0, instance);

        controlsRoot = instance.Q<VisualElement>("MobileControlsRoot");
        movementJoystick = instance.Q<MobileJoystickElement>("LeftJoystick");
        aimingJoystick = instance.Q<MobileJoystickElement>("RightJoystick");
        weaponSwitchButton = instance.Q<Button>("WeaponSwitchButton");
        deathOverlay = document.rootVisualElement.Q<VisualElement>("DeathScreenContainer");

        if (controlsRoot == null || movementJoystick == null || aimingJoystick == null || weaponSwitchButton == null)
        {
            Debug.LogError(
                $"MobileControls.uxml element lookup failed. " +
                $"Root={controlsRoot != null}, LeftJoystick={movementJoystick != null}, " +
                $"RightJoystick={aimingJoystick != null}, WeaponSwitch={weaponSwitchButton != null}.");
            enabled = false;
            return;
        }

        movementJoystick.ValueChanged += OnMoveChanged;
        aimingJoystick.ValueChanged += OnAimChanged;
        aimingJoystick.PressedChanged += OnAimPressedChanged;
        weaponSwitchButton.clicked += OnWeaponSwitchClicked;
        controlsRoot.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        RefreshVisibility(true);
        Debug.Log(
            $"[MobileControls] Installed. platform={Application.platform}, mobile={Application.isMobilePlatform}, " +
            $"screen={Screen.width}x{Screen.height}, visible={controlsEnabled}.");
    }

    private void OnDisable()
    {
        if (movementJoystick != null)
        {
            movementJoystick.ValueChanged -= OnMoveChanged;
        }

        if (aimingJoystick != null)
        {
            aimingJoystick.ValueChanged -= OnAimChanged;
            aimingJoystick.PressedChanged -= OnAimPressedChanged;
        }

        if (weaponSwitchButton != null)
        {
            weaponSwitchButton.clicked -= OnWeaponSwitchClicked;
        }

        if (controlsRoot != null)
        {
            controlsRoot.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        ReleaseLocalPlayer();
        controlsRoot?.RemoveFromHierarchy();
    }

    private void Update()
    {
        if (localPlayer == null || !localPlayer.isActiveAndEnabled)
        {
            FindLocalPlayer();
        }

        RefreshVisibility(false);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        isFocused = hasFocus;
        if (!hasFocus)
        {
            ResetControls();
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            ResetControls();
        }
    }

    private void FindLocalPlayer()
    {
        NetworkPlayerSync[] syncs = FindObjectsByType<NetworkPlayerSync>(FindObjectsInactive.Exclude);
        foreach (NetworkPlayerSync sync in syncs)
        {
            if (sync != null && sync.isLocalPlayer && sync.TryGetComponent(out PlayerController player))
            {
                if (localPlayer != player)
                {
                    ReleaseLocalPlayer();
                    localPlayer = player;
                    localPlayer.SetMobileControlsActive(controlsEnabled);
                }
                return;
            }
        }
    }

    private void RefreshVisibility(bool force)
    {
        bool platformEnabled = Application.isMobilePlatform;
#if UNITY_EDITOR
        platformEnabled |= PlayerPrefs.GetInt(SimulationPreferenceKey, 0) == 1;
#endif
        bool overlayVisible = deathOverlay != null && deathOverlay.resolvedStyle.display != DisplayStyle.None;
        // Keep the HUD visible while the network player is still spawning.
        // Input handlers are null-safe and become active as soon as FindLocalPlayer
        // binds the locally-owned PlayerController.
        bool localPlayerCanPlay = localPlayer == null || IsLocalPlayerAlive();
        bool shouldEnable = platformEnabled && isFocused && !overlayVisible && localPlayerCanPlay;
        if (!force && shouldEnable == controlsEnabled)
        {
            return;
        }

        controlsEnabled = shouldEnable;
        if (controlsRoot != null)
        {
            controlsRoot.style.display = shouldEnable ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (localPlayer != null)
        {
            localPlayer.SetMobileControlsActive(shouldEnable);
        }

        Debug.Log(
            $"[MobileControls] Visibility={shouldEnable}; platform={platformEnabled}, focused={isFocused}, " +
            $"deathOverlay={overlayVisible}, playerCanPlay={localPlayerCanPlay}.");

        if (!shouldEnable)
        {
            ResetControls();
        }
    }

    private bool IsLocalPlayerAlive()
    {
        if (localPlayer == null)
        {
            return false;
        }

        NetworkPlayerSync sync = localPlayer.GetComponent<NetworkPlayerSync>();
        return sync == null || sync.GetState() == null || !sync.GetState().isDead;
    }

    private void OnGeometryChanged(GeometryChangedEvent evt)
    {
        controlsRoot.EnableInClassList("mobile-controls--compact", evt.newRect.height > 0f && evt.newRect.height < 760f);
    }

    private void OnMoveChanged(Vector2 value)
    {
        localPlayer?.SetMobileMoveInput(value);
    }

    private void OnAimChanged(Vector2 value)
    {
        if (localPlayer == null)
        {
            return;
        }

        localPlayer.SetMobileAimInput(value);
        localPlayer.SetMobileShootHeld(value.magnitude >= ShootThreshold);
    }

    private void OnAimPressedChanged(bool pressed)
    {
        if (!pressed && localPlayer != null)
        {
            localPlayer.SetMobileAimInput(Vector2.zero);
            localPlayer.SetMobileShootHeld(false);
        }
    }

    private void OnWeaponSwitchClicked()
    {
        localPlayer?.RequestWeaponSwitch();
    }

    private void ResetControls()
    {
        movementJoystick?.ResetInput(true);
        aimingJoystick?.ResetInput(true);
        localPlayer?.ResetMobileInput();
    }

    private void ReleaseLocalPlayer()
    {
        if (localPlayer != null)
        {
            localPlayer.SetMobileControlsActive(false);
            localPlayer = null;
        }
    }
}
