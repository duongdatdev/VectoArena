using UnityEngine;
using UnityEditor;
using System.IO;

public static class WeaponDatabaseSetup
{
    private static readonly string DataFolder = "Assets/Data/Weapons";
    private static readonly string BlastPrefabFolder = "Assets/Prefabs/Weapons/Models";
    private static readonly string MeleePrefabFolder = "Assets/Prefabs/Weapons/Melee/Hammer";
    private static readonly string FloatingPrefabFolder = "Assets/Prefabs/Floating";
    private static readonly string BulletPrefabPath = "Assets/Prefabs/Bullet.prefab";

    private struct WeaponDef
    {
        public string type;
        public string blastPrefabName;
        public int animId;
        public float fireRate;
        public int maxAmmo;
    }

    private static readonly WeaponDef[] Weapons = new[]
    {
        new WeaponDef { type = "Sword",           blastPrefabName = "Melee_Hammer",           animId = 1, fireRate = 0.70f, maxAmmo = -1 },
        new WeaponDef { type = "Rifle",           blastPrefabName = "Weapon_Rifle",           animId = 2, fireRate = 0.10f, maxAmmo = 30 },
        new WeaponDef { type = "Shotgun",         blastPrefabName = "Weapon_Shotgun",         animId = 9, fireRate = 0.67f, maxAmmo = 8 },
        new WeaponDef { type = "Pistol",          blastPrefabName = "Weapon_Pistol",          animId = 8, fireRate = 0.20f, maxAmmo = 12 },
        new WeaponDef { type = "BurstRifle",      blastPrefabName = "Weapon_BurstRifle",      animId = 2, fireRate = 0.125f, maxAmmo = 24 },
        new WeaponDef { type = "Sniper",          blastPrefabName = "Weapon_Sniper",          animId = 4, fireRate = 1.25f, maxAmmo = 5 },
        new WeaponDef { type = "HunterSniper",    blastPrefabName = "Weapon_HunterSniper",    animId = 4, fireRate = 1.00f, maxAmmo = 6 },
        new WeaponDef { type = "Launcher",        blastPrefabName = "Weapon_Launcher",        animId = 5, fireRate = 2.00f, maxAmmo = 3 },
        new WeaponDef { type = "MachineGun",      blastPrefabName = "Weapon_MachineGun",      animId = 2, fireRate = 0.071f, maxAmmo = 50 },
        new WeaponDef { type = "Minigun",         blastPrefabName = "Weapon_Minigun",         animId = 3, fireRate = 0.05f, maxAmmo = 80 },
        new WeaponDef { type = "BlasterShotgun",  blastPrefabName = "Weapon_BlasterShotgun",  animId = 9, fireRate = 0.83f, maxAmmo = 6 },
        new WeaponDef { type = "RebelRifle",      blastPrefabName = "Weapon_RebelRifle",      animId = 2, fireRate = 0.111f, maxAmmo = 25 },
    };

    [MenuItem("VectoArena/Setup Weapon Database from Blast Royale Prefabs")]
    public static void SetupWeaponDatabase()
    {
        if (!AssetDatabase.IsValidFolder(DataFolder))
        {
            string parent = Path.GetDirectoryName(DataFolder).Replace("\\", "/");
            if (!AssetDatabase.IsValidFolder(parent))
            {
                AssetDatabase.CreateFolder("Assets", "Data");
            }
            AssetDatabase.CreateFolder(parent, "Weapons");
        }

        GameObject bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BulletPrefabPath);

        WeaponDatabase database = ScriptableObject.CreateInstance<WeaponDatabase>();

        var weaponsList = new System.Collections.Generic.List<WeaponData>();

        foreach (WeaponDef def in Weapons)
        {
            string assetPath = $"{DataFolder}/{def.type}.asset";
            WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>(assetPath);

            if (data == null)
            {
                data = ScriptableObject.CreateInstance<WeaponData>();
                AssetDatabase.CreateAsset(data, assetPath);
            }

            data.weaponType = def.type;
            data.animWeaponTypeId = def.animId;
            data.fireRate = def.fireRate;
            data.maxAmmo = def.maxAmmo;
            bool isMelee = def.type == "Sword";
            data.isMelee = isMelee;
            data.bulletPrefab = isMelee ? null : bulletPrefab;

            string prefabFolder = isMelee ? MeleePrefabFolder : BlastPrefabFolder;
            string blastPath = $"{prefabFolder}/{def.blastPrefabName}.prefab";
            GameObject blastPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(blastPath);
            if (blastPrefab != null)
            {
                data.weaponModelPrefab = blastPrefab;
            }
            else
            {
                Debug.LogWarning($"[WeaponDatabaseSetup] Blast prefab not found: {blastPath}");
            }

            string floatingPath = $"{FloatingPrefabFolder}/FloatingItem_{def.type}.prefab";
            GameObject floatingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(floatingPath);
            if (floatingPrefab != null)
            {
                data.floatingItemPrefab = floatingPrefab;
            }

            EditorUtility.SetDirty(data);
            weaponsList.Add(data);
        }

