using System;
using UnityEngine;

[Serializable]
public class BaseSettings
{
    public int startingTurretCount = 2;
    public int levelsLostOnDestruction = 1;
    public int startingLevelPlayer = 1;
    public int startingLevelOther = 0;

    public TurretSettings turret = new TurretSettings();

    public BuildingSettings[] buildings = new BuildingSettings[4];
}

[Serializable]
public class BuildingSettings
{
    [HideInInspector] public string name;

    public const int LevelCount = 10;

    public int[] productionAmountByLevel = new int[LevelCount];
    public int[] productionTimeByLevel = new int[LevelCount];
    public int[] upgradeCostConcreteByLevel = new int[LevelCount];
    public int[] upgradeTimeByLevel = new int[LevelCount];
}

[Serializable]
public class TurretSettings
{
    public float maxHealth = 100;
    public float damage = 10;
    public float fireCooldown = 1;
    public int buildCostSteel = 50;
}
