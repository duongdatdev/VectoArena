using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Local-player health bar with a delayed damage indicator.
/// The orange damage layer lingers briefly before catching up with the current HP.
/// </summary>
public sealed class PlayerHealthBarUI : MonoBehaviour
{
    [SerializeField] private Image healthFill;
    [SerializeField] private Image damageFill;
    [SerializeField] private TMP_Text healthLabel;
    [SerializeField, Min(0f)] private float damageHoldDuration = 0.35f;
    [SerializeField, Min(0.01f)] private float damageCatchUpDuration = 1.15f;

    private Coroutine damageAnimation;
    private float lastNormalizedHealth;
    private bool hasHealthValue;

    public Image HealthFill => healthFill;

    private void Awake()
    {
        if (healthFill == null)
        {
            healthFill = transform.Find("HealthFill")?.GetComponent<Image>();
        }

        if (damageFill == null)
        {
            damageFill = transform.Find("DamageFill")?.GetComponent<Image>();
        }

        if (healthLabel == null)
        {
            healthLabel = transform.Find("HealthValue")?.GetComponent<TMP_Text>();
        }
    }

    public void SetHealth(float current, float maximum)
    {
        float normalized = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;

        if (healthLabel != null)
        {
            healthLabel.text = Mathf.CeilToInt(Mathf.Max(0f, current)).ToString();
        }

        if (healthFill != null)
        {
            healthFill.fillAmount = normalized;
        }

        if (damageFill == null)
        {
            return;
        }

        if (!hasHealthValue)
        {
            lastNormalizedHealth = normalized;
            hasHealthValue = true;
            damageFill.fillAmount = normalized;
            return;
        }

        // NetworkPlayerSync sends the authoritative HP every frame. Repeated values
        // must not restart the delayed damage animation, or the red layer never fades.
        if (Mathf.Approximately(normalized, lastNormalizedHealth))
        {
            return;
        }

        bool tookDamage = normalized < lastNormalizedHealth;
        lastNormalizedHealth = normalized;

        if (damageAnimation != null)
        {
            StopCoroutine(damageAnimation);
            damageAnimation = null;
        }

        // Healing is reflected immediately. Only HP loss uses the delayed red layer.
        if (!tookDamage)
        {
            damageFill.fillAmount = normalized;
            return;
        }

        damageAnimation = StartCoroutine(AnimateDamageFill(normalized));
    }

    private IEnumerator AnimateDamageFill(float target)
    {
        yield return new WaitForSecondsRealtime(damageHoldDuration);

        float start = damageFill.fillAmount;
        float elapsed = 0f;
        while (elapsed < damageCatchUpDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            damageFill.fillAmount = Mathf.Lerp(start, target, elapsed / damageCatchUpDuration);
            yield return null;
        }

        damageFill.fillAmount = target;
        damageAnimation = null;
    }
}
