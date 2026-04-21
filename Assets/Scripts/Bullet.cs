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
        GetComponent<Rigidbody>().linearVelocity = transform.forward * moveSpeed;
        
        Destroy(gameObject, lifeTime);
    }

    private void OnCollisionEnter(Collision other)
    {
        if (owner != null && other.gameObject == owner.gameObject) return;

        NetworkPlayerSync targetSync = other.gameObject.GetComponent<NetworkPlayerSync>();
        if (targetSync != null && owner != null && owner.isLocalPlayer)
        {
            owner.SendHit(targetSync.GetSessionId());
        }

        Health targetHealth = other.gameObject.GetComponent<Health>();
        if (targetHealth != null)
        {
            // Health deduction is now handled by the server
            // targetHealth.TakeDamage(10);
        }
        
        Destroy(gameObject);
    }
}
