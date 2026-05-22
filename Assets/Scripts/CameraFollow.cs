using System;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Targer to follow")] public Transform target;
    [Header("Camera offset")] public Vector3 offset = new Vector3(0f, 15f, -10f);
    private Vector3 baseOffset;

    private void Awake()
    {
        baseOffset = offset;
        GameSettings.Initialize();
        ApplySettings();
    }

    private void OnEnable()
    {
        GameSettings.Changed += ApplySettings;
    }

    private void OnDisable()
    {
        GameSettings.Changed -= ApplySettings;
    }

    private void LateUpdate()
    {
        if (target != null)
        {
            transform.position = target.position + offset;
        }
    }

    private void ApplySettings()
    {
        float distanceScale = GameSettings.CameraDistance;
        offset = baseOffset * distanceScale;
    }
}
