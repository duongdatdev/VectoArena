using System;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    [Header("Bullet properties")]
    public float moveSpeed = 20f;
    public float lifeTime = 2f;

    private void Start()
    {
        GetComponent<Rigidbody>().linearVelocity = transform.forward * moveSpeed;
        
        Destroy(gameObject, lifeTime);
    }

    private void OnCollisionEnter(Collision other)
    {
        Health targetHealth = other.gameObject.GetComponent<Health>();

        if (targetHealth != null)
        {
            targetHealth.TakeDamage(10);
        }
        
        Destroy(gameObject);
    }
}
