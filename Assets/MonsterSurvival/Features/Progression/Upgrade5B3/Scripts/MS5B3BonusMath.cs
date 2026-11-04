using System;

// 5B.3: Pure, deterministic math. No disk reads, no PlayerPrefs, no scene changes.
[Serializable]
public struct MS5B3BonusValues
{
    public int vitalityLevel, strengthLevel, furyLevel, agilityLevel;
    public float maxHealth, damage, attackSpeed, moveSpeed;
}

public static class MS5B3BonusMath
{
    public static bool TryCalculate(int vitality, int strength, int fury, int agility,
                                    out MS5B3BonusValues value, out string error)
    {
        value = default(MS5B3BonusValues);
        error = null;
        if (vitality < 0 || vitality > 10 || strength < 0 || strength > 10 ||
            fury < 0 || fury > 10 || agility < 0 || agility > 10)
        {
            error = "Upgrade level must be between 0 and 10.";
            return false;
        }
        value.vitalityLevel = vitality;
        value.strengthLevel = strength;
        value.furyLevel = fury;
        value.agilityLevel = agility;
        value.maxHealth = vitality * 10f;
        value.damage = strength * 0.05f;
        value.attackSpeed = fury * 0.03f;
        value.moveSpeed = agility * 0.02f;
        return true;
    }

    public static bool TryFromSnapshot(MS5B1PermanentUpgrades.Snapshot snapshot,
                                       out MS5B3BonusValues value, out string error)
    {
        value = default(MS5B3BonusValues);
        if (snapshot == null)
        {
            error = "Save profile snapshot is null.";
            return false;
        }
        return TryCalculate(snapshot.LevelOf(MS5B1UpgradeDefinitions.Vitality),
                            snapshot.LevelOf(MS5B1UpgradeDefinitions.Strength),
                            snapshot.LevelOf(MS5B1UpgradeDefinitions.Fury),
                            snapshot.LevelOf(MS5B1UpgradeDefinitions.Agility),
                            out value, out error);
    }
}
