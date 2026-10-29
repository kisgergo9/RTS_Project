using System;

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
    public string name; // ez csak kiírja az inspectorba a beállításoknál hogy épp melyik épületet állítjuk, csak nekünk lényeges

    public int productionDurationSeconds = 60;

    public int[] productionPerSecondByLevel = new int[10];
    public int[] upgradeCostConcreteByLevel = new int[10];
    public int[] upgradeTimeByLevel = new int[10];
}

[Serializable]
public class TurretSettings
{
    public float maxHealth = 100;
    public float damage = 10;
    public float fireCooldown = 1;
    public int buildCostSteel = 50;
}
