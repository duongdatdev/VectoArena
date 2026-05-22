using UnityEngine;

public enum WeaponType
{
    None = 0,
    Melee = 1,
    Gun = 2,
    XLGun = 3,
    SniperGun = 4,
    Launcher = 5,
    XLMelee = 6,
    KnifeMelee = 7,
    Handgun = 8,
    Shotgun = 9
}

public class WeaponSkinMonoComponent : MonoBehaviour
{
    public WeaponType WeaponType = WeaponType.Melee;
}
