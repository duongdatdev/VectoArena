using UnityEngine;

public class VecPickupMarker : MonoBehaviour
{
    private const string MarkerName = "VEC_Pickup_Marker";

    [SerializeField] private float labelHeight = 2.25f;
    [SerializeField] private float pulseAmplitude = 0.16f;
    [SerializeField] private float pulseSpeed = 3.8f;

    private Transform markerRoot;
    private TextMesh label;
    private Light glow;

    private void Awake()
    {
        EnsureMarker();
    }

    private void OnEnable()
    {
        EnsureMarker();
    }

    private void LateUpdate()
    {
        if (markerRoot == null)
        {
            return;
        }

        float pulse = Mathf.Sin(Time.time * pulseSpeed) * pulseAmplitude;
        markerRoot.localPosition = new Vector3(0f, labelHeight + pulse, 0f);

        Camera cam = Camera.main;
        if (cam != null)
        {
            markerRoot.rotation = Quaternion.LookRotation(markerRoot.position - cam.transform.position);
        }

        if (glow != null)
        {
            glow.intensity = 2.3f + Mathf.Abs(pulse) * 4f;
        }
    }

    private void EnsureMarker()
    {
        if (markerRoot != null)
        {
            return;
        }

        Transform existing = transform.Find(MarkerName);
        markerRoot = existing != null ? existing : new GameObject(MarkerName).transform;
        markerRoot.SetParent(transform, false);
        markerRoot.localPosition = new Vector3(0f, labelHeight, 0f);

        label = markerRoot.GetComponent<TextMesh>();
        if (label == null)
        {
            label = markerRoot.gameObject.AddComponent<TextMesh>();
        }

        label.text = "VEC\nHOLD TO COLLECT";
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.fontSize = 34;
        label.characterSize = 0.055f;
        label.color = new Color(1f, 0.88f, 0.18f, 1f);

        glow = GetComponentInChildren<Light>();
        if (glow == null)
        {
            GameObject lightObject = new GameObject("VEC_Glow");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            glow = lightObject.AddComponent<Light>();
        }

        glow.type = LightType.Point;
        glow.color = new Color(0.22f, 1f, 0.72f, 1f);
        glow.range = 4.5f;
        glow.intensity = 2.4f;
    }
}
