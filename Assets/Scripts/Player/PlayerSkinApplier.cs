using System;
using UnityEngine;

public static class PlayerSkinApplier
{
    public static Animator ApplyEquippedSkin(GameObject playerRoot)
    {
        return ApplySkin(playerRoot, PlayerInventory.EquippedSkinId);
    }

    public static Animator ApplySkin(GameObject playerRoot, string skinId)
    {
        if (playerRoot == null)
        {
            return null;
        }

        SkinCatalogItem item = SkinCatalog.GetById(skinId);
        if (string.IsNullOrEmpty(item.PrefabResourcePath))
        {
            return ResolveSkinAnimator(playerRoot);
        }

        GameObject skinPrefab = Resources.Load<GameObject>(item.PrefabResourcePath);
        if (skinPrefab == null)
        {
            Debug.LogWarning($"Skin prefab not found: {item.PrefabResourcePath}");
            return ResolveSkinAnimator(playerRoot);
        }

        RuntimeAnimatorController animatorController = ResolveSkinAnimator(playerRoot)?.runtimeAnimatorController;
        Transform previousSkin = FindCurrentSkinRoot(playerRoot.transform);
        if (previousSkin != null)
        {
            UnityEngine.Object.Destroy(previousSkin.gameObject);
        }

        GameObject skinInstance = UnityEngine.Object.Instantiate(skinPrefab, playerRoot.transform);
        skinInstance.name = skinPrefab.name;
        skinInstance.transform.localPosition = Vector3.zero;
        skinInstance.transform.localRotation = Quaternion.identity;
        skinInstance.transform.localScale = Vector3.one;
        skinInstance.layer = playerRoot.layer;
        SetLayerRecursively(skinInstance.transform, playerRoot.layer);

        Animator animator = skinInstance.GetComponentInChildren<Animator>(true);
        if (animator != null && animator.runtimeAnimatorController == null && animatorController != null)
        {
            animator.runtimeAnimatorController = animatorController;
        }

        if (animator != null && !animator.TryGetComponent<CharacterAnimationEventReceiver>(out _))
        {
            animator.gameObject.AddComponent<CharacterAnimationEventReceiver>();
        }

        return animator;
    }

    private static Transform FindCurrentSkinRoot(Transform playerRoot)
    {
        foreach (Transform child in playerRoot)
        {
            if (child.name.StartsWith("Char_", StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }

        return null;
    }

    public static Animator ResolveSkinAnimator(GameObject playerRoot)
    {
        if (playerRoot == null)
        {
            return null;
        }

        Transform skinRoot = FindCurrentSkinRoot(playerRoot.transform);
        if (skinRoot != null)
        {
            Animator skinAnimator = skinRoot.GetComponentInChildren<Animator>(true);
            if (skinAnimator != null)
            {
                return skinAnimator;
            }
        }

        return playerRoot.GetComponentInChildren<Animator>(true);
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;

        foreach (Transform child in root)
        {
            SetLayerRecursively(child, layer);
        }
    }
}
