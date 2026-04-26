using UnityEngine;
using UnityEngine.UI; 

public class WeaponPickup : MonoBehaviour
{
    [Header("Weapon Stats")]
    [Tooltip("The weapon model that will appear on the player's hands")]
    public GameObject weaponModelPrefab;
    [Tooltip("The bullet this weapon will shoot")]
    public GameObject bulletPrefab;
    [Tooltip("The fire rate of this weapon")]
    public float fireRate = 0.5f;
    [Tooltip("Maximum ammo for this weapon (-1 = unlimited)")]
    public int maxAmmo = -1;

    [Header("Pickup Settings")]
    [Tooltip("Required waiting time to pick up the item (in seconds)")]
    public float pickupTimeRequired = 2.0f;
    [Tooltip("The radial loading circle UI (Image with fill method Radial 360)")]
    public Image loadingCircleUI;

    private float currentPickupTime = 0f;
    private bool isPlayerInZone = false;
    private PlayerController playerInZone;
    private NetworkPlayerSync playerSync;
    private string syncedItemId;
    private bool pickupRequested = false;
    private float lastProgressSent = -1f;

    public void Initialize(string itemId)
    {
        syncedItemId = itemId;
        SetSyncedPickupProgress(0f, false);
    }

    private void Start()
    {
        if (loadingCircleUI != null)
        {
            loadingCircleUI.fillAmount = 0f;
            loadingCircleUI.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (isPlayerInZone && playerInZone != null)
        {
            // increase the timer using DeltaTime while the player is inside the trigger zone
            currentPickupTime += Time.deltaTime;

            if (loadingCircleUI != null)
            {
                loadingCircleUI.fillAmount = currentPickupTime / pickupTimeRequired;
            }

            if (playerSync != null && !string.IsNullOrEmpty(syncedItemId))
            {
                float normalizedProgress = Mathf.Clamp01(currentPickupTime / pickupTimeRequired);
                if (pickupRequested || Mathf.Abs(normalizedProgress - lastProgressSent) >= 0.02f)
                {
                    playerSync.SendPickupProgress(syncedItemId, normalizedProgress);
                    lastProgressSent = normalizedProgress;
                }
            }

            if (currentPickupTime >= pickupTimeRequired)
            {
                RequestPickup();
            }
        }
    }

    private void RequestPickup()
    {
        if (pickupRequested || playerSync == null || string.IsNullOrEmpty(syncedItemId))
        {
            return;
        }

        pickupRequested = true;
        playerSync.SendPickupItem(syncedItemId);
    }

    public void SetSyncedPickupProgress(float progress, bool isActive)
    {
        if (loadingCircleUI == null)
        {
            return;
        }

        if (isActive)
        {
            loadingCircleUI.gameObject.SetActive(true);
            loadingCircleUI.fillAmount = Mathf.Clamp01(progress);
            return;
        }

        loadingCircleUI.fillAmount = 0f;
        loadingCircleUI.gameObject.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            NetworkPlayerSync sync = other.GetComponent<NetworkPlayerSync>();
            if (sync == null || !sync.isLocalPlayer)
            {
                return;
            }

            PlayerController pc = other.GetComponent<PlayerController>();
            if (pc != null)
            {
                playerInZone = pc;
                playerSync = sync;
                isPlayerInZone = true;
                
                //show circle
                if (loadingCircleUI != null)
                {
                    loadingCircleUI.gameObject.SetActive(true);
                    loadingCircleUI.fillAmount = 0f;
                }

                if (!string.IsNullOrEmpty(syncedItemId))
                {
                    playerSync.SendPickupProgress(syncedItemId, 0f);
                    lastProgressSent = 0f;
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            NetworkPlayerSync sync = other.GetComponent<NetworkPlayerSync>();
            if (sync == null || !sync.isLocalPlayer)
            {
                return;
            }

            isPlayerInZone = false;
            playerInZone = null;
            playerSync = null;
            currentPickupTime = 0f;
            pickupRequested = false;
            lastProgressSent = -1f;

            if (!string.IsNullOrEmpty(syncedItemId) && sync != null)
            {
                sync.SendPickupProgress(syncedItemId, 0f);
            }

            if (loadingCircleUI != null)
            {
                loadingCircleUI.fillAmount = 0f;
                loadingCircleUI.gameObject.SetActive(false);
            }
        }
    }
}
