using System;
using UnityEngine;
using UnityEngine.UI;

public class Health : MonoBehaviour
{
    [Header("Health Settings")] public float maxHealth = 100f;

    public float currentHealth;

    [Header("Health Bar")] public Image healthBarFill;

    private PlayerHealthBarUI playerHealthBar;

    // Fired when the local player loses HP. Argument is the damage amount (positive).
    public static event Action<float> OnLocalPlayerDamaged;

    private NetworkPlayerSync cachedSync;
    private bool healthBarLookupDone;

    private void Awake()
    {
        cachedSync = GetComponent<NetworkPlayerSync>();
    }

    private void Start()
    {
        currentHealth = maxHealth;
        TryResolveHealthBarFill();
        UpdateHealthBar();
    }

    public void TakeDamage(float damage)
    {
        // Damage is authoritative on the server via Colyseus.
        Debug.Log(gameObject.name + " wants to take damage, should send to server");
    }

    public void SetHealth(float health)
    {
        float clamped = Mathf.Max(0f, health);

        if (Mathf.Abs(currentHealth - clamped) > 0.1f)
        {
            Debug.Log($"[Health] {gameObject.name} HP synced from server: {clamped}");
        }

        // Detect damage (HP drop) on the local player to trigger visual feedback.
        float delta = currentHealth - clamped;
        if (delta > 0.1f && cachedSync != null && cachedSync.isLocalPlayer)
        {
            OnLocalPlayerDamaged?.Invoke(delta);
        }

        currentHealth = clamped;
        UpdateHealthBar();
    }

    private void TryResolveHealthBarFill()
    {
        if (healthBarLookupDone) return;
        if (cachedSync == null || !cachedSync.isLocalPlayer) return;

        healthBarLookupDone = true;
        var healthBarObject = GameObject.Find("PlayerHealthBar");
        if (healthBarObject != null)
        {
            playerHealthBar = healthBarObject.GetComponent<PlayerHealthBarUI>();
        }

        if (healthBarFill == null && playerHealthBar != null)
        {
            healthBarFill = playerHealthBar.HealthFill;
        }

        if (healthBarFill == null)
        {
            var hpImageObj = GameObject.Find("HealthFill");
            if (hpImageObj != null)
            {
                healthBarFill = hpImageObj.GetComponent<Image>();
            }
        }

        if (healthBarFill == null)
        {
            Debug.LogWarning("[Health] Cannot find GameObject named HealthFill");
        }
    }

    private void UpdateHealthBar()
    {
        if (healthBarFill == null)
        {
            TryResolveHealthBarFill();
            if (healthBarFill == null) return;
        }

        float normalizedHealth = maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
        healthBarFill.fillAmount = normalizedHealth;
        playerHealthBar?.SetHealth(currentHealth, maxHealth);
    }
}
