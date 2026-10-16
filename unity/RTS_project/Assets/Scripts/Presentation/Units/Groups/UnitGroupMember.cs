using UnityEngine;

public sealed class UnitGroupMember : MonoBehaviour
{
    [SerializeField]
    private UnitType unitType = UnitType.Ground;

    public UnitType Type => unitType;

    public UnitGroup Group { get; private set; }

    public int FormationSlot { get; private set; }

    private void Start()
    {
        Register();
    }

    private void Register()
    {
        if (Group != null)
            return;

        if (UnitGroupManager.Instance == null)
        {
            Debug.LogError(
                $"Nem található UnitGroupManager: {gameObject.name}",
                this);
            return;
        }

        UnitGroupManager.Instance.CreateSingletonGroup(this);
    }

    public void SetGroup(UnitGroup group, int formationSlot)
    {
        Group = group;
        FormationSlot = group != null ? formationSlot : 0;
    }

    public void SetGroup(UnitGroup group)
    {
        SetGroup(group, 0);
    }

    private void OnDestroy()
    {
        if (UnitGroupManager.Instance != null)
            UnitGroupManager.Instance.UnregisterMember(this);
    }
}