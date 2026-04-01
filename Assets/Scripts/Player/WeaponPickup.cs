using UnityEngine;
using UnityEngine.UI; // Để tương tác với UI (Image)
using System.Collections;

public class WeaponPickup : MonoBehaviour
{
    [Header("Weapon Stats")]
    [Tooltip("Mô hình súng sẽ xuất hiện trên tay nhân vật")]
    public GameObject weaponModelPrefab;
    [Tooltip("Viên đạn súng này sẽ bắn ra")]
    public GameObject bulletPrefab;
    [Tooltip("Tốc độ bắn của súng này")]
    public float fireRate = 0.5f;

    [Header("Pickup Settings")]
    [Tooltip("Thời gian chờ cần thiết để nhặt (giây)")]
    public float pickupTimeRequired = 2.0f;
    [Tooltip("Thanh loading vòng tròn (UI Image có fill method Radial 360)")]
    public Image loadingCircleUI;

    private float currentPickupTime = 0f;
    private bool isPlayerInZone = false;
    private PlayerController playerInZone;

    private void Start()
    {
        // Khởi tạo vòng tròn bằng 0
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
            // Tăng thời gian theo DeltaTime khi người chơi đứng trong vòng
            currentPickupTime += Time.deltaTime;

            // Cập nhật UI
            if (loadingCircleUI != null)
            {
                loadingCircleUI.fillAmount = currentPickupTime / pickupTimeRequired;
            }

            // Hoàn thành vòng tải
            if (currentPickupTime >= pickupTimeRequired)
            {
                PickUpWeapon();
            }
        }
    }

    private void PickUpWeapon()
    {
        // Yêu cầu PlayerController thay đổi súng
        playerInZone.EquipWeapon(weaponModelPrefab, bulletPrefab, fireRate);

        // Huỷ bỏ vật phẩm trên cảnh
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
                
                // Hiển thị vòng tròn UI
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
            // Reset trạng thái nếu người chơi đi ra khỏi vòng tròn
            isPlayerInZone = false;
            playerInZone = null;
            currentPickupTime = 0f;

            if (loadingCircleUI != null)
            {
                loadingCircleUI.fillAmount = 0f;
                loadingCircleUI.gameObject.SetActive(false); // Ẩn UI đi khi không dùng đến
            }
        }
    }
}
