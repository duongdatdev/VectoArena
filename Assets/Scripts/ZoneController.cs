using UnityEngine;

public class ZoneController : MonoBehaviour
{
    // Define the states of the zone
    public enum ZoneState 
    { 
        Waiting,    // Players have time to loot/move
        Shrinking,  // The orange border is moving towards the white border
        MatchEnded  // The zone has fully closed
    }

    [Header("Zone State")]
    public ZoneState currentState = ZoneState.Waiting;
    
    [Header("Current Zone (Orange)")]
    public Vector3 currentCenter;
    public float currentRadius = 100f;

    [Header("Next Zone (White)")]
    public Vector3 nextCenter;
    public float nextRadius;

    [Header("Timing Settings")]
    public float waitTime = 10f;       // Seconds to wait before the zone starts shrinking
    public float shrinkDuration = 15f; // Seconds it takes for the zone to fully shrink
    public float shrinkFactor = 0.5f;  // How much smaller the next zone gets (0.5 = 50%)
    
    [Header("Damage Settings")]
    [Tooltip("Damage per second when outside the zone")]
    public float damagePerSecond = 5f;
    [Tooltip("Damage increases each phase")]
    public float damageMultiplierPerPhase = 1.5f;
    
    // Internal timers and references for smooth animation
    private float timer = 0f;
    private Vector3 startShrinkCenter;
    private float startShrinkRadius;
    private int currentPhase = 0;
    private float currentDamagePerSecond;

    void Start()
    {
        // Initialize the first safe zone randomly inside the starting map
        currentCenter = Vector3.zero; // Assuming map center is 0,0,0
        currentDamagePerSecond = damagePerSecond;
        
        // Generate the first next zone
        GenerateNextZone();
        UpdateShader();
        
        Debug.Log($"Zone initialized. Current Radius: {currentRadius}, Next Radius: {nextRadius}");
    }

    void Update()
    {
        // Stop calculating if the match is over
        if (currentState == ZoneState.MatchEnded) return;

        timer += Time.deltaTime;

        switch (currentState)
        {
            case ZoneState.Waiting:
                // Check if the waiting period is over
                if (timer >= waitTime)
                {
                    // Transition to Shrinking state
                    currentState = ZoneState.Shrinking;
                    timer = 0f; // Reset timer for the shrink phase
                    
                    // Save the starting point for smooth interpolation
                    startShrinkCenter = currentCenter;
                    startShrinkRadius = currentRadius;
                    
                    Debug.Log($"Zone is shrinking! Phase {currentPhase + 1}");
                }
                break;

            case ZoneState.Shrinking:
                // Calculate percentage of completion (from 0.0 to 1.0)
                float progress = Mathf.Clamp01(timer / shrinkDuration);
                
                // Smoothly interpolate the radius and the center
                currentRadius = Mathf.Lerp(startShrinkRadius, nextRadius, progress);
                currentCenter = Vector3.Lerp(startShrinkCenter, nextCenter, progress);

                // Check if the shrink phase is complete
                if (progress >= 1f)
                {
                    // Snap to exact values to avoid float precision issues
                    currentRadius = nextRadius;
                    currentCenter = nextCenter;
                    
                    // Increase phase counter
                    currentPhase++;
                    
                    // If the zone is incredibly small, end the match
                    if (currentRadius <= 1f) 
                    {
                        currentState = ZoneState.MatchEnded;
                        Debug.Log("Match Ended! Final Zone closed.");
                    }
                    else
                    {
                        // Increase damage for the next phase
                        currentDamagePerSecond *= damageMultiplierPerPhase;
                        
                        // Setup the next phase
                        GenerateNextZone();
                        currentState = ZoneState.Waiting;
                        timer = 0f;
                        
                        Debug.Log($"Zone reached. Phase {currentPhase} complete. Next damage: {currentDamagePerSecond}/s. Waiting for next phase...");
                    }
                }
                break;
        }

        // Send updated data to the GPU every frame
        UpdateShader();
    }

