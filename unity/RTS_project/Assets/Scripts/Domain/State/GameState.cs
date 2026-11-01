using System;

public sealed class GameState
{
    public PlayerRegistry Players { get; }
    public WorldState World { get; }
    public MatchState Match { get; }

    public GameState()
    {
        Players = new PlayerRegistry();
        World = new WorldState();
        Match = new MatchState();
    }

    public void RegisterPlayer(PlayerState player)
    {
        Players.Register(player);
    }

    public void RegisterBase(BaseData baseData)
    {
        if (baseData == null)
            throw new ArgumentNullException(nameof(baseData));

        BaseOwner owner = baseData.Owner;

        if (owner.Type == BaseOwnerType.Player &&
            !Players.ContainsPlayer(owner.PlayerId))
        {
            throw new InvalidOperationException(
                $"A bázis olyan játékoshoz tartozik, aki nincs regisztrálva: {owner.PlayerId}");
        }

        World.RegisterBase(baseData);
    }

    public void RegisterUnit(UnitState unit)
    {
        if (unit == null)
            throw new ArgumentNullException(nameof(unit));

        if (!Players.ContainsPlayer(unit.OwnerPlayerId))
        {
            throw new InvalidOperationException(
                $"Az egység tulajdonosa nincs regisztrálva: {unit.OwnerPlayerId}");
        }

        World.RegisterUnit(unit);
    }

    public bool UnregisterUnit(int unitId)
    {
        return World.UnregisterUnit(unitId);
    }
}