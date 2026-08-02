#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class MobileControlsEditorMenu
{
    private const string MenuPath = "VectoArena/Mobile/Simulate Touch Controls";

    [MenuItem(MenuPath)]
    private static void ToggleSimulation()
    {
        bool enabled = PlayerPrefs.GetInt(MobileControlsController.SimulationPreferenceKey, 0) != 1;
        PlayerPrefs.SetInt(MobileControlsController.SimulationPreferenceKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
        Menu.SetChecked(MenuPath, enabled);
        Debug.Log($"Mobile touch control simulation {(enabled ? "enabled" : "disabled")}.");
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateSimulation()
    {
        Menu.SetChecked(MenuPath, PlayerPrefs.GetInt(MobileControlsController.SimulationPreferenceKey, 0) == 1);
        return true;
    }
}
#endif
