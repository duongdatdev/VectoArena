using UnityEngine;

public static class MobileInputMath
{
    public static Vector2 ApplyRadialDeadZone(Vector2 value, float deadZone)
    {
        float magnitude = Mathf.Clamp01(value.magnitude);
        if (magnitude <= deadZone || magnitude <= Mathf.Epsilon)
        {
            return Vector2.zero;
        }

        float remappedMagnitude = Mathf.InverseLerp(deadZone, 1f, magnitude);
        return value.normalized * remappedMagnitude;
    }

    public static Vector3 CameraRelativeDirection(Vector2 input, Vector3 cameraRight, Vector3 cameraForward)
    {
        if (input.sqrMagnitude <= 0.001f)
        {
            return Vector3.zero;
        }

        cameraRight.y = 0f;
        cameraForward.y = 0f;
        cameraRight.Normalize();
        cameraForward.Normalize();
        return Vector3.ClampMagnitude(cameraRight * input.x + cameraForward * input.y, 1f);
    }
}
