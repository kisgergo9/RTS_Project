using System;

public sealed class UnitState
{
    public int UnitId { get; }

    /// <summary>
    /// Az egység tulajdonosának típusa: játékos vagy a semleges sárga frakció.
    /// A bázisokkal azonos BaseOwnerType felsorolást használja; egység nem
    /// lehet gazdátlan (None).
    /// </summary>
    public BaseOwnerType OwnerType { get; }

    /// <summary>
    /// A tulajdonos játékos azonosítója. Csak Player tulajdonos esetén
    /// értelmezett, sárga egységnél 0.
    /// </summary>
    public int OwnerPlayerId { get; }

    public UnitType Type { get; }

    public bool IsYellow => OwnerType == BaseOwnerType.Yellow;

    // Játékos tulajdonában lévõ egység.
    public UnitState(int unitId, int ownerPlayerId, UnitType type)
        : this(unitId, BaseOwnerType.Player, ownerPlayerId, type)
    {
    }

    public UnitState(int unitId, BaseOwnerType ownerType, int ownerPlayerId, UnitType type)
    {
        if (unitId <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(unitId),
                "Az egységazonosítónak pozitívnak kell lennie.");

        if (ownerType != BaseOwnerType.Player && ownerType != BaseOwnerType.Yellow)
            throw new ArgumentOutOfRangeException(
                nameof(ownerType),
                "Az egység tulajdonosa csak játékos vagy a sárga frakció lehet.");

        if (ownerType == BaseOwnerType.Player && ownerPlayerId < 0)
            throw new ArgumentOutOfRangeException(
                nameof(ownerPlayerId),
                "A játékosazonosító nem lehet negatív.");

        if (!Enum.IsDefined(typeof(UnitType), type))
            throw new ArgumentOutOfRangeException(nameof(type));

        UnitId = unitId;
        OwnerType = ownerType;
        OwnerPlayerId = ownerType == BaseOwnerType.Player ? ownerPlayerId : 0;
        Type = type;
    }
}