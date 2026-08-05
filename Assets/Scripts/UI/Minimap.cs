using UnityEngine;

public class Minimap : MonoBehaviour
{
    [Header("References")]
    // Reference to the main player transform
    public Transform player;

    [Header("Settings")]
    // Set to true to rotate the minimap with the player's view
    public bool rotateWithPlayer = false;

    [Header("Mobile Performance")]
    [SerializeField, Min(1f)] private float mobileRefreshRate = 10f;
    [SerializeField, Min(64)] private int mobileTextureSize = 256;

    private Camera minimapCamera;
    private Camera mainCamera;
    private CameraFollow cameraFollow;
    private bool usesManualRendering;
    private float nextRenderTime;

    private void Awake()
    {
        minimapCamera = GetComponent<Camera>();
        usesManualRendering = Application.isMobilePlatform && minimapCamera != null;

        if (usesManualRendering)
        {
            // A 3D minimap does not need to redraw at the display refresh rate.
            minimapCamera.enabled = false;
            ResizeTargetTextureForMobile();
        }
    }

    private void ResizeTargetTextureForMobile()
    {
        RenderTexture target = minimapCamera.targetTexture;
        if (target == null || (target.width <= mobileTextureSize && target.height <= mobileTextureSize))
        {
            return;
        }

        target.Release();
        target.width = mobileTextureSize;
        target.height = mobileTextureSize;
        target.useMipMap = false;
        target.autoGenerateMips = false;
        target.Create();
    }

    private void LateUpdate()
    {
        if (player == null)
        {
            // Auto-find the player target from the Main Camera's follow script
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            if (mainCamera != null && cameraFollow == null)
            {
                cameraFollow = mainCamera.GetComponent<CameraFollow>();
            }

            if (cameraFollow != null)
            {
                player = cameraFollow.target;
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

        if (usesManualRendering && Time.unscaledTime >= nextRenderTime)
        {
            minimapCamera.Render();
            nextRenderTime = Time.unscaledTime + 1f / mobileRefreshRate;
        }
    }
}
