using System;
using UnityEngine;
using UnityEngine.UI;

public class Health : MonoBehaviour
{
    [Header("Health Settings")] public float maxHealth = 100f;

    public float currentHealth;

    [Header("Health Bar")] public Image healthBarFill;

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
        if (healthBarFill != null || healthBarLookupDone) return;
        if (cachedSync == null || !cachedSync.isLocalPlayer) return;

        healthBarLookupDone = true;
        var hpImageObj = GameObject.Find("HealthFill");
        if (hpImageObj != null)
        {
            healthBarFill = hpImageObj.GetComponent<Image>();
        }
        else
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

        healthBarFill.fillAmount = currentHealth / maxHealth;
    }
}
