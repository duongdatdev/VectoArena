using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class SkinAnimatorControllerSetup
{
    private const string ControllerPath = "Assets/Animations/Characters/character_animator.controller";
    private static readonly string[] SkinPrefabPaths =
    {
        "Assets/Resources/CharacterSkins/Female02/Char_Female02.prefab",
        "Assets/Resources/CharacterSkins/CorposFemale/Char_CorposFemale.prefab",
        "Assets/Resources/CharacterSkins/AssassinFemale/Char_AssassinFemale.prefab",
        "Assets/Resources/CharacterSkins/CyberBunny/Char_CyberBunny.prefab",
        "Assets/Resources/CharacterSkins/Iceking/Char_Iceking.prefab",
        "Assets/Resources/CharacterSkins/Anubis/Char_Anubis.prefab",
        "Assets/Resources/CharacterSkins/GearedApe/Char_GearedApe.prefab"
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

            RuntimeAnimatorController skinController = ResolveSkinController(prefabPath, controller);
            animator.runtimeAnimatorController = skinController;
            EditorUtility.SetDirty(animator);
            EditorUtility.SetDirty(prefab);
            PrefabUtility.SavePrefabAsset(prefab);
            Debug.Log($"Assigned {skinController.name} to {prefabPath}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static RuntimeAnimatorController ResolveSkinController(string prefabPath, RuntimeAnimatorController fallbackController)
    {
        string controllerPath = prefabPath.Substring(0, prefabPath.Length - ".prefab".Length) + "_animator.overrideController";
        RuntimeAnimatorController overrideController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
        return overrideController != null ? overrideController : fallbackController;
    }
}
