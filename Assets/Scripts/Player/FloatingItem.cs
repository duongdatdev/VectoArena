using UnityEngine;

public class FloatingItem : MonoBehaviour
{
    [Header("Rotation Settings")]
    public float rotationSpeed = 90f;
    public Vector3 rotationAxis = Vector3.up;

    [Header("Hover Settings")]
    public float hoverSpeed = 2f;
    public float hoverHeight = 0.5f;

    private float originalY;

    private void Start()
    {
        // Lưu vị trí Y ban đầu
        originalY = transform.position.y;
    }

    private void Update()
    {
        // Xoay vật phẩm theo deltaTime
        transform.Rotate(rotationAxis, rotationSpeed * Time.deltaTime);

        // Lơ lửng bằng hàm Sin
        float newY = originalY + Mathf.Sin(Time.time * hoverSpeed) * hoverHeight;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }
}
