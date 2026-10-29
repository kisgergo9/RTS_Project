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
    LevelZero,
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
    private float productionProgress;

    private int upgradingIndex = -1;
    private float upgradeTimeLeft;

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

    public void Tick(float deltaTime, IResourceWallet wallet)
    {
        if (producingIndex >= 0)
        {
            float usedTime = Math.Min(deltaTime, productionTimeLeft);
            int ratePerSecond = settings.buildings[producingIndex].productionPerSecondByLevel[levels[producingIndex]];

            productionProgress = productionProgress + ratePerSecond * usedTime;
            int wholeUnits = (int)productionProgress;

            if (wholeUnits >= 1)
            {
                productionProgress = productionProgress - wholeUnits;
                wallet.Add((ResourceType)producingIndex, wholeUnits);
            }

            productionTimeLeft = productionTimeLeft - deltaTime;

            if (productionTimeLeft <= 0f)
            {
                producingIndex = -1;
                productionTimeLeft = 0f;
            }
        }

        if (upgradingIndex >= 0)
        {
            upgradeTimeLeft = upgradeTimeLeft - deltaTime;

            if (upgradeTimeLeft <= 0f)
            {
                levels[upgradingIndex] = levels[upgradingIndex] + 1;
                upgradingIndex = -1;
                upgradeTimeLeft = 0f;
            }
        }
    }

    public ActionInfo StartAction(int buildingIndex, BuildingAction action, IResourceWallet wallet)
    {
        switch (action)
        {
            case BuildingAction.Produce:
                if (producingIndex >= 0)
                {
                    return ActionInfo.ActionRunning;
                }

                if (levels[buildingIndex] <= 0)
                {
                    return ActionInfo.LevelZero;
                }

                producingIndex = buildingIndex;
                productionTimeLeft = settings.buildings[buildingIndex].productionDurationSeconds;
                return ActionInfo.Started;

            case BuildingAction.Upgrade:
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

                int cost = building.upgradeCostConcreteByLevel[targetLevel];

                if (!wallet.TrySpend(ResourceType.Concrete, cost))
                {
                    return ActionInfo.NotEnoughConcrete;
                }

                upgradingIndex = buildingIndex;
                upgradeTimeLeft = building.upgradeTimeByLevel[targetLevel];
                return ActionInfo.Started;

            default:
                throw new ArgumentOutOfRangeException(nameof(action));
        }
    }
}
