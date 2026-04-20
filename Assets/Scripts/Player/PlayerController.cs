using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5;
    
    [Header("Shooting  Settings")]
    public GameObject bulletPrefab;
    public Transform firePoint;
    public float fireRate;

    [Header("Weapon Settings")]
    public Transform weaponHolder;
    private GameObject currentWeaponModel;

    private float nextFireTime = 0f;
    private Rigidbody rb;
    private Camera mainCam;
    
    private GameControls gameControls;
    private Vector2 moveInput;
    private Vector2 mousePos;
    private bool isShooting;

    private Animator anim;

    private void Awake()
    {
        gameControls = new GameControls();

        gameControls.Gameplay.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        gameControls.Gameplay.Move.canceled += ctx => moveInput = Vector2.zero;

        gameControls.Gameplay.Aim.performed += ctx => mousePos = ctx.ReadValue<Vector2>();

        gameControls.Gameplay.Shoot.started += ctx => isShooting = true;
        gameControls.Gameplay.Shoot.canceled += ctx => isShooting = false;
    }

    private void OnEnable()
    {
        gameControls?.Gameplay.Enable();
    }

    private void OnDisable()
    {
        gameControls?.Gameplay.Disable();
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody>();

        anim = GetComponent<Animator>();
        
        mainCam = Camera.main;
    }

    private void Update()
    {
        Ray ray = mainCam.ScreenPointToRay(mousePos);

        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

        if (groundPlane.Raycast(ray, out float rayDistance))
        {
            Vector3 point = ray.GetPoint(rayDistance);

            Vector3 lookDirection = point - transform.position;

            lookDirection.y = 0f;

            transform.forward = lookDirection;
        }

        if (isShooting && Time.time >= nextFireTime)
        {
            Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);

            var sync = GetComponent<NetworkPlayerSync>();
            if (sync != null)
            {
                sync.SendShoot(firePoint.position, firePoint.rotation);
            }

            nextFireTime = Time.time + fireRate;
        }
        
        UpdateAnimation();
    }

    private void FixedUpdate()
    {
        Vector3 movement = new Vector3(moveInput.x, 0f, moveInput.y);
        rb.MovePosition(rb.position + movement * moveSpeed * Time.fixedDeltaTime);

        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        rb.angularVelocity = Vector3.zero;
    }

    private void OnCollisionEnter(Collision other)
    {
        if (other.gameObject.name == "DamageBlock")
        {
            Debug.Log("Player hit damage block");
            gameObject.GetComponent<Health>().TakeDamage(20);
        }
    }

    private void UpdateAnimation()
    {
        bool isWalking = moveInput.magnitude > 0.1f;
        
        anim.SetBool("isWalking", isWalking);
        anim.SetBool("isHoldingRight", isShooting);
    }

    public void EquipWeapon(GameObject weaponModelPrefab, GameObject newBulletPrefab, float newFireRate)
    {
        // Remove older weapon 
        if (weaponHolder != null)
        {
            foreach (Transform child in weaponHolder)
            {
                Destroy(child.gameObject);
            }

            // instantiate new weapon prefab
            if (weaponModelPrefab != null)
            {
                currentWeaponModel = Instantiate(weaponModelPrefab, weaponHolder);
                currentWeaponModel.transform.localPosition = Vector3.zero;
                currentWeaponModel.transform.localRotation = Quaternion.identity;

                // update new fire point from the new weapon model
                Transform newFirePoint = currentWeaponModel.transform.Find("FirePoint");
                if (newFirePoint != null)
                {
                    firePoint = newFirePoint;
                }
                else
                {
                    Debug.LogWarning("The new weapon does not have a child object named 'FirePoint'. Please create one for accurate shooting.");
                }
            }
        }
        else
        {
            Debug.LogWarning("weaponHolder is not assigned in PlayerController!");
        }

        // Update bullet prefab and fire rate
        if (newBulletPrefab != null) 
        {
            bulletPrefab = newBulletPrefab;
        }
        fireRate = newFireRate;
    }
}
