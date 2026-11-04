using System;
using System.Collections.Generic;

// 5C.1: Fixed content identifiers persisted in MS5A1SaveData.unlockedContentIds.
// Existing maps are NOT gated by this stage. 5C.2 will add UI and an explicit migration policy.
public enum MS5C1UnlockCategory { Map, Equipment, Achievement }

public sealed class MS5C1UnlockDefinition
{
    public readonly string id;
    public readonly string title;
    public readonly MS5C1UnlockCategory category;
    public readonly int price;
    public readonly bool starterUnlocked;
    public readonly bool achievementOnly;

    public MS5C1UnlockDefinition(string id, string title, MS5C1UnlockCategory category,
                                 int price, bool starterUnlocked = false, bool achievementOnly = false)
    {
        this.id = id; this.title = title; this.category = category;
        this.price = price; this.starterUnlocked = starterUnlocked;
        this.achievementOnly = achievementOnly;
    }
}

public static class MS5C1UnlockCatalog
{
    private static readonly MS5C1UnlockDefinition[] Items = {
        new MS5C1UnlockDefinition("map.green_meadow", "Green Meadow", MS5C1UnlockCategory.Map, 0, true),
        new MS5C1UnlockDefinition("map.sand_dunes", "Sand Dunes", MS5C1UnlockCategory.Map, 250),
        new MS5C1UnlockDefinition("map.ancient_ruins", "Ancient Ruins", MS5C1UnlockCategory.Map, 650),
        new MS5C1UnlockDefinition("equipment.reaper_edge", "Reaper Edge", MS5C1UnlockCategory.Equipment, 800),
        new MS5C1UnlockDefinition("equipment.wardens_oath", "Warden's Oath", MS5C1UnlockCategory.Equipment, 1100),
        new MS5C1UnlockDefinition("achievement.shadow_titan", "Shadow Titan Defeated", MS5C1UnlockCategory.Achievement, 0, false, true),
        new MS5C1UnlockDefinition("achievement.grave_warden", "Grave Warden Defeated", MS5C1UnlockCategory.Achievement, 0, false, true)
    };

    public static MS5C1UnlockDefinition[] GetAll() { return (MS5C1UnlockDefinition[])Items.Clone(); }
    public static bool TryGet(string id, out MS5C1UnlockDefinition definition)
    {
        foreach (MS5C1UnlockDefinition item in Items)
        {
            if (string.Equals(item.id, id, StringComparison.Ordinal)) { definition = item; return true; }
        }
        definition = null;
        return false;
    }

    public static bool IsStarter(string id)
    {
        MS5C1UnlockDefinition def;
        return TryGet(id, out def) && def.starterUnlocked;
    }
}
