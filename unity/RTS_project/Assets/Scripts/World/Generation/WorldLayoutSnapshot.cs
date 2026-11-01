using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Átalakítás a WorldLayout és a menthető / hálózaton küldhető
/// WorldGenerationState között, valamint JSON-szerializáció a mentéshez.
///
/// Szinkronizáció: a host a teljes layoutot küldi el a klienseknek (nem csak a
/// seedet), így a kliensek világa a generátor verziójától és a lebegőpontos
/// számítások gépenkénti eltérésétől is független.
/// </summary>
public static class WorldLayoutSnapshot
{
    public static WorldGenerationState ToState(WorldLayout layout)
    {
        if (layout == null)
            throw new ArgumentNullException(nameof(layout));

        WorldGenerationRequest request = layout.Request;

        int[] playerIds = new int[request.PlayerIds.Count];

        for (int i = 0; i < playerIds.Length; i++)
            playerIds[i] = request.PlayerIds[i];

        BasePlacementState[] bases = new BasePlacementState[layout.Bases.Count];

        for (int i = 0; i < bases.Length; i++)
        {
            BaseLayout baseLayout = layout.Bases[i];

            bases[i] = new BasePlacementState
            {
                baseId = baseLayout.BaseId,
                x = baseLayout.Position.x,
                y = baseLayout.Position.y,
                ownerType = baseLayout.OwnerType,
                playerId = baseLayout.PlayerId
            };
        }

        return new WorldGenerationState
        {
            generatorVersion = layout.GeneratorVersion,
            seed = request.Seed,
            worldSize = request.WorldSize,
            yellowBaseCount = request.YellowBaseCount,
            obstacleCount = request.ObstacleCount,
            playerIds = playerIds,
            bases = bases
        };
    }

    /// <summary>
    /// Visszaállítja a layoutot a pillanatképből. Nem generál újra: a mentett
    /// vagy a hosttól kapott bázispozíciókat használja. Érvénytelen adat
    /// esetén kivételt dob, mert az sérült mentést vagy hibás üzenetet jelent.
    /// </summary>
    public static WorldLayout FromState(WorldGenerationState state)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));

        if (state.playerIds == null || state.bases == null)
            throw new ArgumentException("A világ pillanatképe hiányos.", nameof(state));

        WorldGenerationRequest request = new WorldGenerationRequest(
            state.playerIds,
            state.worldSize,
            state.yellowBaseCount,
            state.obstacleCount,
            state.seed);

        WorldBounds bounds = new WorldBounds(state.worldSize);
        HashSet<int> baseIds = new HashSet<int>();
        HashSet<int> players = new HashSet<int>(state.playerIds);
        List<BaseLayout> bases = new List<BaseLayout>(state.bases.Length);

        foreach (BasePlacementState placement in state.bases)
        {
            Vector2 position = new Vector2(placement.x, placement.y);

            if (!baseIds.Add(placement.baseId))
                throw new ArgumentException(
                    $"Duplikált bázisazonosító a pillanatképben: {placement.baseId}", nameof(state));

            if (!bounds.Contains(position))
                throw new ArgumentException(
                    $"A(z) {placement.baseId}. bázis a pályán kívül van: {position}", nameof(state));

            if (!Enum.IsDefined(typeof(BaseOwnerType), placement.ownerType))
                throw new ArgumentException(
                    $"Ismeretlen tulajdonostípus: {placement.ownerType}", nameof(state));

            if (placement.ownerType == BaseOwnerType.Player && !players.Contains(placement.playerId))
                throw new ArgumentException(
                    $"A(z) {placement.baseId}. bázis tulajdonosa nem résztvevő játékos: " +
                    $"{placement.playerId}", nameof(state));

            bases.Add(new BaseLayout(placement.baseId, position, placement.ownerType, placement.playerId));
        }

        return new WorldLayout(state.generatorVersion, request, bases);
    }

    public static string ToJson(WorldGenerationState state, bool prettyPrint = false)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));

        return JsonUtility.ToJson(state, prettyPrint);
    }

    public static WorldGenerationState FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("Üres JSON.", nameof(json));

        WorldGenerationState state = JsonUtility.FromJson<WorldGenerationState>(json);

        if (state == null)
            throw new ArgumentException("A JSON nem értelmezhető világállapotként.", nameof(json));

        return state;
    }
}
