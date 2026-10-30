using System;

public sealed class UnitState
{
    public int UnitId { get; }
    public int OwnerPlayerId { get; }
    public UnitType Type { get; }

    public UnitState(int unitId, int ownerPlayerId, UnitType type)
    {
        if (unitId <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(unitId),
                "Az egységazonosítónak pozitívnak kell lennie.");

        if (ownerPlayerId < 0)
            throw new ArgumentOutOfRangeException(
                nameof(ownerPlayerId),
                "A játékosazonosító nem lehet negatív.");

        if (!Enum.IsDefined(typeof(UnitType), type))
            throw new ArgumentOutOfRangeException(nameof(type));

        UnitId = unitId;
        OwnerPlayerId = ownerPlayerId;
        Type = type;
    }
}