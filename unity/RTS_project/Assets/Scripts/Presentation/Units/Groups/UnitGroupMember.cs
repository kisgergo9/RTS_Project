using UnityEngine;

public sealed class UnitGroupMember : MonoBehaviour
{
    [Header("Unit Configuration")]

    [SerializeField]
    private UnitType unitType = UnitType.Ground;

    [SerializeField]
    private int ownerPlayerId;

    [Tooltip("Játékos vagy a semleges sárga frakció egysége.")]
    [SerializeField]
    private BaseOwnerType ownerType = BaseOwnerType.Player;

    public UnitType Type => unitType;
    public int OwnerPlayerId => ownerPlayerId;
    public BaseOwnerType OwnerType => ownerType;

    public int UnitId { get; private set; }

    public bool IsInitialized { get; private set; }

    public UnitGroup Group { get; private set; }

    public int FormationSlot { get; private set; }

    private void Start()
    {
        Register();
    }

    /// <summary>
    /// A tulajdonos beállítása futás közben létrehozott egységnél (pl. sárga
    /// védõegység). Csak az Initialize elõtt hívható.
    /// </summary>
    public void SetOwner(BaseOwnerType type, int playerId)
    {
        if (IsInitialized)
        {
            Debug.LogError(
                $"Inicializált egység tulajdonosa nem módosítható: {gameObject.name}",
                this);
            return;
        }

        ownerType = type;
        ownerPlayerId = type == BaseOwnerType.Player ? playerId : 0;
    }

    public void Initialize(int unitId)
    {
        if (IsInitialized)
        {
            Debug.LogError(
                $"Az egység már inicializálva van: {gameObject.name}",
                this);
            return;
        }

        if (unitId <= 0)
        {
            Debug.LogError(
                $"Érvénytelen UnitId: {unitId}",
                this);
            return;
        }

        UnitId = unitId;
        IsInitialized = true;
    }

    private void Register()
    {
        if (Group != null)
            return;

        if (!IsInitialized)
        {
            Debug.LogError(
                $"Az egység még nincs inicializálva: {gameObject.name}",
                this);
            return;
        }

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