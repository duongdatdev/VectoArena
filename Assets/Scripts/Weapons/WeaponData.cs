using UnityEngine;

[CreateAssetMenu(fileName = "NewWeaponData", menuName = "VectoArena/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Must match the server weapon type string exactly (e.g. Rifle, Shotgun, Pistol)")]
    public string weaponType;

    [Tooltip("Blast Royale animator weapon_type_float value for blend tree selection")]
    public int animWeaponTypeId;

    [Header("Visuals")]
    [Tooltip("Weapon model prefab attached to the player on equip (Blast Royale weapon prefab)")]
    public GameObject weaponModelPrefab;

    [Tooltip("Ground pickup (floating item) prefab spawned in the world")]
    public GameObject floatingItemPrefab;

    [Header("Combat")]
    [Tooltip("Projectile prefab this weapon fires (null = melee)")]
    public GameObject bulletPrefab;

    [Tooltip("Seconds between shots")]
    public float fireRate = 0.5f;

    [Tooltip("Maximum ammo (-1 = unlimited)")]
    public int maxAmmo = 30;

    [Header("Classification")]
    public bool isMelee = false;

    public bool IsRanged => !isMelee && bulletPrefab != null;
}
