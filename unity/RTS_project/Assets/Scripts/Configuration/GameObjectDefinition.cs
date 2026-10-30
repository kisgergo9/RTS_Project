using UnityEngine;

[CreateAssetMenu(
    fileName = "NewGameObjectDefinition",
    menuName = "Game/Configuration/Game Object Definition")]
public sealed class GameObjectDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField]
    private string id;

    [SerializeField]
    private string displayName;

    [Header("Object Type")]
    [SerializeField]
    private UnitType unitType = UnitType.Ground;

    [Header("Configurations")]
    [SerializeField]
    private FightDefinition fightDefinition;

    [SerializeField]
    private MovementDefinition movementDefinition;

    public string Id => id;
    public string DisplayName => displayName;
    public UnitType UnitType => unitType;

    public FightDefinition Fight => fightDefinition;
    public MovementDefinition Movement => movementDefinition;

    public bool HasFightDefinition => fightDefinition != null;
    public bool HasMovementDefinition => movementDefinition != null;

#if UNITY_EDITOR
    private void OnValidate()
    {
        id = id?.Trim();
        displayName = displayName?.Trim();
    }
#endif
}