        string dbPath = $"{DataFolder}/WeaponDatabase.asset";
        WeaponDatabase existingDb = AssetDatabase.LoadAssetAtPath<WeaponDatabase>(dbPath);
        if (existingDb != null)
        {
            database = existingDb;
        }
        else
        {
            AssetDatabase.CreateAsset(database, dbPath);
        }

        var weaponsField = typeof(WeaponDatabase).GetField("weapons",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (weaponsField != null)
        {
            weaponsField.SetValue(database, weaponsList);
        }

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[WeaponDatabaseSetup] Created {weaponsList.Count} weapon data assets and WeaponDatabase at {dbPath}");
        Debug.Log("[WeaponDatabaseSetup] Remember to assign WeaponDatabase to NetworkManager.weaponDatabase in the Inspector!");
        Debug.Log("[WeaponDatabaseSetup] For weapon types without FloatingItem prefabs, duplicate an existing one and rename it.");

        Selection.activeObject = database;
    }

    [MenuItem("VectoArena/Create Missing FloatingItem Prefabs")]
    public static void CreateMissingFloatingItemPrefabs()
    {
        if (!AssetDatabase.IsValidFolder(FloatingPrefabFolder))
        {
            AssetDatabase.CreateFolder("Assets/Prefabs", "Floating");
        }

        string templatePath = $"{FloatingPrefabFolder}/FloatingItem_Rifle.prefab";
        GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(templatePath);

        if (template == null)
        {
            Debug.LogError($"[WeaponDatabaseSetup] Template prefab not found: {templatePath}");
            return;
        }

        int created = 0;
        int updated = 0;
        foreach (WeaponDef def in Weapons)
        {
            string targetPath = $"{FloatingPrefabFolder}/FloatingItem_{def.type}.prefab";
            bool alreadyExists = AssetDatabase.LoadAssetAtPath<GameObject>(targetPath) != null;

            GameObject instance;
            if (alreadyExists)
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(targetPath));
            }
            else
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(template);
                instance.name = $"FloatingItem_{def.type}";
            }

            string prefabFolder = def.type == "Sword" ? MeleePrefabFolder : BlastPrefabFolder;
            string blastPath = $"{prefabFolder}/{def.blastPrefabName}.prefab";
            GameObject blastPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(blastPath);

            Transform itemHolder = FindChildByName(instance.transform, "Item");
            if (itemHolder != null && blastPrefab != null)
            {
                // Destroy all existing visual model children under Item
                for (int i = itemHolder.childCount - 1; i >= 0; i--)
                {
                    Object.DestroyImmediate(itemHolder.GetChild(i).gameObject);
                }

                // Instantiate the correct Blast Royale weapon prefab as the visual
                GameObject weaponVisual = (GameObject)PrefabUtility.InstantiatePrefab(blastPrefab, itemHolder);
                weaponVisual.transform.localPosition = Vector3.zero;
                weaponVisual.transform.localRotation = Quaternion.identity;
                weaponVisual.transform.localScale = Vector3.one;

                // Strip missing scripts from Blast Royale prefabs (their scripts may not be available in this project)
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(weaponVisual);
                foreach (Transform child in weaponVisual.GetComponentsInChildren<Transform>(true))
                {
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
                }
            }
            else if (blastPrefab == null)
            {
                Debug.LogWarning($"[WeaponDatabaseSetup] Blast prefab not found: {blastPath}");
            }
            else
            {
                Debug.LogWarning($"[WeaponDatabaseSetup] 'Item' child not found in template for {def.type}");
            }

            WeaponPickup pickup = instance.GetComponent<WeaponPickup>();
            if (pickup != null)
            {
                if (blastPrefab != null)
                {
                    pickup.weaponModelPrefab = blastPrefab;
                }
                pickup.fireRate = def.fireRate;
                pickup.maxAmmo = def.maxAmmo;
            }

            PrefabUtility.SaveAsPrefabAsset(instance, targetPath);
            Object.DestroyImmediate(instance);

            if (alreadyExists)
            {
                updated++;
                Debug.Log($"[WeaponDatabaseSetup] Updated FloatingItem prefab: {targetPath}");
            }
            else
            {
                created++;
                Debug.Log($"[WeaponDatabaseSetup] Created FloatingItem prefab: {targetPath}");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[WeaponDatabaseSetup] Created {created}, updated {updated} FloatingItem prefabs. Run 'Setup Weapon Database' again to link them.");
    }

    private static Transform FindChildByName(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
            {
                return child;
            }
        }

        foreach (Transform child in parent)
        {
            Transform found = FindChildByName(child, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
