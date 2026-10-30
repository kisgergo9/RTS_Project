using System;
using UnityEngine;

[Serializable]
public sealed class PlayerSetup
{
    public int playerId;
    public string playerName;
}

[RequireComponent(typeof(GameObjectRegistrar))]
public sealed class GameBootstrapper : MonoBehaviour
{
    [Header("Players")]
    [SerializeField]
    private PlayerSetup[] players;

    [Header("Bases")]
    [SerializeField]
    private BaseController[] bases;

    [Header("Game Data")]
    [SerializeField]
    private GameObjectDefinition[] gameObjectDefinitions;

    public GameDataRegistry GameData { get; private set; }

    public GameState State { get; private set; }

    private UnitIdAllocator _unitIdAllocator;
    private GameObjectRegistrar _registrar;

    private bool _initialized;

    private void Awake()
    {
        _registrar = GetComponent<GameObjectRegistrar>();

        Initialize();
    }

    private void InitializeGameData()
    {
        GameData = new GameDataRegistry(gameObjectDefinitions);

        Debug.Log(
            $"GameDataRegistry inicializálva. " +
            $"Objektumdefiníciók száma: {GameData.GameObjectCount}");
    }

    public void Initialize()
    {
        if (_initialized)
            return;

        InitializeGameData();

        State = new GameState();
        _unitIdAllocator = new UnitIdAllocator();

        InitializePlayers();
        InitializeBases();

        // A jelenetben elhelyezett egységek regisztrációja.
        _registrar.Initialize(this);

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

    /// <summary>
    /// Egység regisztrálása a központi játékállapotba.
    /// A jelenetben elhelyezett és a futás közben létrehozott
    /// egységek is ezt a metódust használják.
    /// </summary>
    internal void RegisterUnit(UnitGroupMember unit)
    {
        if (unit == null)
            throw new ArgumentNullException(nameof(unit));

        if (State == null || _unitIdAllocator == null)
            throw new InvalidOperationException(
                "A GameBootstrapper még nincs inicializálva.");

        if (unit.IsInitialized)
            return;

        int unitId = _unitIdAllocator.Allocate();

        UnitState unitState = new UnitState(
            unitId,
            unit.OwnerPlayerId,
            unit.Type);

        State.RegisterUnit(unitState);

        unit.Initialize(unitId);

        Debug.Log(
            $"Egység regisztrálva: ID={unitId}, " +
            $"Type={unit.Type}, Owner={unit.OwnerPlayerId}");
    }
}