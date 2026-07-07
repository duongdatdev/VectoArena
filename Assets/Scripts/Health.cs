using UnityEngine;
using UnityEngine.UI;

public class Health : MonoBehaviour
{
    [Header("Health Settings")] public float maxHealth = 100f;

    public float currentHealth;

    [Header("Health Bar")] public Image healthBarFill;

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
        if (Mathf.Abs(currentHealth - health) > 0.1f)
        {
            Debug.Log($"[Health] {gameObject.name} HP synced from server: {health}");
        }

        currentHealth = Mathf.Max(0f, health);
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
