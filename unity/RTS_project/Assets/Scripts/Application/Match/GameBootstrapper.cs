using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class PlayerSetup
{
    public int playerId;
    public string playerName;
}

public sealed class GameBootstrapper : MonoBehaviour
{
    [Header("Players")]

    [SerializeField]
    private PlayerSetup[] players;

    [Header("Bases")]

    [SerializeField]
    private BaseController[] bases;

    [Header("Units")]

    [SerializeField]
    private UnitGroupMember[] units;

    [Header("World Generation (optional)")]

    [Tooltip("Ha meg van adva, a bázisok a generált világból származnak, " +
             "és a Bases tömbnek üresnek kell lennie.")]
    [SerializeField]
    private WorldGenerationSetup worldGeneration;

    [SerializeField]
    private WorldSceneBuilder worldBuilder;

    public GameState State { get; private set; }

    // Generált világ esetén a generálás eredménye, egyébként null.
    public WorldLayout WorldLayout { get; private set; }

    // A beállított játékosok azonosítói (szerkesztõben is olvasható).
    public IEnumerable<int> ConfiguredPlayerIds
    {
        get
        {
            if (players == null)
                yield break;

            foreach (PlayerSetup setup in players)
            {
                if (setup != null)
                    yield return setup.playerId;
            }
        }
    }

    private UnitIdAllocator _unitIdAllocator;

    private bool _initialized;

    private void Awake()
    {
        Initialize();
    }

    public void Initialize()
    {
        if (_initialized)
        {
            Debug.LogError(
                "A GameBootstrapper már inicializálva lett.",
                this);
            return;
        }

        State = new GameState();

        _unitIdAllocator = new UnitIdAllocator();

        InitializePlayers();
        InitializeBases();
        InitializeUnits();

        _initialized = true;
    }

    private void InitializePlayers()
    {
        if (players == null)
            throw new InvalidOperationException(
                "A PlayerSetup tömb nincs beállítva.");

        foreach (PlayerSetup setup in players)
        {
            if (setup == null)
                throw new InvalidOperationException(
                    "A PlayerSetup tömb null elemet tartalmaz.");

            PlayerState player = new PlayerState(
                setup.playerId,
                setup.playerName);

            State.RegisterPlayer(player);
        }
    }

    private void InitializeBases()
    {
        if (worldGeneration != null)
        {
            InitializeGeneratedWorld();
            return;
        }

        if (bases == null)
            return;

        foreach (BaseController baseController in bases)
        {
            if (baseController == null)
                throw new InvalidOperationException(
                    "A BaseController tömb null elemet tartalmaz.");

            baseController.InitializeFromSceneConfiguration(
                State.Players);

            State.RegisterBase(baseController.Data);
        }
    }

    private void InitializeGeneratedWorld()
    {
        if (worldBuilder == null)
            throw new InvalidOperationException(
                "Generált világhoz WorldSceneBuilder szükséges.");

        if (bases != null && bases.Length > 0)
            throw new InvalidOperationException(
                "Generált világ esetén a Bases tömbnek üresnek kell lennie.");

        WorldGenerationResult result =
            worldGeneration.Generate(State.Players.Players);

        if (!result.Success)
            throw new InvalidOperationException(
                $"A világgenerálás sikertelen ({result.Error}): {result.ErrorMessage}");

        WorldLayout = result.Layout;
        State.World.SetGeneration(WorldLayoutSnapshot.ToState(result.Layout));

        foreach (BaseController baseController in
                 worldBuilder.Build(result.Layout, State.Players, worldGeneration.Settings))
        {
            State.RegisterBase(baseController.Data);
        }

        Debug.Log(
            $"Világ generálva: méret={result.Layout.WorldSize}, " +
            $"seed={result.Layout.Seed}, bázisok={result.Layout.Bases.Count}, " +
            $"akadályok={result.Layout.Obstacles.Count}, dekorációk={result.Layout.Decorations.Count}, " +
            $"kísérletek={result.Attempts}");
    }

    private void InitializeUnits()
    {
        if (units == null)
            return;

        foreach (UnitGroupMember unit in units)
        {
            if (unit == null)
                throw new InvalidOperationException(
                    "A UnitGroupMember tömb null elemet tartalmaz.");

            int unitId = _unitIdAllocator.Allocate();

            UnitState unitState = new UnitState(
                unitId,
                unit.OwnerPlayerId,
                unit.Type);

            // Domainállapot regisztrációja.
            State.RegisterUnit(unitState);

            // Unity komponens inicializálása.
            unit.Initialize(unitId);
        }
    }
}