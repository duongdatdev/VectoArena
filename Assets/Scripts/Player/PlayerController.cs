using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using VectoArena.Schema;

public class PlayerController : MonoBehaviour
{
    private const string BlastWeaponAnchorName = "weapon";
    private const string BlastWeaponMeleeAnchorName = "weapon_melee";
    private const string BlastWeaponXLMeleeAnchorName = "weapon_xlmelee";
    private const string LegacyWeaponHolderName = "WeaponHolder";
    private static readonly string[] WeaponFirePointNames = { "FirePoint", "MuzzleStandard", "Muzzle", "MuzzleFlash" };

    [Header("Movement Settings")]
    [Tooltip("Base speed from Blast Royale: 2.8f (BattleRoyale mode) or 3.75f (Deathmatch mode)")]
    public float moveSpeed = 2.8f;
    
    [Header("Shooting  Settings")]
    public GameObject bulletPrefab;
    public Transform firePoint;
    public bool useProjectileSpawnOffsetFallback = true;
    public Vector3 projectileSpawnOffset = new Vector3(0f, 1f, 0.5f);
    public float fireRate;
    public int defaultMaxAmmo = -1;

    [Header("Weapon Settings")]
    public Transform weaponHolder;
    public Transform weaponMeleeHolder;
    public Transform weaponXLMeleeHolder;
    public GameObject defaultMeleeWeaponPrefab;
    [Tooltip("Hammer reach. Blast Royale's Hammer AttackRange is 1 world unit.")]
    public float meleeAttackRange = 1f;
    public float meleeAttackRadius = 1.15f;
    public float meleeAttackAngle = 85f;
    public float meleeAttackCooldown = 0.7f;
    private GameObject currentWeaponModel;
    private GameObject currentMeleeWeaponModel;
    private GameObject currentRangedWeaponModel;
    private Transform defaultFirePoint;

    private float nextFireTime = 0f;
    private Rigidbody rb;
    private Camera mainCam;
    
    private GameControls gameControls;
    private Vector2 moveInput;
    private Vector2 mousePos;
    private bool isShooting;
    private Vector2 mobileMoveInput;
    private Vector2 mobileAimInput;
    private bool mobileShootHeld;
    private bool mobileControlsActive;
    private int maxAmmo = -1;
    private int currentAmmo = -1;
    private bool rangedWeaponEquipped;
    private string lastSyncedWeapon;
    private ParticleSystem currentMuzzleFlash;

    private Animator anim;
    private readonly HashSet<int> animatorParameterHashes = new HashSet<int>();

    private static readonly int PIsWalking = Animator.StringToHash("isWalking");
    private static readonly int PIsHoldingRight = Animator.StringToHash("isHoldingRight");
    private static readonly int PMoving = Animator.StringToHash("moving");
    private static readonly int PAiming = Animator.StringToHash("aiming");
    private static readonly int PAttack = Animator.StringToHash("attack");
    private static readonly int PHit = Animator.StringToHash("hit");
    private static readonly int PEquipMelee = Animator.StringToHash("equip_melee");
    private static readonly int PEquipGun = Animator.StringToHash("equip_gun");
    private static readonly int PWeaponType = Animator.StringToHash("weapon_type");
    private static readonly int PWeaponTypeFloat = Animator.StringToHash("weapon_type_float");

    private void Awake()
    {
        gameControls = new GameControls();

        gameControls.Gameplay.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        gameControls.Gameplay.Move.canceled += ctx => moveInput = Vector2.zero;

        gameControls.Gameplay.Aim.performed += ctx => mousePos = ctx.ReadValue<Vector2>();

        gameControls.Gameplay.Shoot.started += ctx => isShooting = true;
        gameControls.Gameplay.Shoot.canceled += ctx => isShooting = false;
    }

    private void OnEnable()
    {
        gameControls?.Gameplay.Enable();
    }

    private void OnDisable()
    {
        gameControls?.Gameplay.Disable();
    }

    private void OnDestroy()
    {
        if (gameControls != null)
        {
            gameControls.Dispose();
            gameControls = null;
        }
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        anim = PlayerSkinApplier.ApplyEquippedSkin(gameObject);
        if (anim == null)
        {
            anim = ResolveAnimator();
        }
        if (TryGetComponent(out NetworkPlayerSync sync))
        {
            sync.RefreshAnimator();
        }
        CacheAnimatorParameters();
        
        mainCam = Camera.main;
        ResolveWeaponAnchorsIfNeeded();
        defaultFirePoint = firePoint;
        maxAmmo = defaultMaxAmmo;
        currentAmmo = defaultMaxAmmo;
        EquipDefaultMeleeWeapon();
    }

