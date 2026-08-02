using NUnit.Framework;
using UnityEngine;

public class MobileInputMathTests
{
    [Test]
    public void DeadZone_ReturnsZeroInsideThreshold()
    {
        Assert.That(MobileInputMath.ApplyRadialDeadZone(new Vector2(0.1f, 0f), 0.16f), Is.EqualTo(Vector2.zero));
    }

    [Test]
    public void DeadZone_RemapsOuterRangeToOne()
    {
        Vector2 result = MobileInputMath.ApplyRadialDeadZone(new Vector2(1f, 0f), 0.16f);
        Assert.That(result.x, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(result.y, Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void DeadZone_ClampsInputOutsideJoystickRadius()
    {
        Vector2 result = MobileInputMath.ApplyRadialDeadZone(new Vector2(3f, 4f), 0.16f);

        Assert.That(result.magnitude, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(result.x, Is.EqualTo(0.6f).Within(0.0001f));
        Assert.That(result.y, Is.EqualTo(0.8f).Within(0.0001f));
    }

    [Test]
    public void CameraRelativeDirection_ProjectsAxesOntoGroundPlane()
    {
        Vector3 result = MobileInputMath.CameraRelativeDirection(
            new Vector2(1f, 1f),
            new Vector3(1f, 0.5f, 0f),
            new Vector3(0f, -0.7f, 1f));

        Assert.That(result.y, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(result.magnitude, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(result.x, Is.GreaterThan(0f));
        Assert.That(result.z, Is.GreaterThan(0f));
    }
}
