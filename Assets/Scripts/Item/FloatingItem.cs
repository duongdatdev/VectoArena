using UnityEngine;

public class FloatingItem : MonoBehaviour
{
    [Header("Rotation Settings")]
    public float rotationSpeed = 90f;
    public Vector3 rotationAxis = Vector3.up;

    private void Update()
    {
        //rotate the item based on deltaTime
        transform.Rotate(rotationAxis, rotationSpeed * Time.deltaTime);
    }
}
