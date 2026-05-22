using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class VectoAudioLibrarySetup
{
    private const string LibraryPath = "Assets/AddressableResources/Audio/VectoAudioLibrary.asset";
    private const string AudioRootPath = "Assets/AddressableResources/Audio";

    [MenuItem("VectoArena/Setup Audio Library")]
    public static void SetupAudioLibrary()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));

        VectoAudioLibrary library = AssetDatabase.LoadAssetAtPath<VectoAudioLibrary>(LibraryPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<VectoAudioLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }

        library.entries = BuildEntries();
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        Selection.activeObject = library;
    }

    [MenuItem("VectoArena/Create Audio Manager In Scene")]
    public static void CreateAudioManagerInScene()
    {
        SetupAudioLibrary();

        VectoAudioManager manager = UnityEngine.Object.FindAnyObjectByType<VectoAudioManager>();
        if (manager == null)
        {
            GameObject managerObject = new GameObject("VectoAudioManager");
            manager = managerObject.AddComponent<VectoAudioManager>();
        }

        VectoAudioLibrary library = AssetDatabase.LoadAssetAtPath<VectoAudioLibrary>(LibraryPath);
        SerializedObject serializedManager = new SerializedObject(manager);
        serializedManager.FindProperty("library").objectReferenceValue = library;
        serializedManager.ApplyModifiedProperties();
        EditorUtility.SetDirty(manager);
        Selection.activeObject = manager.gameObject;
    }

    private static List<VectoAudioEntry> BuildEntries()
    {
        return new List<VectoAudioEntry>
        {
            Entry(VectoAudioId.ButtonClickForward, 0.9f, "ButtonClickForward", "EnterGame", "ChooseGameMode"),
            Entry(VectoAudioId.ButtonClickBackward, 0.9f, "ButtonClickBackward", "UnequipEquipment"),
            Entry(VectoAudioId.EnterGame, 1f, "EnterGame"),
            Entry(VectoAudioId.ChooseGameMode, 1f, "ChooseGameMode"),
            Entry(VectoAudioId.MusicMainStart, 0.55f, "MusicMainStart"),
            Entry(VectoAudioId.MusicMainLoop, 0.55f, true, "MusicMainLoop"),
            Entry(VectoAudioId.MusicBrSkydiveLoop, 0.55f, true, "MusicBrSkydiveLoop"),
            Entry(VectoAudioId.MusicBrLowLoop, 0.55f, true, "MusicBrLowLoop"),
            Entry(VectoAudioId.MusicBrMidLoop, 0.55f, true, "MusicBrMidLoop"),
            Entry(VectoAudioId.MusicBrHighLoop, 0.55f, true, "MusicBrHighLoop"),
            Entry(VectoAudioId.MusicPostGameWinStart, 0.55f, "MusicPostGameWinStart"),
            Entry(VectoAudioId.MusicPostGameLoseStart, 0.55f, "MusicPostGameLoseStart"),
            Entry(VectoAudioId.MusicPostGameLoop, 0.55f, true, "MusicPostGameLoop"),
            Entry(VectoAudioId.WeaponPickup, 1f, "WeaponPickup"),
            Entry(VectoAudioId.WeaponPickupLocal, 1f, "WeaponPickupLocal", "WeaponPickup"),
            Entry(VectoAudioId.HealthPickup, 1f, "HealthPickup"),
            Entry(VectoAudioId.HealthPickupLocal, 1f, "HealthPickupLocal", "HealthPickup"),
            Entry(VectoAudioId.AmmoPickup, 1f, "AmmoPickup"),
            Entry(VectoAudioId.AmmoEmpty, 1f, "AmmoEmpty"),
            Entry(VectoAudioId.AssaultRifleShot, 0.8f, "AssaultRifleShot"),
            Entry(VectoAudioId.AssaultRifleShotLocal, 0.8f, "AssaultRifleShotLocal", "AssaultRifleShot"),
            Entry(VectoAudioId.ShotgunShot, 0.9f, "ShotgunShot"),
            Entry(VectoAudioId.ShotgunShotLocal, 0.9f, "ShotgunShotLocal", "ShotgunShot"),
            Entry(VectoAudioId.SniperShot, 0.9f, "SniperShot"),
            Entry(VectoAudioId.SniperShotLocal, 0.9f, "SniperShotLocal", "SniperShot"),
            Entry(VectoAudioId.PistolShot, 0.8f, "PistolShot"),
            Entry(VectoAudioId.PistolShotLocal, 0.8f, "PistolShotLocal", "PistolShot"),
            Entry(VectoAudioId.SMGShot, 0.8f, "SMGShot"),
            Entry(VectoAudioId.SMGShotLocal, 0.8f, "SMGShotLocal", "SMGShot"),
            Entry(VectoAudioId.MinigunShot, 0.8f, "MinigunShot"),
            Entry(VectoAudioId.MinigunShotLocal, 0.8f, "MinigunShotLocal", "MinigunShot"),
            Entry(VectoAudioId.QuadzookaShot, 0.9f, "QuadzookaShot"),
            Entry(VectoAudioId.QuadzookaShotLocal, 0.9f, "QuadzookaShotLocal", "QuadzookaShot"),
            Entry(VectoAudioId.DefaultMeleeShot, 0.9f, "MeleeDefaultShot"),
            Entry(VectoAudioId.DefaultMeleeShotLocal, 0.9f, "MeleeDefaultShotLocal", "MeleeDefaultShot"),
            Entry(VectoAudioId.KnifeMeleeShot, 0.9f, "MeleeKnifeShot"),
            Entry(VectoAudioId.KnifeMeleeShotLocal, 0.9f, "MeleeKnifeShotLocal", "MeleeKnifeShot"),
            Entry(VectoAudioId.ForestAmbientLoop, 0.45f, true, "ForestAmbientLoop"),
            Entry(VectoAudioId.CentralAmbientLoop, 0.45f, true, "CentralAmbientLoop")
        };
    }

    private static VectoAudioEntry Entry(VectoAudioId id, float volume, params string[] prefixes)
    {
        return Entry(id, volume, false, prefixes);
    }

    private static VectoAudioEntry Entry(VectoAudioId id, float volume, bool loop, params string[] prefixes)
    {
        VectoAudioEntry entry = new VectoAudioEntry
        {
            id = id,
            volume = volume,
            loop = loop,
            clips = FindClips(prefixes)
        };
        return entry;
    }

    private static List<AudioClip> FindClips(params string[] prefixes)
    {
        List<AudioClip> clips = new List<AudioClip>();
        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { AudioRootPath });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = Path.GetFileNameWithoutExtension(path);
            if (Matches(fileName, prefixes))
            {
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null && !clips.Contains(clip))
                {
                    clips.Add(clip);
                }
            }
        }

        return clips;
    }

    private static bool Matches(string fileName, string[] prefixes)
    {
        foreach (string prefix in prefixes)
        {
            if (fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
