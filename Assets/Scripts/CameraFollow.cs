using System;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Targer to follow")] public Transform target;
    [Header("Camera offset")] public Vector3 offset = new Vector3(0f, 15f, -10f);

    private void LateUpdate()
    {
        if (target != null)
        {
            transform.position = target.position + offset;
        }
    }
}
