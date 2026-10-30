using UnityEngine;

[CreateAssetMenu(
    fileName = "NewFightDefinition",
    menuName = "Game/Configuration/Fight Definition")]
public sealed class FightDefinition : ScriptableObject
{
    [Header("Combat Specification")]
    [SerializeField]
    private SpecificationType specificationType = SpecificationType.Normal;

    [Header("Durability")]
    [Min(1)]
    [SerializeField]
    private int hp = 100;

    [Min(1)]
    [SerializeField]
    private int armor = 1;

    [Header("Damage")]
    [Min(0)]
    [SerializeField]
    private int normalDamage;

    [Min(0)]
    [SerializeField]
    private int specializedDamage;

    public SpecificationType SpecificationType => specificationType;

    public int HP => hp;
    public int Armor => armor;

    public int NormalDamage => normalDamage;
    public int SpecializedDamage => specializedDamage;

    public bool CanAttack =>
        specificationType != SpecificationType.None;

#if UNITY_EDITOR
    private void OnValidate()
    {
        hp = Mathf.Max(1, hp);
        armor = Mathf.Max(1, armor);

        normalDamage = Mathf.Max(0, normalDamage);
        specializedDamage = Mathf.Max(0, specializedDamage);

        if (specificationType == SpecificationType.None)
        {
            normalDamage = 0;
            specializedDamage = 0;
        }

        if (specificationType == SpecificationType.Normal)
        {
            specializedDamage = 0;
        }
    }
#endif
}