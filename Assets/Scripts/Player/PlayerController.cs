using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using VectoArena.Schema;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5;
    
    [Header("Shooting  Settings")]
    public GameObject bulletPrefab;
    public Transform firePoint;
    public float fireRate;
    public int defaultMaxAmmo = -1;

    [Header("Weapon Settings")]
    public Transform weaponHolder;
    public float meleeAttackRange = 2.5f;
    public float meleeAttackRadius = 1.15f;
    public float meleeAttackCooldown = 0.7f;
    private GameObject currentWeaponModel;

    private float nextFireTime = 0f;
    private Rigidbody rb;
    private Camera mainCam;
    
    private GameControls gameControls;
    private Vector2 moveInput;
    private Vector2 mousePos;
    private bool isShooting;
    private int maxAmmo = -1;
    private int currentAmmo = -1;
    private bool rangedWeaponEquipped;
    private string lastSyncedWeapon;

    private Animator anim;
    private readonly HashSet<int> animatorParameterHashes = new HashSet<int>();

    private static readonly int PIsWalking = Animator.StringToHash("isWalking");
    private static readonly int PIsHoldingRight = Animator.StringToHash("isHoldingRight");
    private static readonly int PMoving = Animator.StringToHash("moving");
    private static readonly int PAiming = Animator.StringToHash("aiming");
    private static readonly int PAttack = Animator.StringToHash("attack");
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

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        anim = GetComponent<Animator>();
        CacheAnimatorParameters();
        
        mainCam = Camera.main;
        maxAmmo = defaultMaxAmmo;
        currentAmmo = defaultMaxAmmo;
    }

    private void Update()
    {
        Ray ray = mainCam.ScreenPointToRay(mousePos);

        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

        if (groundPlane.Raycast(ray, out float rayDistance))
        {
            Vector3 point = ray.GetPoint(rayDistance);

            Vector3 lookDirection = point - transform.position;

            lookDirection.y = 0f;

            transform.forward = lookDirection;
        }

        HandleWeaponSwitchInput();
        SyncWeaponStateFromServer();

        if (isShooting && Time.time >= nextFireTime)
        {
            var sync = GetComponent<NetworkPlayerSync>();

            if (IsUsingMeleeWeapon())
            {
                TryMeleeAttack(sync);
            }
            else if (CanShoot())
            {
                GameObject bulletObj = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
                Bullet bullet = bulletObj.GetComponent<Bullet>();

                if (sync != null)
                {
                    if (bullet != null) bullet.owner = sync;
                    sync.SendShoot(firePoint.position, firePoint.rotation);
                }

                TriggerAttackAnimation();
                ConsumeAmmo();
                nextFireTime = Time.time + fireRate;
            }
        }
        
        UpdateAnimation();
    }

    private void FixedUpdate()
    {
        Vector3 movement = new Vector3(moveInput.x, 0f, moveInput.y);
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
        bool isWalking = moveInput.magnitude > 0.1f;
        bool isUsingMeleeWeapon = IsUsingMeleeWeapon();
        bool canShoot = CanShoot();
        bool isHoldingRight = isShooting && (isUsingMeleeWeapon || canShoot);
        bool isAiming = isShooting && !isUsingMeleeWeapon && canShoot;

        SetAnimatorBoolIfPresent(PIsWalking, isWalking);
        SetAnimatorBoolIfPresent(PIsHoldingRight, isHoldingRight);

        SetAnimatorBoolIfPresent(PMoving, isWalking);
        SetAnimatorBoolIfPresent(PAiming, isAiming);
    }

    public void TriggerAttackAnimation()
    {
        SetAnimatorTriggerIfPresent(PAttack);
    }

    public void EquipWeapon(GameObject weaponModelPrefab, GameObject newBulletPrefab, float newFireRate, int newMaxAmmo)
    {
        // Remove older weapon 
        if (weaponHolder != null)
        {
            foreach (Transform child in weaponHolder)
            {
                Destroy(child.gameObject);
            }

            // instantiate new weapon prefab
            if (weaponModelPrefab != null)
            {
                currentWeaponModel = Instantiate(weaponModelPrefab, weaponHolder);
                currentWeaponModel.transform.localPosition = Vector3.zero;
                currentWeaponModel.transform.localRotation = Quaternion.identity;

                // update new fire point from the new weapon model
                Transform newFirePoint = currentWeaponModel.transform.Find("FirePoint");
                if (newFirePoint != null)
                {
                    firePoint = newFirePoint;
                }
                else
                {
                    Debug.LogWarning("The new weapon does not have a child object named 'FirePoint'. Please create one for accurate shooting.");
                }
            }
        }
        else
        {
            Debug.LogWarning("weaponHolder is not assigned in PlayerController!");
        }

        // Update bullet prefab and fire rate
        if (newBulletPrefab != null) 
        {
            bulletPrefab = newBulletPrefab;
        }
        fireRate = newFireRate;
        maxAmmo = newMaxAmmo;
        currentAmmo = newMaxAmmo < 0 ? -1 : newMaxAmmo;
        rangedWeaponEquipped = weaponModelPrefab != null;
        if (currentWeaponModel != null)
        {
            currentWeaponModel.SetActive(true);
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
            sync.SendWeaponSwitch("melee");
        }

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            sync.SendWeaponSwitch("ranged");
        }
    }

    private void SyncWeaponStateFromServer()
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

        if (currentWeaponModel != null)
        {
            bool shouldShowRangedModel = state.currentWeapon == state.rangedWeapon && rangedWeaponEquipped;
            if (currentWeaponModel.activeSelf != shouldShowRangedModel)
            {
                currentWeaponModel.SetActive(shouldShowRangedModel);
            }
        }

        bool isMeleeEquipped = !string.IsNullOrEmpty(state.currentWeapon) && state.currentWeapon == state.meleeWeapon;
        SetWeaponTypeAnimation(isMeleeEquipped);

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
            return false;
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

            bestDistance = distance;
            bestTarget = targetSync;
        }

        if (bestTarget == null)
        {
            return;
        }

        sync.SendMeleeAttack(bestTarget.GetSessionId());
        TriggerAttackAnimation();
        nextFireTime = Time.time + meleeAttackCooldown;
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

    private void SetWeaponTypeAnimation(bool isMeleeEquipped)
    {
        int weaponType = isMeleeEquipped ? 0 : 1;
        SetAnimatorIntIfPresent(PWeaponType, weaponType);
        SetAnimatorFloatIfPresent(PWeaponTypeFloat, weaponType);
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

    private bool CanShoot()
    {
        if (bulletPrefab == null || firePoint == null)
        {
            return false;
        }

        return maxAmmo < 0 || currentAmmo > 0;
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
