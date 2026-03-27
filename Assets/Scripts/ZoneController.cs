using UnityEngine;

public class ZoneController : MonoBehaviour
{
    [Header("Material Settings")]
    [Tooltip("Assign the Fullscreen Material that uses the BaseZoneShader here")]
    public Material zoneColorMat;

    [Header("Zone References")]
    [Tooltip("An empty GameObject representing the center of the zone")]
    public Transform zoneCenterObject; 

    [Header("Zone Parameters")]
    public float currentRadius = 50f;
    public float targetRadius = 10f;
    public float shrinkSpeed = 3f;

    void Update()
    {
        // 1. Shrink the zone radius over time
        if (currentRadius > targetRadius)
        {
            currentRadius -= shrinkSpeed * Time.deltaTime;
        }

        // 2. Pass parameters to the Fullscreen Shader
        if (zoneColorMat != null)
        {
            zoneColorMat.SetFloat("_ZoneRadius", currentRadius);
            
            if (zoneCenterObject != null)
            {
                zoneColorMat.SetVector("_ZoneCenter", zoneCenterObject.position);
            }
        }
    }
}