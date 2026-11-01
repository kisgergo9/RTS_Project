using System;
using System.Collections.Generic;

/// <summary>
/// A host által (a lobbyban) kiválasztott generálási beállítások.
/// A seeddel együtt ez határozza meg egyértelműen a generált világot,
/// ezért a mentésnek is tartalmaznia kell.
/// </summary>
public sealed class WorldGenerationRequest
{
    private readonly int[] _playerIds;

    /// <summary>
    /// A résztvevő játékosok azonosítói. A kezdőbázisok növekvő
    /// azonosító szerinti sorrendben kerülnek a körre.
    /// </summary>
    public IReadOnlyList<int> PlayerIds => _playerIds;

    public int PlayerCount => _playerIds.Length;
    public int WorldSize { get; }
    public int YellowBaseCount { get; }

    /// <summary>
    /// A terepakadályok kért száma. A generátor jelenleg csak ellenőrzi,
    /// de még nem helyez el akadályokat.
    /// </summary>
    public int ObstacleCount { get; }

    public int Seed { get; }

    public WorldGenerationRequest(
        IEnumerable<int> playerIds,
        int worldSize,
        int yellowBaseCount,
        int obstacleCount,
        int seed)
    {
        if (playerIds == null)
            throw new ArgumentNullException(nameof(playerIds));

        _playerIds = new List<int>(playerIds).ToArray();

        HashSet<int> unique = new HashSet<int>();

        foreach (int playerId in _playerIds)
        {
            if (playerId < 0)
                throw new ArgumentOutOfRangeException(nameof(playerIds),
                    $"A játékosazonosító nem lehet negatív: {playerId}");

            if (!unique.Add(playerId))
                throw new ArgumentException(
                    $"Duplikált játékosazonosító: {playerId}", nameof(playerIds));
        }

        WorldSize = worldSize;
        YellowBaseCount = yellowBaseCount;
        ObstacleCount = obstacleCount;
        Seed = seed;
    }
}
