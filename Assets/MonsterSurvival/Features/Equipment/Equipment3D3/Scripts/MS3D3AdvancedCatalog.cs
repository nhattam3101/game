using System;
using System.Collections.Generic;
using UnityEngine;

// Separate affix layer. Existing EquipmentCatalog owns slots/quality; 3D2 owns Damage/HP/etc.
[Serializable]
public struct MS3D3AdvancedBonus
{
    [Min(0f)] public float flatBulletDamage;
    [Min(0f)] public float attackRangeFlat;
    [Min(0f)] public float bulletSpeedPercent;

    public static MS3D3AdvancedBonus operator +(MS3D3AdvancedBonus a, MS3D3AdvancedBonus b)
    {
        return new MS3D3AdvancedBonus {
            flatBulletDamage = a.flatBulletDamage + b.flatBulletDamage,
            attackRangeFlat = a.attackRangeFlat + b.attackRangeFlat,
            bulletSpeedPercent = a.bulletSpeedPercent + b.bulletSpeedPercent
        };
    }

    public static MS3D3AdvancedBonus operator -(MS3D3AdvancedBonus a, MS3D3AdvancedBonus b)
    {
        return new MS3D3AdvancedBonus {
            flatBulletDamage = a.flatBulletDamage - b.flatBulletDamage,
            attackRangeFlat = a.attackRangeFlat - b.attackRangeFlat,
            bulletSpeedPercent = a.bulletSpeedPercent - b.bulletSpeedPercent
        };
    }

    public bool HasValue
    {
        get { return flatBulletDamage > .001f || attackRangeFlat > .001f || bulletSpeedPercent > .001f; }
    }

    public string CompactText()
    {
        var parts = new List<string>();
        if (flatBulletDamage > .001f) parts.Add("BULLET +" + flatBulletDamage.ToString("0.#"));
        if (attackRangeFlat > .001f) parts.Add("RANGE +" + attackRangeFlat.ToString("0.#"));
        if (bulletSpeedPercent > .001f) parts.Add("SHOT SPD +" + bulletSpeedPercent.ToString("0.#") + "%");
        return parts.Count > 0 ? string.Join("  /  ", parts) : "No advanced affixes";
    }
}

[Serializable]
public sealed class MS3D3AdvancedEntry
{
    public MS3AItemData item;
    public MS3D3AdvancedBonus bonus;
}

[CreateAssetMenu(fileName = "AdvancedAffixes", menuName = "MonsterSurvival/Equipment 3D3/Advanced Affix Catalog")]
public sealed class MS3D3AdvancedCatalog : ScriptableObject
{
    public List<MS3D3AdvancedEntry> entries = new List<MS3D3AdvancedEntry>();

    public MS3D3AdvancedBonus Get(MS3AItemData item)
    {
        if (!item || entries == null) return default(MS3D3AdvancedBonus);
        foreach (var e in entries)
            if (e != null && e.item == item) return e.bonus;
        return default(MS3D3AdvancedBonus);
    }

    public bool Contains(MS3AItemData item)
    {
        if (!item || entries == null) return false;
        foreach (var e in entries) if (e != null && e.item == item) return true;
        return false;
    }
}
