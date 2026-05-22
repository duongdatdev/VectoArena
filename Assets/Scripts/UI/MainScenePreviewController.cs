using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using VectoArena.UI.MainMenu;
using UnityEngine.EventSystems;

public class MainScenePreviewController : MonoBehaviour
{
    [SerializeField] private string defaultSkinResourcePath = "CharacterSkins/Female01/Char_Female01";
    [SerializeField] private float previewWeaponType = 1f;
    [SerializeField] private Vector3 fallbackPreviewPosition = new Vector3(0f, -1.15f, 0f);
    [SerializeField] private Vector3 fallbackPreviewRotation = new Vector3(0f, 75f, 0f);
    [SerializeField] private Vector3 fallbackPreviewScale = new Vector3(1.6f, 1.6f, 1.6f);
    [SerializeField] private Vector3 anchoredPreviewRotation = new Vector3(0f, 75f, 0f);

    private GameObject currentCharacter;
    private string currentSkinId;
    private RuntimeAnimatorController previewAnimatorController;
    private GameObject homeScreenPrefabObj;
    private Transform localAnchor;
    private Transform localCharacterAnchor;
    private Transform previewParent;
    private CancellationTokenSource loadCancellation;

    private void OnEnable()
    {
        loadCancellation?.Cancel();
        loadCancellation?.Dispose();
        loadCancellation = new CancellationTokenSource();

        EnsureEventSystem();
        EnsureHomeScreenPrefab();
        PlayerInventory.Changed += RefreshCharacter;
        RefreshCharacter();
        _ = LoadEquippedSkin(loadCancellation.Token);
    }

    private void OnDisable()
    {
        PlayerInventory.Changed -= RefreshCharacter;
        CancelPendingLoad();
    }

    private void OnDestroy()
    {
        PlayerInventory.Changed -= RefreshCharacter;
        CancelPendingLoad();
    }

