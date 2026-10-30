using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GameObjectRegistrar : MonoBehaviour
{
    public static GameObjectRegistrar Instance { get; private set; }

    private GameBootstrapper _bootstrapper;
    private bool _initialized;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError(
                "A jelenetben több GameObjectRegistrar található.",
                this);

            return;
        }

        Instance = this;
    }

    public void Initialize(GameBootstrapper bootstrapper)
    {
        if (_initialized)
            throw new InvalidOperationException(
                "A GameObjectRegistrar már inicializálva lett.");

        if (bootstrapper == null)
            throw new ArgumentNullException(nameof(bootstrapper));

        _bootstrapper = bootstrapper;

        // A jelenetben már elhelyezett, aktív egységek megkeresése.
        UnitGroupMember[] sceneUnits =
            FindObjectsByType<UnitGroupMember>(
                FindObjectsSortMode.None);

        foreach (UnitGroupMember unit in sceneUnits)
        {
            if (unit == null)
                continue;

            RegisterUnit(unit);
        }

        _initialized = true;

        Debug.Log(
            $"GameObjectRegistrar inicializálva. " +
            $"Megtalált egységek: {sceneUnits.Length}");
    }

    public void RegisterUnit(UnitGroupMember unit)
    {
        if (unit == null)
            throw new ArgumentNullException(nameof(unit));

        if (_bootstrapper == null)
            throw new InvalidOperationException(
                "A GameObjectRegistrar még nincs inicializálva.");

        // Már regisztrált egységet nem regisztrálunk újra.
        if (unit.IsInitialized)
            return;

        _bootstrapper.RegisterUnit(unit);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}