using System;
using System.Collections.Generic;
using UnityEngine;

// Bonuses are added to the EXISTING PlayerStats values, never substitute them.
// Percent values mean points of the existing multiplier: 20 => +0.20 damageMultiplier.
[Serializable]
public struct MS3D2Bonus
{
    [Min(0f)] public float damagePercent;
    [Min(0f)] public float attackSpeedPercent;
    [Min(0f)] public float moveSpeedPercent;
    [Min(0f)] public float critChancePoints; // 5 = +5 percentage points (0.05)
    [Min(0f)] public float maxHealthFlat;

    public static MS3D2Bonus operator +(MS3D2Bonus a, MS3D2Bonus b)
    {
        return new MS3D2Bonus {
            damagePercent = a.damagePercent + b.damagePercent,
            attackSpeedPercent = a.attackSpeedPercent + b.attackSpeedPercent,
            moveSpeedPercent = a.moveSpeedPercent + b.moveSpeedPercent,
            critChancePoints = a.critChancePoints + b.critChancePoints,
            maxHealthFlat = a.maxHealthFlat + b.maxHealthFlat
        };
    }
    public static MS3D2Bonus operator -(MS3D2Bonus a, MS3D2Bonus b)
    {
        return new MS3D2Bonus {
            damagePercent = a.damagePercent - b.damagePercent,
            attackSpeedPercent = a.attackSpeedPercent - b.attackSpeedPercent,
            moveSpeedPercent = a.moveSpeedPercent - b.moveSpeedPercent,
            critChancePoints = a.critChancePoints - b.critChancePoints,
            maxHealthFlat = a.maxHealthFlat - b.maxHealthFlat
        };
    }

    public string CompactText()
    {
        var parts = new List<string>(5);
        if (damagePercent > .001f) parts.Add("DMG +" + damagePercent.ToString("0.#") + "%");
        if (attackSpeedPercent > .001f) parts.Add("ATK SPD +" + attackSpeedPercent.ToString("0.#") + "%");
        if (moveSpeedPercent > .001f) parts.Add("MOVE +" + moveSpeedPercent.ToString("0.#") + "%");
        if (critChancePoints > .001f) parts.Add("CRIT +" + critChancePoints.ToString("0.#") + "pt");
        if (maxHealthFlat > .001f) parts.Add("HP +" + maxHealthFlat.ToString("0.#"));
        return parts.Count > 0 ? string.Join("  |  ", parts) : "NO BONUS";
    }
}

[Serializable]
public sealed class MS3D2BonusEntry
{
    public MS3AItemData item;
    public MS3D2Bonus bonuses;
}

[CreateAssetMenu(fileName = "EquipmentBonuses", menuName = "MonsterSurvival/Inventory 3D/Equipment Bonuses 3D2")]
public sealed class MS3D2BonusCatalog : ScriptableObject
{
    public List<MS3D2BonusEntry> entries = new List<MS3D2BonusEntry>();

    public MS3D2Bonus GetBonus(MS3AItemData item)
    {
        if (item == null || entries == null) return default(MS3D2Bonus);
        foreach (var row in entries)
            if (row != null && row.item == item) return row.bonuses;
        return default(MS3D2Bonus);
    }

    public bool HasEntry(MS3AItemData item)
    {
        if (item == null || entries == null) return false;
        foreach (var row in entries)
            if (row != null && row.item == item) return true;
        return false;
    }
}
