using System;
using System.Collections.Generic;
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
            RemoveDuplicateSkinRoots(playerRoot.transform, item.Id);
            return ResolveSkinAnimator(playerRoot);
        }

        GameObject skinPrefab = Resources.Load<GameObject>(item.PrefabResourcePath);
        if (skinPrefab == null)
        {
            Debug.LogWarning($"Skin prefab not found: {item.PrefabResourcePath}");
            return ResolveSkinAnimator(playerRoot);
        }

        RuntimeAnimatorController fallbackAnimatorController = ResolveSkinAnimator(playerRoot)?.runtimeAnimatorController;
        RuntimeAnimatorController skinAnimatorController = ResolveSkinAnimatorController(item);
        RemoveAllSkinRoots(playerRoot.transform);

        GameObject skinInstance = UnityEngine.Object.Instantiate(skinPrefab, playerRoot.transform);
        skinInstance.name = skinPrefab.name;
        skinInstance.transform.localPosition = Vector3.zero;
        skinInstance.transform.localRotation = Quaternion.identity;
        skinInstance.transform.localScale = Vector3.one;
        skinInstance.layer = playerRoot.layer;
        SetLayerRecursively(skinInstance.transform, playerRoot.layer);

        Animator animator = skinInstance.GetComponentInChildren<Animator>(true);
        if (animator != null && skinAnimatorController != null)
        {
            animator.runtimeAnimatorController = skinAnimatorController;
        }
        else if (animator != null && animator.runtimeAnimatorController == null && fallbackAnimatorController != null)
        {
            animator.runtimeAnimatorController = fallbackAnimatorController;
        }

        EnsureAnimationEventReceiver(animator);

        return animator;
    }

    private static void RemoveAllSkinRoots(Transform playerRoot)
    {
        List<Transform> skinRoots = new List<Transform>();
        foreach (Transform child in playerRoot)
        {
            if (child.name.StartsWith("Char_", StringComparison.OrdinalIgnoreCase))
            {
                skinRoots.Add(child);
            }
        }

        foreach (Transform skinRoot in skinRoots)
        {
            skinRoot.gameObject.SetActive(false);
            skinRoot.SetParent(null);
            UnityEngine.Object.Destroy(skinRoot.gameObject);
        }
    }

    private static RuntimeAnimatorController ResolveSkinAnimatorController(SkinCatalogItem item)
    {
        RuntimeAnimatorController controller = SkinCatalog.LoadAnimatorController(item);
        if (controller == null && item != null && !string.IsNullOrEmpty(item.AnimatorControllerResourcePath))
        {
            Debug.LogWarning($"Skin animator controller not found: {item.AnimatorControllerResourcePath}");
        }

        return controller;
    }

    private static void RemoveDuplicateSkinRoots(Transform playerRoot, string skinIdToKeep)
    {
        Transform keep = null;
        List<Transform> skinRootsToRemove = new List<Transform>();

        foreach (Transform child in playerRoot)
        {
            if (!child.name.StartsWith("Char_", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (keep == null && string.Equals(child.name, $"Char_{skinIdToKeep}", StringComparison.OrdinalIgnoreCase))
            {
                keep = child;
                continue;
            }

            skinRootsToRemove.Add(child);
        }

        foreach (Transform skinRoot in skinRootsToRemove)
        {
            skinRoot.gameObject.SetActive(false);
            skinRoot.SetParent(null);
            UnityEngine.Object.Destroy(skinRoot.gameObject);
        }
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
                EnsureAnimationEventReceiver(skinAnimator);
                return skinAnimator;
            }
        }

        Animator animator = playerRoot.GetComponentInChildren<Animator>(true);
        EnsureAnimationEventReceiver(animator);
        return animator;
    }

    private static void EnsureAnimationEventReceiver(Animator animator)
    {
        if (animator != null && !animator.TryGetComponent<CharacterAnimationEventReceiver>(out _))
        {
            animator.gameObject.AddComponent<CharacterAnimationEventReceiver>();
        }
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
