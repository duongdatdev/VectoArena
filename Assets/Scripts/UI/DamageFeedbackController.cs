using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Handles local-player combat feedback: a red full-screen vignette flash when the
// LOCAL player takes damage. Hit confirmation on the enemy is handled elsewhere
// (the victim plays its built-in "hit" animation + a floating damage number),
// which suits a top-down camera better than a center-screen crosshair marker.
//
// Wire `damageVignette` in the Inspector (a full-screen red Image on the HUD canvas).
public class DamageFeedbackController : MonoBehaviour
{
    [Header("Damage Vignette (local player took damage)")]
    [Tooltip("Full-screen red Image overlay, alpha 0 by default.")]
    public Image damageVignette;
    [Tooltip("Peak alpha of the red flash.")]
    [Range(0f, 1f)] public float vignetteMaxAlpha = 0.45f;
    [Tooltip("Seconds for the red flash to fade back to 0.")]
    public float vignetteFadeDuration = 0.5f;

    private Coroutine vignetteRoutine;

    private void Awake()
    {
        SetImageAlpha(damageVignette, 0f);
    }

    private void OnEnable()
    {
        Health.OnLocalPlayerDamaged += HandleLocalDamaged;
    }

    private void OnDisable()
    {
        Health.OnLocalPlayerDamaged -= HandleLocalDamaged;
    }

    private void HandleLocalDamaged(float amount)
    {
        if (damageVignette == null) return;

        if (vignetteRoutine != null) StopCoroutine(vignetteRoutine);
        vignetteRoutine = StartCoroutine(FlashVignette());
    }

    private IEnumerator FlashVignette()
    {
        SetImageAlpha(damageVignette, vignetteMaxAlpha);

        float elapsed = 0f;
        while (elapsed < vignetteFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / vignetteFadeDuration);
            SetImageAlpha(damageVignette, Mathf.Lerp(vignetteMaxAlpha, 0f, t));
            yield return null;
        }

        SetImageAlpha(damageVignette, 0f);
        vignetteRoutine = null;
    }

    private static void SetImageAlpha(Image image, float alpha)
    {
        if (image == null) return;
        Color c = image.color;
        c.a = alpha;
        image.color = c;
    }
}
