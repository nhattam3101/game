using System;
using System.Collections.Generic;
using UnityEngine;

public enum MS3CRarity { Common, Rare, Epic, Legendary }

[Serializable]
public sealed class MS3CRarityEntry
{
    public MS3AItemData item;
    public MS3CRarity rarity;
}

// Rarity belongs to an ITEM ASSET, not a random copy of that item.
// This preserves the original 3A stacking rules (which group by item asset).
[CreateAssetMenu(fileName = "RarityCatalog", menuName = "MonsterSurvival/Inventory 3C/Rarity Catalog")]
public sealed class MS3CRarityCatalog : ScriptableObject
{
    public List<MS3CRarityEntry> items = new List<MS3CRarityEntry>();

    public MS3CRarity GetRarity(MS3AItemData item)
    {
        if (item == null || items == null) return MS3CRarity.Common;
        foreach (MS3CRarityEntry row in items)
            if (row != null && row.item == item) return row.rarity;
        return MS3CRarity.Common;
    }

    public static Color ColorOf(MS3CRarity rarity)
    {
        switch (rarity)
        {
            case MS3CRarity.Rare: return new Color(.30f, .79f, 1f, 1f);
            case MS3CRarity.Epic: return new Color(.76f, .40f, 1f, 1f);
            case MS3CRarity.Legendary: return new Color(1f, .76f, .30f, 1f);
            default: return new Color(.80f, .83f, .92f, 1f);
        }
    }
}