    private async Task LoadEquippedSkin(CancellationToken cancellationToken)
    {
        try
        {
            await PlayerInventory.LoadFromServer();
        }
        catch (Exception ex)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                Debug.LogWarning("Failed to load equipped skin for main scene preview: " + ex.Message);
            }
            return;
        }

        if (cancellationToken.IsCancellationRequested || this == null || !isActiveAndEnabled)
        {
            return;
        }

        RefreshCharacter();
    }

    private void RefreshCharacter()
    {
        if (this == null || !isActiveAndEnabled)
        {
            return;
        }

        string equippedSkinId = PlayerInventory.EquippedSkinId;
        if (currentCharacter != null && currentSkinId == equippedSkinId)
        {
            return;
        }

        if (currentCharacter != null)
        {
            Destroy(currentCharacter);
        }

        GameObject prefab = LoadEquippedSkinPrefab(equippedSkinId);
        if (prefab == null)
        {
            Debug.LogWarning("Main scene preview skin prefab not found: " + equippedSkinId);
            return;
        }

        currentCharacter = Instantiate(prefab, localAnchor != null ? localAnchor : transform);
        currentCharacter.name = "MainScenePreview_" + equippedSkinId;

        ApplyPreviewLocalTransform(currentCharacter.transform);

        currentSkinId = equippedSkinId;

        Animator animator = currentCharacter.GetComponentInChildren<Animator>(true);
        EnsurePreviewAnimatorController(animator, SkinCatalog.GetById(equippedSkinId));
        ConfigurePreviewAnimator(animator);
        
        EnsureCharacterInteraction(currentCharacter);
    }

    private GameObject LoadEquippedSkinPrefab(string skinId)
    {
        SkinCatalogItem item = SkinCatalog.GetById(skinId);
        string resourcePath = string.IsNullOrEmpty(item.PrefabResourcePath) ? defaultSkinResourcePath : item.PrefabResourcePath;
        return Resources.Load<GameObject>(resourcePath);
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            
            #if ENABLE_INPUT_SYSTEM
            eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            #else
            eventSystem.AddComponent<StandaloneInputModule>();
            #endif
        }
    }

    private void EnsureHomeScreenPrefab()
    {
        if (homeScreenPrefabObj != null) return;

        Transform existing = transform.Find("HomeScreen");
        if (existing != null)
        {
            homeScreenPrefabObj = existing.gameObject;
        }
        else
        {
            GameObject prefab = Resources.Load<GameObject>("Backgrounds/HomeScreen");
            if (prefab != null)
            {
                Debug.Log("Instantiating HomeScreen prefab from Resources");
                homeScreenPrefabObj = Instantiate(prefab, transform);
                homeScreenPrefabObj.name = "HomeScreen";
            }
            else
            {
                Debug.LogWarning("HomeScreen prefab not found in Resources/Backgrounds! Please ensure it is copied over.");
            }
        }

        if (homeScreenPrefabObj != null)
        {
            Transform localRoot = RecursiveFind(homeScreenPrefabObj.transform, "Local");
            localAnchor = RecursiveFind(homeScreenPrefabObj.transform, "Local Anchor");
            localCharacterAnchor = localRoot != null ? RecursiveFind(localRoot, "CharacterAnchor") : null;
            if (localAnchor == null) Debug.LogWarning("Local Anchor not found in HomeScreen prefab!");
            if (localCharacterAnchor == null) Debug.LogWarning("Local/CharacterAnchor not found in HomeScreen prefab!");
            previewParent = localCharacterAnchor != null ? localCharacterAnchor : (localAnchor != null ? localAnchor : transform);
            DisableRemotePartyMemberShadows();

            var uiDoc = homeScreenPrefabObj.GetComponent<UnityEngine.UIElements.UIDocument>();
            if (uiDoc != null) Destroy(uiDoc);

            Transform camTransform = RecursiveFind(homeScreenPrefabObj.transform, "Main Menu Camera");
            if (camTransform != null)
            {
                Camera cam = camTransform.GetComponent<Camera>();
                if (cam != null)
                {
                    cam.fieldOfView = 45f;
                    cam.nearClipPlane = 0.1f;
                    cam.farClipPlane = 500f;
                    
                    // Make this the active camera
                    if (Camera.main != null && Camera.main != cam)
                    {
                        AudioListener mainListener = Camera.main.GetComponent<AudioListener>();
                        if (mainListener != null) mainListener.enabled = false;
                        Camera.main.gameObject.SetActive(false);
                    }
                    cam.gameObject.tag = "MainCamera";
                }

                if (camTransform.GetComponent<AudioListener>() == null)
                {
                    try { camTransform.gameObject.AddComponent<AudioListener>(); } catch {}
                }
            }
            else
            {
                Debug.LogWarning("Main Menu Camera not found in HomeScreen prefab!");
            }
        }
    }

    private Transform RecursiveFind(Transform parent, string childName)
    {
        if (parent.name == childName) return parent;
        foreach (Transform child in parent)
        {
            Transform found = RecursiveFind(child, childName);
            if (found != null) return found;
        }
        return null;
    }

    private void DisableRemotePartyMemberShadows()
    {
        if (homeScreenPrefabObj == null)
        {
            return;
        }

        Transform partyMembers = RecursiveFind(homeScreenPrefabObj.transform, "PartyMembers");
        if (partyMembers == null)
        {
            return;
        }

        DisableShadowSpritesRecursive(partyMembers);
    }

    private void DisableShadowSpritesRecursive(Transform parent)
    {
        foreach (Transform child in parent)
        {
            if (child.name == "Bigger Shadow" || child.name == "Smaller Shadow")
            {
                child.gameObject.SetActive(false);
            }

            DisableShadowSpritesRecursive(child);
        }
    }

    private void ApplyPreviewLocalTransform(Transform previewTransform)
    {
        if (previewTransform == null)
        {
            return;
        }

        if (this == null)
        {
            return;
        }

        Transform targetParent = previewParent != null ? previewParent : (localCharacterAnchor != null ? localCharacterAnchor : (localAnchor != null ? localAnchor : transform));
        if (previewTransform.parent != targetParent)
        {
            previewTransform.SetParent(targetParent, false);
        }

        if (targetParent == localCharacterAnchor && localCharacterAnchor != null)
        {
            previewTransform.localPosition = Vector3.zero;
            previewTransform.localRotation = Quaternion.identity;
            previewTransform.localScale = Vector3.one;
            return;
        }

        previewTransform.localPosition = fallbackPreviewPosition;
        previewTransform.localRotation = Quaternion.Euler(fallbackPreviewRotation);
        previewTransform.localScale = fallbackPreviewScale;
    }

    private void EnsurePreviewAnimatorController(Animator animator, SkinCatalogItem item)
    {
        if (animator == null)
        {
            return;
        }

        RuntimeAnimatorController controller = ResolvePreviewAnimatorController(item);
        if (controller != null && animator.runtimeAnimatorController != controller)
        {
            animator.runtimeAnimatorController = controller;
        }
    }

    private RuntimeAnimatorController ResolvePreviewAnimatorController(SkinCatalogItem item)
    {
        RuntimeAnimatorController skinController = SkinCatalog.LoadAnimatorController(item);
        if (skinController != null)
        {
            return skinController;
        }

        if (previewAnimatorController != null)
        {
            return previewAnimatorController;
        }

        GameObject defaultPrefab = Resources.Load<GameObject>(defaultSkinResourcePath);
        Animator defaultAnimator = defaultPrefab == null ? null : defaultPrefab.GetComponentInChildren<Animator>(true);
        previewAnimatorController = defaultAnimator == null ? null : defaultAnimator.runtimeAnimatorController;
        return previewAnimatorController;
    }

    private void EnsureCharacterInteraction(GameObject character)
    {
        if (character.GetComponent<MainSceneCharacterInteraction>() == null)
        {
            character.AddComponent<MainSceneCharacterInteraction>();
        }

        if (character.GetComponent<Collider>() == null)
        {
            CapsuleCollider col = character.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 0.75f, 0);
            col.radius = 0.36f;
            col.height = 1.5f;
            col.isTrigger = false;
        }
    }

    private void ConfigurePreviewAnimator(Animator animator)
    {
        if (animator == null)
        {
            return;
        }

        SetBoolIfExists(animator, "moving", false);
        SetBoolIfExists(animator, "aiming", false);
        SetBoolIfExists(animator, "isWalking", false);
        SetBoolIfExists(animator, "isHoldingRight", false);
        SetBoolIfExists(animator, "equip_melee", true);
        SetBoolIfExists(animator, "equip_gun", false);
        SetFloatIfExists(animator, "weapon_type_float", previewWeaponType);
        SetIntIfExists(animator, "weapon_type", Mathf.RoundToInt(previewWeaponType));
        PlayStateIfExists(animator, "IdleBT");
        animator.Update(0f);
    }

    private void SetBoolIfExists(Animator animator, string parameterName, bool value)
    {
        if (animator == null)
        {
            return;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Bool && parameter.name == parameterName)
            {
                animator.SetBool(parameterName, value);
                return;
            }
        }
    }

    private void SetFloatIfExists(Animator animator, string parameterName, float value)
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Float && parameter.name == parameterName)
            {
                animator.SetFloat(parameterName, value);
                return;
            }
        }
    }

    private void SetIntIfExists(Animator animator, string parameterName, int value)
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Int && parameter.name == parameterName)
            {
                animator.SetInteger(parameterName, value);
                return;
            }
        }
    }
    private void PlayStateIfExists(Animator animator, string stateName)
    {
        if (animator.HasState(0, Animator.StringToHash(stateName)))
        {
            animator.Play(stateName, 0, 0f);
        }
    }

    private void CancelPendingLoad()
    {
        if (loadCancellation == null)
        {
            return;
        }

        loadCancellation.Cancel();
        loadCancellation.Dispose();
        loadCancellation = null;
    }
}
