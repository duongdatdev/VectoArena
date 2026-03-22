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

    private float nextFireTime = 0f;
    private Rigidbody rb;
    private Camera mainCam;
    
    private GameControls gameControls;
    private Vector2 moveInput;
    private Vector2 mousePos;
    private bool isShooting;

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
        gameControls.Gameplay.Enable();
    }

    private void OnDisable()
    {
        gameControls.Gameplay.Disable();
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
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
    }

    private void FixedUpdate()
    {
        Vector3 movement = new Vector3(moveInput.x, 0f, moveInput.y);
        rb.MovePosition(rb.position + movement * moveSpeed * Time.fixedDeltaTime);
    }
}
