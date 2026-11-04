using System;
using System.Collections.Generic;

// 5B.1: catalog of permanent upgrade definitions. NO PlayerStats/Health mutations here.
public sealed class MS5B1UpgradeDefinition
{
    public readonly string id;
    public readonly string title;
    public readonly string bonusDescription;
    public readonly int baseCost;
    public readonly int maxLevel;

    public MS5B1UpgradeDefinition(string id, string title, string bonusDescription, int baseCost, int maxLevel)
    {
        this.id = id;
        this.title = title;
        this.bonusDescription = bonusDescription;
        this.baseCost = baseCost;
        this.maxLevel = maxLevel;
    }

    public int PriceForNextLevel(int currentLevel)
    {
        if (currentLevel < 0 || currentLevel >= maxLevel) return -1;
        long cost = (long)baseCost * (currentLevel + 1);
        return cost > int.MaxValue ? -1 : (int)cost;
    }
}

public static class MS5B1UpgradeDefinitions
{
    public const string Vitality = "vitality";
    public const string Strength = "strength";
    public const string Fury = "fury";
    public const string Agility = "agility";

    private static readonly MS5B1UpgradeDefinition[] Catalog = {
        new MS5B1UpgradeDefinition(Vitality, "VITALITY", "+10 Max HP / level", 50, 10),
        new MS5B1UpgradeDefinition(Strength, "STRENGTH", "+5% Damage / level", 60, 10),
        new MS5B1UpgradeDefinition(Fury, "FURY", "+3% Attack Speed / level", 70, 10),
        new MS5B1UpgradeDefinition(Agility, "AGILITY", "+2% Move Speed / level", 45, 10)
    };

    public static MS5B1UpgradeDefinition[] GetAll()
    {
        return (MS5B1UpgradeDefinition[])Catalog.Clone();
    }

    public static bool TryGet(string id, out MS5B1UpgradeDefinition result)
    {
        result = null;
        if (string.IsNullOrEmpty(id)) return false;
        foreach (MS5B1UpgradeDefinition entry in Catalog)
        {
            if (!string.Equals(entry.id, id, StringComparison.Ordinal)) continue;
            result = entry;
            return true;
        }
        return false;
    }
}
