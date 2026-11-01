using System;

public enum BaseOwnerType
{
    None,
    Player,
    Yellow
}

public class BaseOwner
{
    private BaseOwnerType type;
    private int playerId;

    public BaseOwnerType Type
    {
        get { return type; }
    }

    public int PlayerId
    {
        get { return playerId; }
    }

    public BaseOwner(BaseOwnerType type, int playerId)
    {
        this.type = type;
        this.playerId = playerId;
    }
}

public enum BuildingAction
{
    Produce,
    Upgrade
}

public enum ActionInfo
{
    Started,
    ActionRunning,
    MaxLevelReached,
    NotEnoughConcrete
}

public class BaseData
{
    private int baseId;
    private BaseOwner owner;
    private BaseSettings settings;

    private int[] levels;

    private int producingIndex = -1;
    private float productionTimeLeft;
    private int productionReward;
    private float productionTimeTotal;

    private int upgradingIndex = -1;
    private float upgradeTimeLeft;
    private float upgradeTimeTotal;

    public int BaseId
    {
        get { return baseId; }
    }

    public BaseOwner Owner
    {
        get { return owner; }
    }

    public int BuildingCount
    {
        get { return levels.Length; }
    }

    public int ProducingIndex
    {
        get { return producingIndex; }
    }

    public float ProductionTimeLeft
    {
        get { return productionTimeLeft; }
    }

    public int UpgradingIndex
    {
        get { return upgradingIndex; }
    }

    public float UpgradeTimeLeft
    {
        get { return upgradeTimeLeft; }
    }

    public float ProductionProgress
    {
        get { return GetProgress(productionTimeLeft, productionTimeTotal); }
    }

    public float UpgradeProgress
    {
        get { return GetProgress(upgradeTimeLeft, upgradeTimeTotal); }
    }

    public BaseData(int baseId, BaseOwner owner, BaseSettings settings)
    {
        this.baseId = baseId;
        this.owner = owner;
        this.settings = settings;

        int startLevel;

        switch (owner.Type)
        {
            case BaseOwnerType.Player:
                startLevel = settings.startingLevelPlayer;
                break;

            default:
                startLevel = settings.startingLevelOther;
                break;
        }

        levels = new int[settings.buildings.Length];

        for (int i = 0; i < levels.Length; i++)
        {
            levels[i] = startLevel;
        }
    }

    public int GetLevel(int buildingIndex)
    {
        return levels[buildingIndex];
    }

    public static ResourceType GetResource(int buildingIndex)
    {
        return (ResourceType)buildingIndex;
    }

    public void Tick(float deltaTime, IResourceWallet wallet)
    {
        TickProduction(deltaTime, wallet);
        TickUpgrade(deltaTime);
    }

    public ActionInfo StartAction(int buildingIndex, BuildingAction action, IResourceWallet wallet)
    {
        switch (action)
        {
            case BuildingAction.Produce:
                return StartProduction(buildingIndex);

            case BuildingAction.Upgrade:
                return StartUpgrade(buildingIndex, wallet);

            default:
                throw new ArgumentOutOfRangeException(nameof(action));
        }
    }

    // A fal leomlása: a szintek a felükre csökkennek (felfelé kerekítve), a bázis gazdátlan lesz, a futó műveletek megszakadnak.
    public void Collapse()
    {
        for (int i = 0; i < levels.Length; i++)
        {
            levels[i] = (levels[i] + 1) / 2;
        }

        owner = new BaseOwner(BaseOwnerType.None, 0);

        EndProduction();
        EndUpgrade();
    }

    private void TickProduction(float deltaTime, IResourceWallet wallet)
    {
        if (producingIndex < 0)
        {
            return;
        }

        productionTimeLeft -= deltaTime;

        if (productionTimeLeft <= 0f)
        {
            wallet.Add(GetResource(producingIndex), productionReward);
            EndProduction();
        }
    }

    private void TickUpgrade(float deltaTime)
    {
        if (upgradingIndex < 0)
        {
            return;
        }

        upgradeTimeLeft -= deltaTime;

        if (upgradeTimeLeft <= 0f)
        {
            levels[upgradingIndex] += 1;
            EndUpgrade();
        }
    }

    private ActionInfo StartProduction(int buildingIndex)
    {
        if (producingIndex >= 0)
        {
            return ActionInfo.ActionRunning;
        }

        BuildingSettings building = settings.buildings[buildingIndex];
        int level = levels[buildingIndex];

        producingIndex = buildingIndex;
        productionTimeLeft = building.productionTimeByLevel[level];
        productionTimeTotal = productionTimeLeft;
        productionReward = building.productionAmountByLevel[level];
        return ActionInfo.Started;
    }

    private ActionInfo StartUpgrade(int buildingIndex, IResourceWallet wallet)
    {
        if (upgradingIndex >= 0)
        {
            return ActionInfo.ActionRunning;
        }

        BuildingSettings building = settings.buildings[buildingIndex];
        int targetLevel = levels[buildingIndex] + 1;

        if (targetLevel >= building.upgradeCostConcreteByLevel.Length)
        {
            return ActionInfo.MaxLevelReached;
        }

        if (!wallet.TrySpend(ResourceType.Concrete, building.upgradeCostConcreteByLevel[targetLevel]))
        {
            return ActionInfo.NotEnoughConcrete;
        }

        upgradingIndex = buildingIndex;
        upgradeTimeLeft = building.upgradeTimeByLevel[targetLevel];
        upgradeTimeTotal = upgradeTimeLeft;
        return ActionInfo.Started;
    }

    private void EndProduction()
    {
        producingIndex = -1;
        productionTimeLeft = 0f;
    }

    private void EndUpgrade()
    {
        upgradingIndex = -1;
        upgradeTimeLeft = 0f;
    }

    private static float GetProgress(float timeLeft, float timeTotal)
    {
        if (timeTotal <= 0f)
        {
            return 1f;
        }

        return 1f - timeLeft / timeTotal;
    }
}
