using System;
using System.Collections.Generic;

/// <summary>
/// A lobby állapota a hoston: a játékoshelyek és a generálási beállítások.
/// Unity-független, így tesztelhető; a lobby felülete és (később) a hálózati
/// réteg is ezt használja.
///
/// A beállítások mindig a WorldSizingRules érvényes tartományán belül
/// maradnak. Játékosszám-változáskor a világméret, világméret-változáskor a
/// sárga bázisok és az akadályok száma az ajánlott értékre áll.
///
/// A játékoshelyek azonosítója 1-től a játékosszámig tart; az 1-es hely a
/// hosté. A mérkőzés csak akkor indulhat, ha minden hely foglalt.
/// </summary>
public sealed class LobbyState
{
    public const int HostPlayerId = 1;

    /// <summary>
    /// Többjátékos mérkőzéshez legalább ennyi játékos kell; az egyjátékos
    /// teszt külön, a lobbyn kívül indul.
    /// </summary>
    public const int MinLobbyPlayerCount = 2;

    private readonly WorldSizingRules _rules;
    private readonly List<LobbySlot> _slots = new List<LobbySlot>();

    public event Action Changed;

    public LobbyState(WorldSizingRules rules, string hostName)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));

        if (string.IsNullOrWhiteSpace(hostName))
            throw new ArgumentException("A host nevét meg kell adni.", nameof(hostName));

        MinPlayerCount = Math.Max(MinLobbyPlayerCount, rules.MinPlayerCount);

        if (MinPlayerCount > rules.MaxPlayerCount)
            throw new ArgumentException(
                $"A beállítások legfeljebb {rules.MaxPlayerCount} játékost engednek, " +
                $"a lobbyhoz legalább {MinLobbyPlayerCount} kell.");

        ResizeSlots(MinPlayerCount);
        _slots[0].Occupy(hostName);
        ApplyRecommendedWorldSize();
    }

    public int MinPlayerCount { get; }
    public int MaxPlayerCount => _rules.MaxPlayerCount;
    public int PlayerCount => _slots.Count;

    public int WorldSize { get; private set; }
    public int YellowBaseCount { get; private set; }
    public int ObstacleCount { get; private set; }

    public int WorldSizeStep => _rules.WorldSizeStep;
    public IntRange WorldSizeRange => _rules.GetWorldSizeRange(PlayerCount);
    public IntRange YellowBaseCountRange => _rules.GetYellowBaseCountRange(PlayerCount, WorldSize);

    public IReadOnlyList<LobbySlot> Slots => _slots;

    /// <summary>
    /// A játékosszám legkisebb értéke, amelyre csökkenteni lehet: a
    /// legutolsó foglalt hely nem szűnhet meg.
    /// </summary>
    public int MinSelectablePlayerCount
    {
        get
        {
            int lastOccupied = 0;

            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].IsOccupied)
                    lastOccupied = i + 1;
            }

            return Math.Max(MinPlayerCount, lastOccupied);
        }
    }

    public int OccupiedSlotCount
    {
        get
        {
            int count = 0;

            foreach (LobbySlot slot in _slots)
            {
                if (slot.IsOccupied)
                    count++;
            }

            return count;
        }
    }

    public bool CanStart => OccupiedSlotCount == PlayerCount;

    public void SetPlayerCount(int playerCount)
    {
        int clamped = Clamp(playerCount, MinSelectablePlayerCount, MaxPlayerCount);

        if (clamped == PlayerCount)
            return;

        ResizeSlots(clamped);
        ApplyRecommendedWorldSize();
    }

    /// <summary>
    /// A világméret a lépésközre kerekítve, az érvényes tartományba szorítva
    /// áll be; a sárga bázisok és az akadályok száma az ajánlottra áll.
    /// </summary>
    public void SetWorldSize(int worldSize)
    {
        IntRange range = WorldSizeRange;
        int step = WorldSizeStep;
        int rounded = (int)Math.Round(worldSize / (double)step, MidpointRounding.AwayFromZero) * step;
        int clamped = Clamp(rounded, range.Min, range.Max);

        if (clamped == WorldSize)
            return;

        ApplyWorldSize(clamped);
    }

    public void SetYellowBaseCount(int yellowBaseCount)
    {
        IntRange range = YellowBaseCountRange;
        int clamped = Clamp(yellowBaseCount, range.Min, range.Max);

        if (clamped == YellowBaseCount)
            return;

        YellowBaseCount = clamped;
        RaiseChanged();
    }

    public void ApplyRecommendedSettings()
    {
        ApplyRecommendedWorldSize();
    }

    /// <summary>
    /// Az első szabad helyet foglalja le. Ha nincs szabad hely, false.
    /// </summary>
    public bool TryOccupyFreeSlot(string playerName, out int playerId)
    {
        if (string.IsNullOrWhiteSpace(playerName))
            throw new ArgumentException("A játékos nevét meg kell adni.", nameof(playerName));

        foreach (LobbySlot slot in _slots)
        {
            if (slot.IsOccupied)
                continue;

            slot.Occupy(playerName);
            playerId = slot.PlayerId;
            RaiseChanged();
            return true;
        }

        playerId = 0;
        return false;
    }

    /// <summary>
    /// Felszabadítja a játékos helyét. A host helye nem szabadítható fel.
    /// </summary>
    public void ReleaseSlot(int playerId)
    {
        if (playerId == HostPlayerId)
            throw new InvalidOperationException("A host helye nem szabadítható fel.");

        if (playerId < 1 || playerId > PlayerCount || !_slots[playerId - 1].IsOccupied)
            return;

        _slots[playerId - 1].Release();
        RaiseChanged();
    }

    public WorldGenerationRequest CreateRequest(int seed)
    {
        if (!CanStart)
            throw new InvalidOperationException("Nincs minden játékoshely betöltve.");

        int[] playerIds = new int[PlayerCount];

        for (int i = 0; i < playerIds.Length; i++)
            playerIds[i] = _slots[i].PlayerId;

        return new WorldGenerationRequest(playerIds, WorldSize, YellowBaseCount, ObstacleCount, seed);
    }

    private void ResizeSlots(int playerCount)
    {
        while (_slots.Count > playerCount)
            _slots.RemoveAt(_slots.Count - 1);

        while (_slots.Count < playerCount)
            _slots.Add(new LobbySlot(_slots.Count + 1));
    }

    private void ApplyRecommendedWorldSize()
    {
        ApplyWorldSize(_rules.GetRecommendedWorldSize(PlayerCount));
    }

    private void ApplyWorldSize(int worldSize)
    {
        WorldSize = worldSize;
        YellowBaseCount = _rules.GetRecommendedYellowBaseCount(PlayerCount, worldSize);
        ObstacleCount = _rules.GetRecommendedObstacleCount(worldSize);
        RaiseChanged();
    }

    private void RaiseChanged()
    {
        Changed?.Invoke();
    }

    private static int Clamp(int value, int min, int max)
    {
        return Math.Max(min, Math.Min(value, max));
    }
}

/// <summary>
/// Egy játékoshely a lobbyban. A PlayerId egyben a mérkőzésbeli
/// játékosazonosító.
/// </summary>
public sealed class LobbySlot
{
    public LobbySlot(int playerId)
    {
        PlayerId = playerId;
    }

    public int PlayerId { get; }
    public string PlayerName { get; private set; }
    public bool IsOccupied => PlayerName != null;

    internal void Occupy(string playerName)
    {
        PlayerName = playerName;
    }

    internal void Release()
    {
        PlayerName = null;
    }
}
