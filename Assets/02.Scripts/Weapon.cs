using UnityEngine;

[CreateAssetMenu(menuName = "Moko/Weapon")]
public class Weapon : ScriptableObject
{
    public string DisplayName;
    public WeaponType Type = WeaponType.Melee;
    public MoveSet MoveSet;
}
