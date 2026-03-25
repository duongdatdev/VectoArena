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
        currentHealth -= damage;

        if (currentHealth < 0)
        {
            currentHealth = 0;
        }

        UpdateHealthBar();

        Debug.Log(gameObject.name + " take damage");

        if (currentHealth == 0)
        {
            Die();
        }
    }

    void Die()
    {
        Debug.Log(gameObject.name + " dead");

        Destroy(gameObject);
    }

    void UpdateHealthBar()
    {
        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = currentHealth / maxHealth;
        }
    }
}