using System;

/// <summary>
/// A generált világ menthető, hálózaton küldhető pillanatképe.
/// Tartalmazza a generálási beállításokat és a seedet (reprodukálhatóság),
/// valamint a bázisok tényleges elhelyezését is, így a mentés visszatöltése
/// és a kliensek világa nem függ attól, hogy a generátor később változik-e.
///
/// Szándékosan egyszerű, publikus mezős adatosztály (JsonUtility és hálózati
/// szerializáció miatt), Unity-típusok nélkül. Létrehozása és visszaalakítása
/// a WorldLayoutSnapshot feladata.
/// </summary>
[Serializable]
public sealed class WorldGenerationState
{
    public int generatorVersion;
    public int seed;
    public int worldSize;
    public int yellowBaseCount;
    public int obstacleCount;
    public int[] playerIds = Array.Empty<int>();
    public BasePlacementState[] bases = Array.Empty<BasePlacementState>();
}

[Serializable]
public struct BasePlacementState
{
    public int baseId;
    public float x;
    public float y;
    public BaseOwnerType ownerType;
    public int playerId;
}
