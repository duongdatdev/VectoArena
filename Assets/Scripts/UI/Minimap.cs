using UnityEngine;

public class Minimap : MonoBehaviour
{
    [Header("References")]
    // Reference to the main player transform
    public Transform player;

    [Header("Settings")]
    // Set to true to rotate the minimap with the player's view
    public bool rotateWithPlayer = false;

    private void LateUpdate()
    {
        if (player == null)
        {
            // Auto-find the player target from the Main Camera's follow script
            if (Camera.main != null && Camera.main.GetComponent<CameraFollow>() != null)
            {
                player = Camera.main.GetComponent<CameraFollow>().target;
            }
            if (player == null) return;
        }

        // Update the Minimap Camera's position to follow the player
        // Keep the camera's original altitude (Y axis) constant
        Vector3 newPosition = player.position;
        newPosition.y = transform.position.y;
        transform.position = newPosition;

        // Synchronize the rotation of the minimap if enabled
        if (rotateWithPlayer)
        {
            // The camera faces downwards (X = 90) and rotates around the Y axis based on the player
            transform.rotation = Quaternion.Euler(90f, player.eulerAngles.y, 0f);
        }
    }
}
