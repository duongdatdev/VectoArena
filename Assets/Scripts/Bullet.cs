using System;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    [Header("Bullet Properties (Blast Royale reference)")]
    [Tooltip("Bullet move speed in units/sec. Blast Royale range: Rifle=30, Shotgun/Sniper=40, Launcher=10")]
    public float moveSpeed = 35f;

    [Tooltip("Seconds before auto-destroy")]
    public float lifeTime = 1.5f;

    public NetworkPlayerSync owner;

    [Header("Trail Settings")]
    [Tooltip("Duration of the trail behind the bullet")]
    public float trailTime = 0.12f;

    [Tooltip("Start width of the trail")]
    public float trailStartWidth = 0.08f;

    [Tooltip("End width of the trail")]
    public float trailEndWidth = 0.0f;

    // Cached direction vector set once at spawn, ensuring it never changes even while player moves
    private Vector3 flyDirection;
    private bool alreadyHit;

    private static Material s_sharedTrailMaterial;

    private static Material GetSharedTrailMaterial()
    {
        if (s_sharedTrailMaterial != null) return s_sharedTrailMaterial;
        Shader shader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
        if (shader != null)
        {
            s_sharedTrailMaterial = new Material(shader) { name = "BulletTrail(Shared)" };
        }
        return s_sharedTrailMaterial;
    }

    private void Start()
    {
        // Lock the bullet's fly direction at spawn time. Use transform.forward and force Y=0
        // so the bullet always flies horizontally (like Blast Royale's 2D projectile system).
        flyDirection = transform.forward;
        flyDirection.y = 0f;
        flyDirection.Normalize();

        // Override the rotation to be perfectly flat (no tilt from firePoint bone)
        transform.rotation = Quaternion.LookRotation(flyDirection, Vector3.up);

        // If there's a Rigidbody, make it kinematic so physics doesn't affect the bullet trajectory.
        // Blast Royale moves bullets via position += direction * speed * deltaTime, not physics.
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
        }

        // Setup TrailRenderer for bullet trail VFX
        SetupTrail();

        Destroy(gameObject, lifeTime);
    }

    private void Update()
    {
        // Move the bullet in a straight line each frame, independent of physics.
        // This mirrors Blast Royale's ProjectileSystem: transform.Position += Direction * Speed * DeltaTime
        transform.position += flyDirection * moveSpeed * Time.deltaTime;
    }

    private void SetupTrail()
    {
        TrailRenderer trail = GetComponent<TrailRenderer>();
        if (trail == null)
        {
            trail = gameObject.AddComponent<TrailRenderer>();
        }

        trail.time = trailTime;
        trail.startWidth = trailStartWidth;
        trail.endWidth = trailEndWidth;
        trail.numCapVertices = 2;
        trail.numCornerVertices = 2;
        trail.minVertexDistance = 0.05f;

        // Blast Royale style gradient: bright yellow core fading to warm orange then transparent
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1f, 0.95f, 0.4f), 0.0f),
                new GradientColorKey(new Color(1f, 0.5f, 0.1f), 0.6f),
                new GradientColorKey(new Color(1f, 0.3f, 0.0f), 1.0f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1.0f, 0.0f),
                new GradientAlphaKey(0.6f, 0.5f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        trail.colorGradient = gradient;

        // Reuse a shared unlit material across all bullets to avoid per-shot allocations.
        Material shared = GetSharedTrailMaterial();
        if (shared != null)
        {
            trail.sharedMaterial = shared;
        }

        // Ensure it renders above ground plane
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleHit(other.gameObject);
    }

    private void HandleHit(GameObject targetObj)
    {
        if (alreadyHit) return;
        if (owner != null && targetObj == owner.gameObject) return;

        alreadyHit = true;

        NetworkPlayerSync targetSync = targetObj.GetComponent<NetworkPlayerSync>();
        if (targetSync != null && owner != null && owner.isLocalPlayer)
        {
            owner.SendHit(targetSync.GetSessionId());
        }

        Destroy(gameObject);
    }
}
