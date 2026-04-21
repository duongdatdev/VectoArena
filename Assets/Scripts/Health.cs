using System;
using UnityEngine;
using UnityEngine.UI;

public class Health : MonoBehaviour
{
    [Header("Health Settings")] public float maxHealth = 100f;

    public float currentHealth;

    [Header("Health Bar")] public Image healthBarFill;

    private void Start()
    {
        currentHealth = maxHealth;

        UpdateHealthBar();
    }

    public void TakeDamage(float damage)
    {
        // Now handled by Server via Colyseus instead
        // currentHealth -= damage;
        // UpdateHealthBar();
        Debug.Log(gameObject.name + " wants to take damage, should send to server");
    }

    public void SetHealth(float health)
    {
        if (Mathf.Abs(currentHealth - health) > 0.1f)
        {
            Debug.Log($"[Health] {gameObject.name} HP synced from server: {health}");
        }

        currentHealth = health;
        if (currentHealth < 0) currentHealth = 0;
        
        UpdateHealthBar();
        
        if (currentHealth == 0)
        {
            // Die();
        }
    }

    void Die()
    {
        Debug.Log(gameObject.name + " dead");
        Destroy(gameObject);
    }

    void UpdateHealthBar()
    {
        // Try to find the local player's health bar by tag or name if it's null
        if (healthBarFill == null && GetComponent<NetworkPlayerSync>() != null)
        {
            // Usually, we only show screen UI for the LOCAL player
            if (GetComponent<NetworkPlayerSync>().isLocalPlayer)
            {
                var hpImageObj = GameObject.Find("HealthFill");
                if (hpImageObj != null)
                {
                    healthBarFill = hpImageObj.GetComponent<Image>();
                }
                else
                {
                    Debug.LogWarning("can't find gameobject name HealthFill");
                }
            }
        }

        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = currentHealth / maxHealth;
        }
    }
}