using System;
using System.Collections.Generic;

public sealed class PlayerRegistry
{
    private readonly Dictionary<int, PlayerState> players = new();

    public int Count => players.Count;

    public IReadOnlyCollection<PlayerState> Players =>
        players.Values;

    public void Register(PlayerState player)
    {
        if (player == null)
            throw new ArgumentNullException(nameof(player));

        if (players.ContainsKey(player.PlayerId))
        {
            throw new InvalidOperationException(
                $"Már létezik játékos ezzel az azonosítóval: {player.PlayerId}");
        }

        players.Add(player.PlayerId, player);
    }

    public bool TryGetPlayer(int playerId, out PlayerState player)
    {
        return players.TryGetValue(playerId, out player);
    }

    public PlayerState GetPlayer(int playerId)
    {
        if (!players.TryGetValue(playerId, out PlayerState player))
        {
            throw new KeyNotFoundException(
                $"Nem található játékos ezzel az azonosítóval: {playerId}");
        }

        return player;
    }

    public bool ContainsPlayer(int playerId)
    {
        return players.ContainsKey(playerId);
    }
}