    /// <summary>
    /// Calculates a random center and smaller radius for the next safe zone.
    /// Ensures the next zone is entirely contained within the current zone.
    /// </summary>
    private void GenerateNextZone()
    {
        // Calculate the new smaller radius
        nextRadius = currentRadius * shrinkFactor;
        
        // Ensure minimum radius
        if (nextRadius < 1f)
        {
            nextRadius = 1f;
        }

        // Calculate the maximum distance the center can shift without spilling out of the current zone
        float maxOffset = currentRadius - nextRadius;
        
        // Pick a random point within a 2D circle for the offset
        Vector2 randomOffset = Random.insideUnitCircle * maxOffset;
        
        // Apply the offset to the current center (using X and Z axes)
        nextCenter = currentCenter + new Vector3(randomOffset.x, 0f, randomOffset.y);
        
        Debug.Log($"Next zone generated. Center: {nextCenter}, Radius: {nextRadius}");
    }

    /// <summary>
    /// Syncs the C# variables with the Shader Graph properties.
    /// </summary>
    private void UpdateShader()
    {
        Shader.SetGlobalFloat("_GlobalZoneRadius", currentRadius);
        Shader.SetGlobalVector("_GlobalZoneCenter", currentCenter);
        
        Shader.SetGlobalFloat("_GlobalNextZoneRadius", nextRadius);
        Shader.SetGlobalVector("_GlobalNextZoneCenter", nextCenter);
    }

    // =========================================================
    // HELPER METHODS FOR OTHER SCRIPTS (e.g., PlayerHealth)
    // =========================================================

    /// <summary>
    /// Checks if a given position is outside the current safe zone.
    /// Used to apply damage to players outside the ring.
    /// </summary>
    public bool IsPositionOutsideZone(Vector3 targetPosition)
    {
        // Match ended, no damage
        if (currentState == ZoneState.MatchEnded) return false;
        
        // Flatten the Y axis because the zone is a vertical cylinder
        Vector2 targetPosXZ = new Vector2(targetPosition.x, targetPosition.z);
        Vector2 centerXZ = new Vector2(currentCenter.x, currentCenter.z);
        
        float distance = Vector2.Distance(targetPosXZ, centerXZ);
        return distance > currentRadius;
    }
    
    /// <summary>
    /// Gets the current damage per second for being outside the zone.
    /// </summary>
    public float GetCurrentDamagePerSecond()
    {
        return currentDamagePerSecond;
    }
    
    /// <summary>
    /// Gets the current phase number (starts at 0).
    /// </summary>
    public int GetCurrentPhase()
    {
        return currentPhase;
    }
    
    /// <summary>
    /// Gets the time remaining in the current state.
    /// </summary>
    public float GetTimeRemaining()
    {
        switch (currentState)
        {
            case ZoneState.Waiting:
                return waitTime - timer;
            case ZoneState.Shrinking:
                return shrinkDuration - timer;
            default:
                return 0f;
        }
    }
    
    /// <summary>
    /// Gets the progress of the current shrink phase (0 to 1).
    /// </summary>
    public float GetShrinkProgress()
    {
        if (currentState == ZoneState.Shrinking)
        {
            return Mathf.Clamp01(timer / shrinkDuration);
        }
        return 0f;
    }

    // Optional: Gizmo visualization in the Editor
    private void OnDrawGizmos()
    {
        // Draw current zone (orange)
        Gizmos.color = Color.yellow;
        DrawCircle(currentCenter, currentRadius);
        
        // Draw next zone (white)
        Gizmos.color = Color.white;
        DrawCircle(nextCenter, nextRadius);
    }
    
    private void DrawCircle(Vector3 center, float radius, int segments = 50)
    {
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + new Vector3(radius, 0f, 0f);
        
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 newPoint = center + new Vector3(
                Mathf.Cos(angle) * radius,
                0f,
                Mathf.Sin(angle) * radius
            );
            
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }
}