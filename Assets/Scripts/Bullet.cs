using System;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    [Header("Bullet properties")]
    public float moveSpeed = 20f;
    public float lifeTime = 2f;
    public NetworkPlayerSync owner;

    private void Start()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = transform.forward * moveSpeed;
        }

        Destroy(gameObject, lifeTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleHit(other.gameObject);
    }

    private void OnCollisionEnter(Collision other)
    {
        HandleHit(other.gameObject);
    }

    private void HandleHit(GameObject targetObj)
    {
        if (owner != null && targetObj == owner.gameObject) return;

        NetworkPlayerSync targetSync = targetObj.GetComponent<NetworkPlayerSync>();
        if (targetSync != null && owner != null && owner.isLocalPlayer)
        {
            owner.SendHit(targetSync.GetSessionId());
        }
        
        Destroy(gameObject);
    }
}
