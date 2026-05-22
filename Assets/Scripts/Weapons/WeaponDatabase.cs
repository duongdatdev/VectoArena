using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WeaponDatabase", menuName = "VectoArena/Weapon Database")]
public class WeaponDatabase : ScriptableObject
{
    [SerializeField] private List<WeaponData> weapons = new List<WeaponData>();

    private Dictionary<string, WeaponData> _lookup;

    private void BuildLookup()
    {
        _lookup = new Dictionary<string, WeaponData>();
        foreach (WeaponData weapon in weapons)
        {
            if (weapon == null || string.IsNullOrEmpty(weapon.weaponType))
            {
                continue;
            }

            if (_lookup.ContainsKey(weapon.weaponType))
            {
                Debug.LogWarning($"[WeaponDatabase] Duplicate weapon type '{weapon.weaponType}' — skipping.");
                continue;
            }

            _lookup[weapon.weaponType] = weapon;
        }
    }

    public WeaponData GetWeaponData(string weaponType)
    {
        if (string.IsNullOrEmpty(weaponType))
        {
            return null;
        }

        if (_lookup == null)
        {
            BuildLookup();
        }

        _lookup.TryGetValue(weaponType, out WeaponData data);
        return data;
    }

    public GameObject GetFloatingItemPrefab(string weaponType)
    {
        WeaponData data = GetWeaponData(weaponType);
        return data != null ? data.floatingItemPrefab : null;
    }

    public GameObject GetWeaponModelPrefab(string weaponType)
    {
        WeaponData data = GetWeaponData(weaponType);
        return data != null ? data.weaponModelPrefab : null;
    }

    public int GetAnimWeaponTypeId(string weaponType)
    {
        WeaponData data = GetWeaponData(weaponType);
        return data != null ? data.animWeaponTypeId : 0;
    }

    public IReadOnlyList<WeaponData> AllWeapons => weapons;

    private void OnEnable()
    {
        _lookup = null;
    }
}
