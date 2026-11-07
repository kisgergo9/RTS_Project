using System;
using System.Collections.Generic;

/// <summary>
/// A lobbyból indított mérkőzés adatai: a játékosok, a helyi játékos és a
/// host által generált világ pillanatképe.
///
/// A hoston a lobby, a klienseken a hosttól kapott indítóüzenet állítja be
/// (Current) a mérkőzés jelenetének betöltése előtt; a jelenetben a
/// MatchStarter olvassa ki. A világot a kliensek nem generálják újra, hanem
/// a host pillanatképét építik fel, így mindenki ugyanazt a pályát látja.
/// </summary>
public sealed class MatchSetup
{
    private readonly MatchPlayer[] _players;

    public MatchSetup(IEnumerable<MatchPlayer> players, int localPlayerId, WorldGenerationState world)
    {
        if (players == null)
            throw new ArgumentNullException(nameof(players));

        World = world ?? throw new ArgumentNullException(nameof(world));
        _players = new List<MatchPlayer>(players).ToArray();

        if (_players.Length == 0)
            throw new ArgumentException("A mérkőzéshez legalább egy játékos kell.", nameof(players));

        HashSet<int> ids = new HashSet<int>();
        bool hasLocal = false;

        foreach (MatchPlayer player in _players)
        {
            if (!ids.Add(player.PlayerId))
                throw new ArgumentException($"Többször szereplő játékosazonosító: {player.PlayerId}", nameof(players));

            if (player.PlayerId == localPlayerId)
                hasLocal = true;
        }

        if (!hasLocal)
            throw new ArgumentException($"A helyi játékos ({localPlayerId}) nincs a játékosok között.",
                nameof(localPlayerId));

        if (world.playerIds == null || world.playerIds.Length != _players.Length)
            throw new ArgumentException("A világ játékosai nem egyeznek a mérkőzés játékosaival.", nameof(world));

        foreach (int worldPlayerId in world.playerIds)
        {
            if (!ids.Contains(worldPlayerId))
                throw new ArgumentException(
                    $"A világ ismeretlen játékost tartalmaz: {worldPlayerId}", nameof(world));
        }

        LocalPlayerId = localPlayerId;
    }

    /// <summary>
    /// A betöltés alatt álló mérkőzés adatai (null, ha nincs ilyen).
    /// </summary>
    public static MatchSetup Current { get; set; }

    public IReadOnlyList<MatchPlayer> Players => _players;
    public int LocalPlayerId { get; }
    public WorldGenerationState World { get; }

    /// <summary>
    /// A lobby játékoshelyeiből és a generált világból készít mérkőzésadatot.
    /// </summary>
    public static MatchSetup FromLobby(LobbyState lobby, WorldLayout layout, int localPlayerId)
    {
        if (lobby == null)
            throw new ArgumentNullException(nameof(lobby));

        if (layout == null)
            throw new ArgumentNullException(nameof(layout));

        List<MatchPlayer> players = new List<MatchPlayer>(lobby.PlayerCount);

        foreach (LobbySlot slot in lobby.Slots)
        {
            if (!slot.IsOccupied)
                throw new InvalidOperationException("Nincs minden játékoshely betöltve.");

            players.Add(new MatchPlayer(slot.PlayerId, slot.PlayerName));
        }

        return new MatchSetup(players, localPlayerId, WorldLayoutSnapshot.ToState(layout));
    }
}

/// <summary>
/// Egy mérkőzés résztvevője: azonosító és megjelenített név.
/// </summary>
public readonly struct MatchPlayer
{
    public MatchPlayer(int playerId, string name)
    {
        PlayerId = playerId;
        Name = string.IsNullOrWhiteSpace(name) ? LobbyState.GetDefaultPlayerName(playerId) : name;
    }

    public int PlayerId { get; }
    public string Name { get; }
}
