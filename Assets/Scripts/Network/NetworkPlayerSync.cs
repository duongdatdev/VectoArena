using UnityEngine;
using VectoArena.Schema;
using Colyseus;
using System;
using System.Collections.Generic;

public class NetworkPlayerSync : MonoBehaviour
{
    private PlayerState state;
    private string sessionId;
    private Room<GameState> room;
    public bool isLocalPlayer { get; private set; }

    [Header("Interpolation Settings")]
    [SerializeField] private float positionLerpSpeed = 10f;
    [SerializeField] private float rotationLerpSpeed = 10f;

    private Animator animator;
    private Vector3 lastRemotePosition;
    private readonly HashSet<int> animatorParameterHashes = new HashSet<int>();

    private static readonly int PIsWalking = Animator.StringToHash("isWalking");
    private static readonly int PIsHoldingRight = Animator.StringToHash("isHoldingRight");
    private static readonly int PMoving = Animator.StringToHash("moving");
    private static readonly int PAiming = Animator.StringToHash("aiming");
    private static readonly int PWeaponType = Animator.StringToHash("weapon_type");
    private static readonly int PWeaponTypeFloat = Animator.StringToHash("weapon_type_float");
    private static readonly int PDeath = Animator.StringToHash("death");

    private bool isDeadHandled = false;

    private void Start()
    {
        RefreshAnimator();
    }

    public void RefreshAnimator()
    {
        animator = PlayerSkinApplier.ResolveSkinAnimator(gameObject);
        CacheAnimatorParameters();
    }

    public void RefreshAnimator(Animator resolvedAnimator)
    {
        animator = resolvedAnimator != null ? resolvedAnimator : PlayerSkinApplier.ResolveSkinAnimator(gameObject);
        CacheAnimatorParameters();
    }

    public void Initialize(PlayerState playerState, string sid, Room<GameState> roomInstance)
    {
        this.state = playerState;
        this.sessionId = sid;
        this.room = roomInstance;
        this.isLocalPlayer = (state.id == room.SessionId);

        if (!isLocalPlayer)
        {
            // initial position and rotation for remote player
            transform.position = new Vector3(state.x, state.y, state.z);
            transform.rotation = Quaternion.Euler(0, state.rotation, 0);
            lastRemotePosition = transform.position;

            // script that should only run for the local player
            if (TryGetComponent<PlayerController>(out var controller))
            {
                controller.enabled = false;
            }

            if (TryGetComponent<Rigidbody>(out var rb))
            {
                rb.isKinematic = true;
            }
        }
    }

    private void Update()
    {
        if (state == null) return;

        // Automatically sync health every frame to ensure it matches Server state
        var healthComponent = GetComponentInChildren<Health>();
        if (healthComponent != null)
        {
            healthComponent.SetHealth(state.hp);
        }

        if (state.isDead)
        {
            if (!isDeadHandled)
            {
                isDeadHandled = true;
                if (TryGetComponent<Collider>(out var collider))
                {
                    collider.enabled = false;
                }
                SetAnimatorTriggerIfPresent(PDeath);
                if (HasAnimatorParameter(PDeath)) 
                {
                    animator.SetBool(PDeath, true);
                }
                else
                {
                    // Fallback to playing the state directly if the parameter doesn't exist
                    animator.Play("death");
                }
            }
            return; // Skip syncing movement if dead
        }

        if (isLocalPlayer)
        {
            SyncLocalMovementToServer();
        }
        else
        {
            SyncRemoteMovementToClient();
        }
    }

    private void SyncLocalMovementToServer()
    {
        // send every frame for now, but in production, you'd want rate limiting.
        room.Send("move", new
        {
            x = transform.position.x,
            y = transform.position.y,
            z = transform.position.z,
            rotation = transform.eulerAngles.y
        });
    }

    public void SendShoot(Vector3 position, Quaternion rotation)
    {
        if (!isLocalPlayer || room == null) return;

        room.Send("shoot", new {
            x = position.x,
            y = position.y,
            z = position.z,
            rx = rotation.eulerAngles.x,
            ry = rotation.eulerAngles.y,
            rz = rotation.eulerAngles.z
        });
    }

