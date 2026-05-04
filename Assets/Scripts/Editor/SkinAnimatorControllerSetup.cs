using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class SkinAnimatorControllerSetup
{
    private const string ControllerPath = "Assets/AddressableResources/Collections/CharacterSkins/Shared/character_animator.controller";
    private static readonly string[] SkinPrefabPaths =
    {
        "Assets/Resources/CharacterSkins/Female02/Char_Female02.prefab",
        "Assets/Resources/CharacterSkins/CorposFemale/Char_CorposFemale.prefab",
        "Assets/Resources/CharacterSkins/AssassinFemale/Char_AssassinFemale.prefab"
    };

    [MenuItem("VectoArena/Setup Skin Animator Controllers")]
    public static void SetupSkinAnimatorControllers()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"Cannot find character animator controller at {ControllerPath}");
            return;
        }

        foreach (string prefabPath in SkinPrefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"Cannot find skin prefab at {prefabPath}");
                continue;
            }

            Animator animator = prefab.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                Debug.LogWarning($"Skin prefab has no Animator: {prefabPath}");
                continue;
            }

            animator.runtimeAnimatorController = controller;
            EditorUtility.SetDirty(animator);
            EditorUtility.SetDirty(prefab);
            PrefabUtility.SavePrefabAsset(prefab);
            Debug.Log($"Assigned character_animator.controller to {prefabPath}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
