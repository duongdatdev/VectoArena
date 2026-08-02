using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Spawns floating damage numbers above the victim's head when any player takes damage.
// Inspired by Blast Royale's PlayerStatusBarElement: a fixed pool of labels (no runtime
// allocation), Bezier upward motion with a random horizontal offset per hit, a scale
// "bump" that scales with damage, and a fade-out.
//
// Setup in the Gameplay HUD:
//  - Put this on a Screen Space - Overlay (or Camera) Canvas.
//  - Assign `damageNumberPrefab` = a TextMeshProUGUI prefab (single label, RectTransform).
//  - Leave `worldCamera` empty to auto-use Camera.main.
public class DamageNumberManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("A TextMeshProUGUI prefab used as one floating damage label.")]
    public TextMeshProUGUI damageNumberPrefab;
    [Tooltip("Camera used to project world positions to screen. Defaults to Camera.main.")]
    public Camera worldCamera;

    [Header("Pool")]
    public int poolSize = 24;

    [Header("Animation")]
    [Tooltip("Seconds for one damage number to rise and fade.")]
    public float animDuration = 0.9f;
    [Tooltip("World-space vertical offset above the victim origin where the number spawns.")]
    public float headHeight = 2.0f;
    [Tooltip("Screen-space pixels the number travels upward over its lifetime.")]
    public float risePixels = 90f;
    [Tooltip("Random horizontal spread (pixels) so stacked hits don't overlap.")]
    public float horizontalSpread = 40f;

    [Header("Colors")]
    public Color normalColor = Color.white;
    public Color lethalColor = new Color(1f, 0.35f, 0.2f);

    private RectTransform canvasRect;
    private readonly List<DamageNumberInstance> pool = new List<DamageNumberInstance>();
    private readonly List<DamageNumberInstance> active = new List<DamageNumberInstance>();
    private int nextIndex;

    private class DamageNumberInstance
    {
        public TextMeshProUGUI label;
        public RectTransform rect;
        public float elapsed;
        public float lifetime;
        public Vector2 basePos;
        public float horizontalOffset;
        public Color color;
        public bool playing;
    }

    private void Awake()
    {
        canvasRect = transform as RectTransform;
        if (damageNumberPrefab == null)
        {
            Debug.LogWarning("[DamageNumberManager] damageNumberPrefab is not assigned.");
            return;
        }

        for (int i = 0; i < Mathf.Max(1, poolSize); i++)
        {
            var label = Instantiate(damageNumberPrefab, transform);
            label.gameObject.SetActive(false);
            pool.Add(new DamageNumberInstance
            {
                label = label,
                rect = label.rectTransform,
            });
        }
    }

    private void OnEnable()
    {
        NetworkManager.OnDamageTaken += HandleDamageTaken;
    }

    private void OnDisable()
    {
        NetworkManager.OnDamageTaken -= HandleDamageTaken;
    }

    private Camera ResolveCamera()
    {
        if (worldCamera != null) return worldCamera;
        worldCamera = Camera.main;
        return worldCamera;
    }

    private void HandleDamageTaken(NetworkManager.DamageTakenMessage message)
    {
        if (message == null || pool.Count == 0) return;
        if (NetworkManager.Instance == null) return;
        if (!NetworkManager.Instance.TryGetPlayerObject(message.victimId, out var victim) || victim == null) return;

        Camera cam = ResolveCamera();
        if (cam == null) return;

        Vector3 worldPos = victim.transform.position + Vector3.up * headHeight;
        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
        if (screenPos.z < 0f) return; // Behind camera.

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPos, canvasRect.GetComponent<Canvas>()?.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam,
                out Vector2 localPos))
        {
            return;
        }

        SpawnNumber(localPos, Mathf.RoundToInt(message.damage), message.lethal);
    }

    private void SpawnNumber(Vector2 localPos, int damage, bool lethal)
    {
        DamageNumberInstance instance = pool[nextIndex];
        nextIndex = (nextIndex + 1) % pool.Count;

        instance.label.gameObject.SetActive(true);
        instance.label.text = damage.ToString();
        instance.basePos = localPos;
        instance.horizontalOffset = Random.Range(-horizontalSpread, horizontalSpread);
        instance.color = lethal ? lethalColor : normalColor;
        instance.elapsed = 0f;
        instance.lifetime = Mathf.Max(0.1f, animDuration);
        instance.playing = true;

        if (!active.Contains(instance)) active.Add(instance);
    }

    private void Update()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            DamageNumberInstance instance = active[i];
            if (!instance.playing) { active.RemoveAt(i); continue; }

            instance.elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(instance.elapsed / instance.lifetime);

            // Upward rise + slight horizontal drift (Bezier-like ease-out on Y).
            float easeY = 1f - (1f - t) * (1f - t);
            Vector2 pos = instance.basePos;
            pos.x += instance.horizontalOffset * t;
            pos.y += risePixels * easeY;
            instance.rect.anchoredPosition = pos;

            // Scale bump: quick overshoot then settle, bigger for higher damage.
            float scale = t < 0.15f
                ? Mathf.Lerp(0.4f, 1.3f, t / 0.15f)
                : t < 0.35f
                    ? Mathf.Lerp(1.3f, 1f, (t - 0.15f) / 0.2f)
                    : 1f;
            instance.rect.localScale = new Vector3(scale, scale, 1f);

            // Fade out over the second half.
            float alpha = Mathf.Clamp01(1f - Mathf.InverseLerp(0.5f, 1f, t));
            Color c = instance.color;
            c.a = alpha;
            instance.label.color = c;

            if (t >= 1f)
            {
                instance.playing = false;
                instance.label.gameObject.SetActive(false);
                active.RemoveAt(i);
            }
        }
    }
}