    private void Update()
    {
        var sync = GetComponent<NetworkPlayerSync>();
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsGameplayInputBlocked)
        {
            moveInput = Vector2.zero;
            isShooting = false;
            ResetMobileInput();
            UpdateAnimation();
            return;
        }

        if (sync != null && sync.GetState() != null && sync.GetState().isDead)
        {
            // Reset inputs if dead
            moveInput = Vector2.zero;
            isShooting = false;
            ResetMobileInput();
            UpdateAnimation();
            return;
        }

        UpdateFacingDirection();

        HandleWeaponSwitchInput();
        SyncWeaponStateFromServer();

        if (IsShootHeld() && Time.time >= nextFireTime)
        {

            if (IsUsingMeleeWeapon())
            {
                TryMeleeAttack(sync);
            }
            else if (CanShoot())
            {
                if (!TryGetShootPose(out Vector3 shootPosition, out Quaternion shootRotation))
                {
                    return;
                }

                PerformShoot(shootPosition, shootRotation);

                if (sync != null)
                {
                    sync.SendShoot(shootPosition, shootRotation);
                }

                ConsumeAmmo();
                nextFireTime = Time.time + fireRate;
            }
        }
        
        UpdateAnimation();
    }

    private void FixedUpdate()
    {
        var sync = GetComponent<NetworkPlayerSync>();
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsGameplayInputBlocked)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            return;
        }

        if (sync != null && sync.GetState() != null && sync.GetState().isDead)
        {
            rb.linearVelocity = Vector3.zero;
            return;
        }

        Vector2 activeMoveInput = mobileControlsActive ? mobileMoveInput : moveInput;
        Vector3 movement = mobileControlsActive
            ? GetCameraRelativeDirection(activeMoveInput)
            : new Vector3(activeMoveInput.x, 0f, activeMoveInput.y);
        rb.MovePosition(rb.position + movement * moveSpeed * Time.fixedDeltaTime);

        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        rb.angularVelocity = Vector3.zero;
    }

    private void OnCollisionEnter(Collision other)
    {
        if (other.gameObject.name == "DamageBlock")
        {
            Debug.Log("Player hit damage block");
            gameObject.GetComponent<Health>().TakeDamage(20);
        }
    }

    private void UpdateAnimation()
    {
        Vector2 activeMoveInput = mobileControlsActive ? mobileMoveInput : moveInput;
        bool isWalking = activeMoveInput.magnitude > 0.1f;
        bool isUsingMeleeWeapon = IsUsingMeleeWeapon();
        bool canShoot = CanShoot();
        bool shootHeld = IsShootHeld();
        bool isHoldingRight = shootHeld && (isUsingMeleeWeapon || canShoot);
        bool isAiming = shootHeld && !isUsingMeleeWeapon && canShoot;

        SetAnimatorBoolIfPresent(PIsWalking, isWalking);
        SetAnimatorBoolIfPresent(PIsHoldingRight, isHoldingRight);

        SetAnimatorBoolIfPresent(PMoving, isWalking);
        SetAnimatorBoolIfPresent(PAiming, isAiming);
    }

    public void TriggerAttackAnimation()
    {
        SetAnimatorTriggerIfPresent(PAttack);
    }

    // Plays the built-in "hit" reaction clip on this character when it takes damage.
    public void TriggerHitAnimation()
    {
        SetAnimatorTriggerIfPresent(PHit);
    }

    public void PerformShoot(Vector3 position, Quaternion rotation)
    {
        NetworkPlayerSync sync = GetComponent<NetworkPlayerSync>();
        VectoAudioManager.PlayWeaponShot(GetCurrentWeaponName(), position, sync != null && sync.isLocalPlayer);

        if (bulletPrefab != null)
        {
            GameObject bulletObj = Instantiate(bulletPrefab, position, rotation);
            Bullet bullet = bulletObj.GetComponent<Bullet>();
            if (bullet != null)
            {
                bullet.owner = sync;
            }
        }

        TriggerAttackAnimation();

        if (currentMuzzleFlash != null)
        {
            currentMuzzleFlash.Stop();
            currentMuzzleFlash.Play();
        }
    }

    public void EquipWeapon(GameObject weaponModelPrefab, GameObject newBulletPrefab, float newFireRate, int newMaxAmmo)
    {
        bool isMeleeModel = newBulletPrefab == null;
        if (isMeleeModel && currentMeleeWeaponModel != null)
        {
            Destroy(currentMeleeWeaponModel);
            currentMeleeWeaponModel = null;
        }
        else if (!isMeleeModel && currentRangedWeaponModel != null)
        {
            Destroy(currentRangedWeaponModel);
            currentRangedWeaponModel = null;
        }

        Transform targetAnchor = ResolveWeaponAnchor(weaponModelPrefab, isMeleeModel);

        if (targetAnchor != null)
        {
            if (weaponModelPrefab != null)
            {
                currentWeaponModel = Instantiate(weaponModelPrefab, targetAnchor);
                currentWeaponModel.transform.localPosition = Vector3.zero;
                currentWeaponModel.transform.localRotation = Quaternion.identity;
                currentWeaponModel.transform.localScale = Vector3.one;
                if (isMeleeModel)
                {
                    currentMeleeWeaponModel = currentWeaponModel;
                }
                else
                {
                    currentRangedWeaponModel = currentWeaponModel;
                }

                currentMuzzleFlash = currentWeaponModel.GetComponentInChildren<ParticleSystem>();

                Transform newFirePoint = FindWeaponFirePoint(currentWeaponModel.transform);
                if (newFirePoint != null)
                {
                    firePoint = newFirePoint;
                }
                else
                {
                    firePoint = null;
                    Debug.LogWarning("The new weapon does not have a FirePoint or muzzle marker. Using projectileSpawnOffset fallback for shooting.");
                }
            }
        }
        else
        {
            Debug.LogWarning("No weapon anchor is assigned in PlayerController!");
        }

        // Update bullet prefab and fire rate
        if (newBulletPrefab != null) 
        {
            bulletPrefab = newBulletPrefab;
        }
        fireRate = newFireRate;
        maxAmmo = newMaxAmmo;
        currentAmmo = newMaxAmmo < 0 ? -1 : newMaxAmmo;
        rangedWeaponEquipped = weaponModelPrefab != null && !isMeleeModel;
        if (currentMeleeWeaponModel != null)
        {
            currentMeleeWeaponModel.SetActive(true);
        }
        if (currentRangedWeaponModel != null)
        {
            currentRangedWeaponModel.SetActive(!isMeleeModel);
        }
    }

    private void EquipDefaultMeleeWeapon()
    {
        if (defaultMeleeWeaponPrefab != null)
        {
            EquipWeapon(defaultMeleeWeaponPrefab, null, meleeAttackCooldown, -1);
        }
    }

    public void EnsureMeleeWeaponVisual(GameObject fallbackMeleeWeaponPrefab)
    {
        if (defaultMeleeWeaponPrefab == null)
        {
            defaultMeleeWeaponPrefab = fallbackMeleeWeaponPrefab;
        }

        if (currentMeleeWeaponModel == null)
        {
            EquipDefaultMeleeWeapon();
        }
    }

    private void HandleWeaponSwitchInput()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        NetworkPlayerSync sync = GetComponent<NetworkPlayerSync>();
        if (sync == null || !sync.isLocalPlayer)
        {
            return;
        }

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            RequestWeaponSwitch();
        }
    }

    public void SetMobileControlsActive(bool active)
    {
        mobileControlsActive = active;
        if (!active)
        {
            ResetMobileInput();
        }
    }

    public void SetMobileMoveInput(Vector2 value)
    {
        mobileMoveInput = Vector2.ClampMagnitude(value, 1f);
    }

    public void SetMobileAimInput(Vector2 value)
    {
        mobileAimInput = Vector2.ClampMagnitude(value, 1f);
    }

    public void SetMobileShootHeld(bool held)
    {
        mobileShootHeld = held;
    }

    public void ResetMobileInput()
    {
        mobileMoveInput = Vector2.zero;
        mobileAimInput = Vector2.zero;
        mobileShootHeld = false;
    }

    public void RequestWeaponSwitch()
    {
        NetworkPlayerSync sync = GetComponent<NetworkPlayerSync>();
        if (sync == null || !sync.isLocalPlayer)
        {
            return;
        }

        PlayerState state = sync.GetState();
        if (state == null || string.IsNullOrEmpty(state.rangedWeapon))
        {
            return;
        }

        bool isUsingMelee = !string.IsNullOrEmpty(state.currentWeapon) && state.currentWeapon == state.meleeWeapon;
        sync.SendWeaponSwitch(isUsingMelee ? "ranged" : "melee");
    }

    private bool IsShootHeld()
    {
        return mobileControlsActive ? mobileShootHeld : isShooting;
    }

    private Vector3 GetCameraRelativeDirection(Vector2 input)
    {
        if (input.sqrMagnitude <= 0.001f)
        {
            return Vector3.zero;
        }

        Camera referenceCamera = mainCam != null ? mainCam : Camera.main;
        if (referenceCamera == null)
        {
            return new Vector3(input.x, 0f, input.y);
        }

        return MobileInputMath.CameraRelativeDirection(input, referenceCamera.transform.right, referenceCamera.transform.forward);
    }

    private void UpdateFacingDirection()
    {
        Vector3 lookDirection = Vector3.zero;

        // Aim takes priority only while the player is actively attacking.
        if (IsShootHeld())
        {
            lookDirection = mobileControlsActive
                ? GetCameraRelativeDirection(mobileAimInput)
                : GetMouseAimDirection();
        }

        // During normal movement, face the direction the character is travelling.
        if (lookDirection.sqrMagnitude <= 0.001f)
        {
            Vector2 activeMoveInput = mobileControlsActive ? mobileMoveInput : moveInput;
            lookDirection = mobileControlsActive
                ? GetCameraRelativeDirection(activeMoveInput)
                : new Vector3(activeMoveInput.x, 0f, activeMoveInput.y);
        }

        if (lookDirection.sqrMagnitude > 0.001f)
        {
            transform.forward = lookDirection.normalized;
        }
    }

    private Vector3 GetMouseAimDirection()
    {
        if (mainCam == null)
        {
            return Vector3.zero;
        }

        Ray ray = mainCam.ScreenPointToRay(mousePos);
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        if (!groundPlane.Raycast(ray, out float rayDistance))
        {
            return Vector3.zero;
        }

        Vector3 lookDirection = ray.GetPoint(rayDistance) - transform.position;
        lookDirection.y = 0f;
        return lookDirection;
    }

    public void SyncWeaponStateFromServer()
    {
        NetworkPlayerSync sync = GetComponent<NetworkPlayerSync>();
        if (sync == null)
        {
            return;
        }

        PlayerState state = sync.GetState();
        if (state == null)
        {
            return;
        }

        if (currentMeleeWeaponModel != null)
        {
            bool shouldShowMeleeModel = state.currentWeapon == state.meleeWeapon;
            if (currentMeleeWeaponModel.activeSelf != shouldShowMeleeModel)
            {
                currentMeleeWeaponModel.SetActive(shouldShowMeleeModel);
            }
        }
        if (currentRangedWeaponModel != null)
        {
            bool shouldShowRangedModel = state.currentWeapon == state.rangedWeapon && rangedWeaponEquipped;
            if (currentRangedWeaponModel.activeSelf != shouldShowRangedModel)
            {
                currentRangedWeaponModel.SetActive(shouldShowRangedModel);
            }
        }

        bool isMeleeEquipped = !string.IsNullOrEmpty(state.currentWeapon) && state.currentWeapon == state.meleeWeapon;
        string activeWeaponName = isMeleeEquipped ? state.meleeWeapon : state.rangedWeapon;
        SetWeaponTypeAnimation(activeWeaponName, isMeleeEquipped);

        if (state.currentWeapon != lastSyncedWeapon && !string.IsNullOrEmpty(state.currentWeapon))
        {
            TriggerEquipAnimation(isMeleeEquipped);
            lastSyncedWeapon = state.currentWeapon;
        }

        currentAmmo = Mathf.Max(0, Mathf.RoundToInt(state.ammo));
    }

    private bool IsUsingMeleeWeapon()
    {
        NetworkPlayerSync sync = GetComponent<NetworkPlayerSync>();
        if (sync == null)
        {
            return false;
        }

        PlayerState state = sync.GetState();
        if (state == null)
        {
            return currentMeleeWeaponModel != null && currentMeleeWeaponModel.activeSelf;
        }

        return !string.IsNullOrEmpty(state.currentWeapon) &&
               state.currentWeapon == state.meleeWeapon;
    }

    private void TryMeleeAttack(NetworkPlayerSync sync)
    {
        if (sync == null)
        {
            return;
        }

        VectoAudioManager.PlayMelee(transform.position, sync.isLocalPlayer);
        TriggerAttackAnimation();
        nextFireTime = Time.time + meleeAttackCooldown;

        Vector3 center = transform.position + transform.forward * (meleeAttackRange * 0.5f);
        Collider[] hits = Physics.OverlapSphere(center, meleeAttackRadius);

        NetworkPlayerSync bestTarget = null;
        float bestDistance = float.MaxValue;

        foreach (Collider hit in hits)
        {
            NetworkPlayerSync targetSync = hit.GetComponentInParent<NetworkPlayerSync>();
            if (targetSync == null || targetSync == sync)
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, targetSync.transform.position);
            if (distance > meleeAttackRange || distance >= bestDistance)
            {
                continue;
            }

            Vector3 directionToTarget = targetSync.transform.position - transform.position;
            directionToTarget.y = 0f;
            if (directionToTarget.sqrMagnitude <= 0.001f)
            {
                continue;
            }

            float angle = Vector3.Angle(transform.forward, directionToTarget.normalized);
            if (angle > meleeAttackAngle * 0.5f)
            {
                continue;
            }

            bestDistance = distance;
            bestTarget = targetSync;
        }

        sync.SendMeleeAttack(bestTarget != null ? bestTarget.GetSessionId() : string.Empty);
    }

    private string GetCurrentWeaponName()
    {
        NetworkPlayerSync sync = GetComponent<NetworkPlayerSync>();
        PlayerState state = sync != null ? sync.GetState() : null;
        if (state != null && !string.IsNullOrEmpty(state.currentWeapon))
        {
            return state.currentWeapon;
        }

        return rangedWeaponEquipped ? lastSyncedWeapon : string.Empty;
    }

    private void TriggerEquipAnimation(bool isMeleeEquipped)
    {
        if (isMeleeEquipped)
        {
            SetAnimatorTriggerIfPresent(PEquipMelee);
        }
        else
        {
            SetAnimatorTriggerIfPresent(PEquipGun);
        }
    }

    private void SetWeaponTypeAnimation(string weaponName, bool isMeleeEquipped)
    {
        int weaponType = GetWeaponTypeValue(weaponName, isMeleeEquipped);
        SetAnimatorIntIfPresent(PWeaponType, weaponType);
        SetAnimatorFloatIfPresent(PWeaponTypeFloat, weaponType);
    }

    private int GetWeaponTypeValue(string weaponName, bool isMeleeEquipped)
    {
        if (string.IsNullOrEmpty(weaponName))
        {
            return 0;
        }

        if (isMeleeEquipped)
        {
            return 1;
        }

        switch (weaponName)
        {
            case "Rifle":           return 2;  // Gun
            case "BurstRifle":      return 2;  // Gun
            case "RebelRifle":      return 2;  // Gun
            case "MachineGun":      return 2;  // Gun
            case "Shotgun":         return 9;  // Shotgun
            case "BlasterShotgun":  return 9;  // Shotgun
            case "Sniper":          return 4;  // SniperGun
            case "HunterSniper":    return 4;  // SniperGun
            case "Launcher":        return 5;  // Launcher
            case "Minigun":         return 3;  // XLGun
            case "Pistol":          return 8;  // Handgun
            case "Knife":           return 7;  // KnifeMelee
            case "XLMelee":         return 6;  // XLMelee

            case "SniperGun":       return 4;
            case "XLGun":           return 3;
            case "Handgun":         return 8;
            default:                return 2;
        }
    }

    private void CacheAnimatorParameters()
    {
        animatorParameterHashes.Clear();

        if (anim == null)
        {
            return;
        }

        foreach (AnimatorControllerParameter parameter in anim.parameters)
        {
            animatorParameterHashes.Add(parameter.nameHash);
        }
    }

    private bool HasAnimatorParameter(int parameterHash)
    {
        return anim != null && animatorParameterHashes.Contains(parameterHash);
    }

    private void SetAnimatorBoolIfPresent(int parameterHash, bool value)
    {
        if (HasAnimatorParameter(parameterHash))
        {
            anim.SetBool(parameterHash, value);
        }
    }

    private void SetAnimatorTriggerIfPresent(int parameterHash)
    {
        if (HasAnimatorParameter(parameterHash))
        {
            anim.SetTrigger(parameterHash);
        }
    }

    private void SetAnimatorIntIfPresent(int parameterHash, int value)
    {
        if (HasAnimatorParameter(parameterHash))
        {
            anim.SetInteger(parameterHash, value);
        }
    }

    private void SetAnimatorFloatIfPresent(int parameterHash, float value)
    {
        if (HasAnimatorParameter(parameterHash))
        {
            anim.SetFloat(parameterHash, value);
        }
    }

    private Animator ResolveAnimator()
    {
        Transform characterRoot = FindChildTransformByName("Char_Female01");
        if (characterRoot != null && characterRoot.TryGetComponent(out Animator characterAnimator))
        {
            EnsureAnimationEventReceiver(characterAnimator);
            return characterAnimator;
        }

        Animator childAnimator = GetComponentInChildren<Animator>(true);
        if (childAnimator != null)
        {
            EnsureAnimationEventReceiver(childAnimator);
            return childAnimator;
        }

        Animator rootAnimator = GetComponent<Animator>();
        EnsureAnimationEventReceiver(rootAnimator);
        return rootAnimator;
    }

    private void EnsureAnimationEventReceiver(Animator targetAnimator)
    {
        if (targetAnimator == null)
        {
            return;
        }

        if (!targetAnimator.TryGetComponent<CharacterAnimationEventReceiver>(out _))
        {
            targetAnimator.gameObject.AddComponent<CharacterAnimationEventReceiver>();
        }
    }

    private void ResolveWeaponAnchorsIfNeeded()
    {
        if (weaponHolder == null)
        {
            weaponHolder = FindChildTransformByName(BlastWeaponAnchorName) ?? FindChildTransformByName(LegacyWeaponHolderName);
        }

        if (weaponMeleeHolder == null)
        {
            weaponMeleeHolder = FindChildTransformByName(BlastWeaponMeleeAnchorName) ?? weaponHolder;
        }

        if (weaponXLMeleeHolder == null)
        {
            weaponXLMeleeHolder = FindChildTransformByName(BlastWeaponXLMeleeAnchorName) ?? weaponMeleeHolder;
        }
    }

    private Transform FindChildTransformByName(string targetName)
    {
        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        foreach (Transform t in transforms)
        {
            if (string.Equals(t.name, targetName, StringComparison.OrdinalIgnoreCase))
            {
                return t;
            }
        }

        return null;
    }

    private Transform FindChildTransformByName(Transform root, string targetName)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in transforms)
        {
            if (string.Equals(t.name, targetName, StringComparison.OrdinalIgnoreCase))
            {
                return t;
            }
        }

        return null;
    }

    private Transform FindWeaponFirePoint(Transform weaponRoot)
    {
        foreach (string firePointName in WeaponFirePointNames)
        {
            Transform firePointCandidate = FindChildTransformByName(weaponRoot, firePointName);
            if (firePointCandidate != null)
            {
                return firePointCandidate;
            }
        }

        ParticleSystem muzzleFlash = weaponRoot != null ? weaponRoot.GetComponentInChildren<ParticleSystem>(true) : null;
        return muzzleFlash != null ? muzzleFlash.transform : null;
    }

    private Transform ResolveWeaponAnchor(GameObject weaponModelPrefab, bool isMeleeModel)
    {
        ResolveWeaponAnchorsIfNeeded();

        if (!isMeleeModel)
        {
            return weaponHolder;
        }

        if (weaponXLMeleeHolder != null && weaponModelPrefab != null &&
            weaponModelPrefab.name.IndexOf("xl", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return weaponXLMeleeHolder;
        }

        if (weaponMeleeHolder != null)
        {
            return weaponMeleeHolder;
        }

        return weaponHolder;
    }

    private bool CanShoot()
    {
        if (bulletPrefab == null)
        {
            return false;
        }

        bool hasShootOrigin = firePoint != null || useProjectileSpawnOffsetFallback;
        if (!hasShootOrigin)
        {
            return false;
        }

        return maxAmmo < 0 || currentAmmo > 0;
    }

    private bool TryGetShootPose(out Vector3 shootPosition, out Quaternion shootRotation)
    {
        // In Blast Royale, the projectile direction is always the player's aim direction,
        // NOT the firePoint bone's rotation. The firePoint only determines the spawn position.
        // This prevents bullets from flying at weird angles when the weapon model bone is tilted.
        Vector3 aimDirection = transform.forward;
        aimDirection.y = 0f;
        aimDirection.Normalize();
        shootRotation = Quaternion.LookRotation(aimDirection, Vector3.up);

        if (firePoint != null)
        {
            shootPosition = firePoint.position;
            return true;
        }

        if (useProjectileSpawnOffsetFallback)
        {
            shootPosition = transform.TransformPoint(projectileSpawnOffset);
            return true;
        }

        shootPosition = Vector3.zero;
        return false;
    }

    private void ConsumeAmmo()
    {
        if (maxAmmo < 0)
        {
            return;
        }

        currentAmmo = Mathf.Max(0, currentAmmo - 1);
    }
}
