using System;
using System.Collections.Generic;
using Unity.VisualScripting;

/*
 Fontos, hogy a játékos azonosítója és a hálózati kapcsolat azonosítója két külön dolog. 
A PlayerId a mérkõzés teljes idõtartama alatt stabil marad.
 */

public sealed class PlayerState : IResourceWallet
{
    private readonly int[] resourceAmounts;

    public int PlayerId { get; }
    public string PlayerName { get; private set; }
    public bool IsEliminated { get; private set; }

    public PlayerState(int playerId, string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName))
            throw new ArgumentException(
                "A játékos neve nem lehet üres.",
                nameof(playerName));

        PlayerId = playerId;
        PlayerName = playerName;

        resourceAmounts = new int[
            Enum.GetValues(typeof(ResourceType)).Length];
    }

    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException(
                "A játékos neve nem lehet üres.",
                nameof(newName));

        PlayerName = newName;
    }

    public void Eliminate()
    {
        IsEliminated = true;
    }

    public int GetAmount(ResourceType resource)
    {
        ValidateResource(resource);
        return resourceAmounts[(int)resource];
    }

    public bool TrySpend(ResourceType resource, int amount)
    {
        ValidateResource(resource);

        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        int index = (int)resource;

        if (resourceAmounts[index] < amount)
            return false;

        resourceAmounts[index] -= amount;
        return true;
    }

    public void Add(ResourceType resource, int amount)
    {
        ValidateResource(resource);

        if (amount <= 0)
            return;

        int index = (int)resource;
        resourceAmounts[index] = checked(
            resourceAmounts[index] + amount);
    }

    private static void ValidateResource(ResourceType resource)
    {
        if (!Enum.IsDefined(typeof(ResourceType), resource))
            throw new ArgumentOutOfRangeException(nameof(resource));
    }
}
