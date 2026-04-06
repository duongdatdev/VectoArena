using UnityEngine;
using UnityEngine.UI; 
using System.Collections;

public class WeaponPickup : MonoBehaviour
{
    [Header("Weapon Stats")]
    [Tooltip("The weapon model that will appear on the player's hands")]
    public GameObject weaponModelPrefab;
    [Tooltip("The bullet this weapon will shoot")]
    public GameObject bulletPrefab;
    [Tooltip("The fire rate of this weapon")]
    public float fireRate = 0.5f;

    [Header("Pickup Settings")]
    [Tooltip("Required waiting time to pick up the item (in seconds)")]
    public float pickupTimeRequired = 2.0f;
    [Tooltip("The radial loading circle UI (Image with fill method Radial 360)")]
    public Image loadingCircleUI;

    private float currentPickupTime = 0f;
    private bool isPlayerInZone = false;
    private PlayerController playerInZone;

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

            if (currentPickupTime >= pickupTimeRequired)
            {
                PickUpWeapon();
            }
        }
    }

    private void PickUpWeapon()
    {
        playerInZone.EquipWeapon(weaponModelPrefab, bulletPrefab, fireRate);

        Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerController pc = other.GetComponent<PlayerController>();
            if (pc != null)
            {
                playerInZone = pc;
                isPlayerInZone = true;
                
                //show circle
                if (loadingCircleUI != null)
                {
                    loadingCircleUI.gameObject.SetActive(true);
                    loadingCircleUI.fillAmount = 0f;
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInZone = false;
            playerInZone = null;
            currentPickupTime = 0f;

            if (loadingCircleUI != null)
            {
                loadingCircleUI.fillAmount = 0f;
                loadingCircleUI.gameObject.SetActive(false);
            }
        }
    }
}
