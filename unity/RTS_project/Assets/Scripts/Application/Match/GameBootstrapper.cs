using System;
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

    public GameState State { get; private set; }

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