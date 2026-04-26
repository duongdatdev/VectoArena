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

    private void Start()
    {
        animator = GetComponent<Animator>();
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

        // remote player animation can be derived from movement.
        float distance = Vector3.Distance(transform.position, targetPosition);
        bool isWalking = distance > 0.01f;
        animator.SetBool("isWalking", isWalking);

        // if you want to sync shooting or other actions, add flags to the PlayerState schema
        // and set them here like animator.SetBool("isHoldingRight", state.isShooting);

        lastRemotePosition = transform.position;
    }

    public string GetSessionId()
    {
        return sessionId;
    }

    public void SendHit(string targetId)
    {
        if (!isLocalPlayer || room == null) return;
        
        room.Send("hit", new
        {
            targetId = targetId
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
