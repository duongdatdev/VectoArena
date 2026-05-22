using UnityEngine;

public class CharacterAnimationEventReceiver : MonoBehaviour
{
    public System.Action OnStepLeft;
    public System.Action OnStepRight;

    public Transform leftFootAnchor;
    public Transform rightFootAnchor;

    private void Awake()
    {
        if (leftFootAnchor == null)
        {
            leftFootAnchor = FindChildByName("Foot.L");
        }

        if (rightFootAnchor == null)
        {
            rightFootAnchor = FindChildByName("Foot.R");
        }
    }

    public void StepLeft()
    {
        OnStepLeft?.Invoke();
    }

    public void StepRight()
    {
        OnStepRight?.Invoke();
    }

    private Transform FindChildByName(string targetName)
    {
        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        foreach (Transform t in transforms)
        {
            if (string.Equals(t.name, targetName, System.StringComparison.OrdinalIgnoreCase))
            {
                return t;
            }
        }

        return null;
    }
}