    private void SyncRemoteMovementToClient()
    {
        // smoothly interpolate to the target position and rotation from the server
        Vector3 targetPosition = new Vector3(state.x, state.y, state.z);
        Quaternion targetRotation = Quaternion.Euler(0, state.rotation, 0);

        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * positionLerpSpeed);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * rotationLerpSpeed);

        UpdateRemoteAnimation(targetPosition);
    }

    private void UpdateRemoteAnimation(Vector3 targetPosition)
    {
        if (animator == null) return;

        float distance = Vector3.Distance(transform.position, targetPosition);
        bool isWalking = distance > 0.01f;
        bool isUsingMeleeWeapon = !string.IsNullOrEmpty(state.currentWeapon) && state.currentWeapon == state.meleeWeapon;
        bool isHoldingRight = !isUsingMeleeWeapon;

        SetAnimatorBoolIfPresent(PIsWalking, isWalking);
        SetAnimatorBoolIfPresent(PMoving, isWalking);
        SetAnimatorBoolIfPresent(PIsHoldingRight, isHoldingRight);
        SetAnimatorBoolIfPresent(PAiming, isHoldingRight);

        int weaponType = GetWeaponTypeValue(state.currentWeapon, isUsingMeleeWeapon);
        SetAnimatorIntIfPresent(PWeaponType, weaponType);
        SetAnimatorFloatIfPresent(PWeaponTypeFloat, weaponType);

        lastRemotePosition = transform.position;
    }

    private void CacheAnimatorParameters()
    {
        animatorParameterHashes.Clear();

        if (animator == null)
        {
            return;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            animatorParameterHashes.Add(parameter.nameHash);
        }
    }

    private bool HasAnimatorParameter(int parameterHash)
    {
        return animator != null && animatorParameterHashes.Contains(parameterHash);
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
            case "Rifle": return 2;
            case "BurstRifle": return 2;
            case "RebelRifle": return 2;
            case "MachineGun": return 2;
            case "Shotgun": return 9;
            case "BlasterShotgun": return 9;
            case "Sniper": return 4;
            case "HunterSniper": return 4;
            case "Launcher": return 5;
            case "Minigun": return 3;
            case "Pistol": return 8;
            case "Knife": return 7;
            case "XLMelee": return 6;
            case "SniperGun": return 4;
            case "XLGun": return 3;
            case "Handgun": return 8;
            default: return 2;
        }
    }

    private void SetAnimatorBoolIfPresent(int parameterHash, bool value)
    {
        if (HasAnimatorParameter(parameterHash))
        {
            animator.SetBool(parameterHash, value);
        }
    }

    private void SetAnimatorTriggerIfPresent(int parameterHash)
    {
        if (HasAnimatorParameter(parameterHash))
        {
            animator.SetTrigger(parameterHash);
        }
    }

    private void SetAnimatorIntIfPresent(int parameterHash, int value)
    {
        if (HasAnimatorParameter(parameterHash))
        {
            animator.SetInteger(parameterHash, value);
        }
    }

    private void SetAnimatorFloatIfPresent(int parameterHash, float value)
    {
        if (HasAnimatorParameter(parameterHash))
        {
            animator.SetFloat(parameterHash, value);
        }
    }

    public string GetSessionId()
    {
        return sessionId;
    }

    public PlayerState GetState()
    {
        return state;
    }

    public void SendHit(string targetId)
    {
        if (!isLocalPlayer || room == null) return;
        
        room.Send("hit", new
        {
            targetId = targetId
        });
    }

    public void SendMeleeAttack(string targetId)
    {
        if (!isLocalPlayer || room == null) return;
        room.Send("melee_attack", new { targetId = targetId ?? string.Empty });
    }

    public void SendWeaponSwitch(string slot)
    {
        if (!isLocalPlayer || room == null || string.IsNullOrEmpty(slot)) return;

        room.Send("switch_weapon", new
        {
            slot = slot
        });
    }

    public void SendPickupItem(string itemId)
    {
        if (!isLocalPlayer || room == null || string.IsNullOrEmpty(itemId)) return;

        room.Send("pickup_item", new
        {
            itemId = itemId
        });
    }

    public void SendPickupProgress(string itemId, float progress)
    {
        if (!isLocalPlayer || room == null || string.IsNullOrEmpty(itemId)) return;

        room.Send("pickup_progress", new
        {
            itemId = itemId,
            progress = Mathf.Clamp01(progress)
        });
    }
}
