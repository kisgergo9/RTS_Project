using System;
using Unity.Netcode;

/// <summary>
/// A lobby állapotának pillanatképe, amelyet a host minden változáskor
/// elküld a klienseknek, és amelyből a lobby felülete (a hoston is)
/// megjelenik. A klienseken csak megjelenítésre szolgál: a lobbyt
/// egyedül a host módosíthatja.
///
/// A játékoshelyek nevei a játékosazonosító sorrendjében szerepelnek
/// (index = azonosító - 1); az üres név szabad helyet jelent.
/// </summary>
public sealed class LobbySnapshot : INetworkSerializable
{
    /// <summary>
    /// A beolvasott helyszám felső korlátja, hogy egy hibás üzenet ne
    /// okozhasson túlzott memóriafoglalást.
    /// </summary>
    public const int MaxSlots = 64;

    public int LocalPlayerId;
    public int MinPlayerCount;
    public int MaxPlayerCount;
    public int WorldSize;
    public int WorldSizeMin;
    public int WorldSizeMax;
    public int WorldSizeStep;
    public int YellowBaseCount;
    public int YellowBaseMin;
    public int YellowBaseMax;
    public int ObstacleCount;
    public string[] SlotNames = Array.Empty<string>();

    public int PlayerCount => SlotNames.Length;

    public int OccupiedSlotCount
    {
        get
        {
            int count = 0;

            foreach (string name in SlotNames)
            {
                if (!string.IsNullOrEmpty(name))
                    count++;
            }

            return count;
        }
    }

    public bool CanStart => PlayerCount > 0 && OccupiedSlotCount == PlayerCount;

    public static LobbySnapshot FromState(LobbyState state, int localPlayerId)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));

        IntRange sizeRange = state.WorldSizeRange;
        IntRange yellowRange = state.YellowBaseCountRange;
        string[] names = new string[state.PlayerCount];

        for (int i = 0; i < names.Length; i++)
            names[i] = state.Slots[i].PlayerName ?? string.Empty;

        return new LobbySnapshot
        {
            LocalPlayerId = localPlayerId,
            MinPlayerCount = state.MinSelectablePlayerCount,
            MaxPlayerCount = state.MaxPlayerCount,
            WorldSize = state.WorldSize,
            WorldSizeMin = sizeRange.Min,
            WorldSizeMax = sizeRange.Max,
            WorldSizeStep = state.WorldSizeStep,
            YellowBaseCount = state.YellowBaseCount,
            YellowBaseMin = yellowRange.Min,
            YellowBaseMax = yellowRange.Max,
            ObstacleCount = state.ObstacleCount,
            SlotNames = names
        };
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref LocalPlayerId);
        serializer.SerializeValue(ref MinPlayerCount);
        serializer.SerializeValue(ref MaxPlayerCount);
        serializer.SerializeValue(ref WorldSize);
        serializer.SerializeValue(ref WorldSizeMin);
        serializer.SerializeValue(ref WorldSizeMax);
        serializer.SerializeValue(ref WorldSizeStep);
        serializer.SerializeValue(ref YellowBaseCount);
        serializer.SerializeValue(ref YellowBaseMin);
        serializer.SerializeValue(ref YellowBaseMax);
        serializer.SerializeValue(ref ObstacleCount);

        int slotCount = SlotNames.Length;
        serializer.SerializeValue(ref slotCount);

        if (serializer.IsReader)
        {
            if (slotCount < 0 || slotCount > MaxSlots)
                throw new InvalidOperationException($"Érvénytelen helyszám a lobbyüzenetben: {slotCount}");

            SlotNames = new string[slotCount];
        }

        for (int i = 0; i < slotCount; i++)
        {
            string name = SlotNames[i] ?? string.Empty;
            serializer.SerializeValue(ref name);
            SlotNames[i] = name;
        }
    }
}
