using System;

public interface IResourceWallet
{
    int GetAmount(ResourceType resource);
    bool TrySpend(ResourceType resource, int amount);
    void Add(ResourceType resource, int amount);
}

public class SimpleWallet : IResourceWallet
{
    private int[] amounts = new int[Enum.GetValues(typeof(ResourceType)).Length];

    public int GetAmount(ResourceType resource)
    {
        return amounts[(int)resource];
    }

    public bool TrySpend(ResourceType resource, int amount)
    {
        if (amounts[(int)resource] < amount)
        {
            return false;
        }

        amounts[(int)resource] -= amount;
        return true;
    }

    public void Add(ResourceType resource, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        amounts[(int)resource] += amount;
    }
}
