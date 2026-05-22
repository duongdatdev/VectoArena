using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class ZoneController : MonoBehaviour
{
    // Define the states of the zone
    public enum ZoneState 
    { 
        Waiting,    
        Shrinking, 
        MatchEnded
    }

    [Header("Zone State")]
    public ZoneState currentState = ZoneState.Waiting;
    
    [Header("Current Zone (Orange)")]
    public Vector3 currentCenter;
    public float currentRadius = 100f;

    [Header("Next Zone (White)")]
    public Vector3 nextCenter;
    public float nextRadius;

    // seconds to wait before the zone starts shrinking
    public float waitTime = 10f;       
    [Header("Timing Settings")]
    // seconds it takes for the zone to fully shrink
    public float shrinkDuration = 15f; 
    // how much smaller the next zone gets (0.5 = 50%)
    public float shrinkFactor = 0.5f;  
    
    [Header("Damage Settings")]
    [Tooltip("Damage per second when outside the zone")]
    public float damagePerSecond = 5f;
    [Tooltip("Damage increases each phase")]
    public float damageMultiplierPerPhase = 1.5f;
    
    // internal timers and references for smooth animation
    private float timer = 0f;
    private Vector3 startShrinkCenter;
    private float startShrinkRadius;
    private int currentPhase = 0;
    private float currentDamagePerSecond;
    private bool serverAuthoritative = false;

    // target values for smooth interpolation
    private Vector3 targetCenter;
    private float targetRadius;

    void Start()
    {
        currentCenter = Vector3.zero;
        currentDamagePerSecond = damagePerSecond;

        GenerateNextZone();
        UpdateShader();
        
        Debug.Log($"Zone initialized. Current Radius: {currentRadius}, Next Radius: {nextRadius}");
    }

    void Update()
    {
        // stop calculating if the match is over
        if (currentState == ZoneState.MatchEnded) return;

        if (serverAuthoritative)
        {
            // smoothly interpolate current visual state to target state
            currentRadius = Mathf.Lerp(currentRadius, targetRadius, Time.deltaTime * 5f);
            currentCenter = Vector3.Lerp(currentCenter, targetCenter, Time.deltaTime * 5f);
            
            // still increment timer locally for a smooth UI, server will periodically overwrite it
            timer += Time.deltaTime;
            
            UpdateShader();
            return;
        }

        timer += Time.deltaTime;

        switch (currentState)
        {
            case ZoneState.Waiting:
                // check if the waiting period is over
                if (timer >= waitTime)
                {
                    currentState = ZoneState.Shrinking;
                    //reset timer for the shrink phase
                    timer = 0f; 
                    
                    //save the starting point for smooth interpolation
                    startShrinkCenter = currentCenter;
                    startShrinkRadius = currentRadius;
                    
                    Debug.Log($"Zone is shrinking! Phase {currentPhase + 1}");
                }
                break;

            case ZoneState.Shrinking:
                //calculate percentage of completion (from 0.0 to 1.0)
                float progress = Mathf.Clamp01(timer / shrinkDuration);
                
                //smoothly interpolate the radius and the center
                currentRadius = Mathf.Lerp(startShrinkRadius, nextRadius, progress);
                currentCenter = Vector3.Lerp(startShrinkCenter, nextCenter, progress);

                //check if the shrink phase is complete
                if (progress >= 1f)
                {
                    //snap to exact values to avoid float precision issues
                    currentRadius = nextRadius;
                    currentCenter = nextCenter;
                    
                    //increase phase counter
                    currentPhase++;
                    
                    //if the zone is incredibly small, end the match
                    if (currentRadius <= 1f) 
                    {
                        currentState = ZoneState.MatchEnded;
                        Debug.Log("Match Ended! Final Zone closed.");
                    }
                    else
                    {
                        //increase damage for the next phase
                        currentDamagePerSecond *= damageMultiplierPerPhase;
                        
                        //setup the next phase
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
        // Apply the offset to the current center
        if (currentPhase != 0)
        {
            // Calculate the new smaller radius
            nextRadius = currentRadius * shrinkFactor;
        
            // Ensure minimum radius
            if (nextRadius < 1f)
            {
                nextRadius = 0f;
            }

            // Calculate the maximum distance the center can shift without spilling out of the current zone
            float maxOffset = currentRadius - nextRadius;
        
            // Pick a random point within a 2D circle for the offset
            Vector2 randomOffset = Random.insideUnitCircle * maxOffset;
            
            nextCenter = currentCenter + new Vector3(randomOffset.x, 0f, randomOffset.y);
        }
        else
        {
            nextRadius = 50;
            nextCenter = Vector3.zero;
        }
        
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
    
    /// <summary>
    /// Check is position outside next zone
    /// </summary>
    public bool IsPositionOutsideNextZone(Vector3 targetPosition)
    {
        if (currentState == ZoneState.MatchEnded) return false;
        
        Vector2 targetPosXZ = new Vector2(targetPosition.x, targetPosition.z);
        Vector2 nextCenterXZ = new Vector2(nextCenter.x, nextCenter.z);
        
        float distance = Vector2.Distance(targetPosXZ, nextCenterXZ);
        return distance > nextRadius;
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

    private void OnEnable()
    {
        Shader.SetGlobalFloat("_GlobalZoneIntensity", 1f);
    }

    private void OnDisable()
    {
        Shader.SetGlobalFloat("_GlobalZoneIntensity", 0f);
    }

    public void SetServerAuthoritative(bool enabled)
    {
        serverAuthoritative = enabled;
        if (serverAuthoritative)
        {
            timer = 0f;
        }
    }

    public void ApplyState(VectoArena.Schema.ZoneState zoneState)
    {
        currentState = zoneState.currentState == "SHRINKING" ? ZoneState.Shrinking : zoneState.currentState == "MATCHENDED" ? ZoneState.MatchEnded : ZoneState.Waiting;
        
        targetCenter = new Vector3(zoneState.currentCenterX, currentCenter.y, zoneState.currentCenterZ);
        targetRadius = zoneState.currentRadius;
        
        nextCenter = new Vector3(zoneState.nextCenterX, nextCenter.y, zoneState.nextCenterZ);
        nextRadius = zoneState.nextRadius;
        
        currentPhase = (int)zoneState.currentPhase;
        currentDamagePerSecond = zoneState.currentDamagePerSecond;
        timer = zoneState.timer;
        waitTime = zoneState.waitTime;
        shrinkDuration = zoneState.shrinkDuration;
        shrinkFactor = zoneState.shrinkFactor;

        // If we are extremely far from target, snap immediately to prevent huge visual jumps across the map
        if (Mathf.Abs(currentRadius - targetRadius) > 50f || currentRadius == 0f || Vector3.Distance(currentCenter, targetCenter) > 20f)
        {
            currentRadius = targetRadius;
            currentCenter = targetCenter;
        }
    }
}
