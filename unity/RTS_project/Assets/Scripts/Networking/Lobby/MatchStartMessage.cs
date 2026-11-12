using System;
using Unity.Netcode;

// A host üzenete a mérkőzés indításakor: játékosok és a generált világ.
public struct MatchStartMessage : INetworkSerializable
{
    // A beolvasott játékosszám felső korlátja (hibás üzenet ellen).
    public const int MaxPlayers = LobbySnapshot.MaxSlots;

    public int LocalPlayerId;
    public int[] PlayerIds;
    public string[] PlayerNames;
    public WorldGenerationMessage World;

    public static MatchStartMessage FromSetup(MatchSetup setup, int localPlayerId)
    {
        if (setup == null)
            throw new ArgumentNullException(nameof(setup));

        int count = setup.Players.Count;
        MatchStartMessage message = new MatchStartMessage
        {
            LocalPlayerId = localPlayerId,
            PlayerIds = new int[count],
            PlayerNames = new string[count],
            World = new WorldGenerationMessage(setup.World)
        };

        for (int i = 0; i < count; i++)
        {
            message.PlayerIds[i] = setup.Players[i].PlayerId;
            message.PlayerNames[i] = setup.Players[i].Name;
        }

        return message;
    }

    public MatchSetup ToSetup()
    {
        MatchPlayer[] players = new MatchPlayer[PlayerIds.Length];

        for (int i = 0; i < players.Length; i++)
            players[i] = new MatchPlayer(PlayerIds[i], PlayerNames[i]);

        return new MatchSetup(players, LocalPlayerId, World.State);
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref LocalPlayerId);

        int count = PlayerIds?.Length ?? 0;
        serializer.SerializeValue(ref count);

        if (serializer.IsReader)
        {
            if (count < 1 || count > MaxPlayers)
                throw new InvalidOperationException($"Érvénytelen játékosszám az indítóüzenetben: {count}");

            PlayerIds = new int[count];
            PlayerNames = new string[count];
        }

        for (int i = 0; i < count; i++)
        {
            string name = PlayerNames[i] ?? string.Empty;
            serializer.SerializeValue(ref PlayerIds[i]);
            serializer.SerializeValue(ref name);
            PlayerNames[i] = name;
        }

        serializer.SerializeValue(ref World);
    }
}
