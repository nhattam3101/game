using System;
using System.Collections.Generic;
using UnityEngine;

public enum MS3DEquipmentType { Weapon = 0, Armor = 1, Accessory = 2 }
public enum MS3DQuality { Common, Rare, Epic, Legendary }

[Serializable]
public sealed class MS3DEquipmentDefinition
{
    public MS3AItemData item;
    public MS3DEquipmentType type;
    public MS3DQuality quality;
    [TextArea(1, 3)] public string flavorText;
}

// Does not modify MS3AItemData or MS3CRarityCatalog.
// Only items registered here can be equipped.
[CreateAssetMenu(fileName = "EquipmentCatalog", menuName = "MonsterSurvival/Inventory 3D/Equipment Catalog")]
public sealed class MS3DEquipmentCatalog : ScriptableObject
{
    public List<MS3DEquipmentDefinition> entries = new List<MS3DEquipmentDefinition>();

    public bool TryGet(MS3AItemData item, out MS3DEquipmentDefinition definition)
    {
        definition = null;
        if (item == null || item.itemKind != MS3AItemKind.Material || entries == null) return false;
        foreach (var row in entries)
            if (row != null && row.item == item)
            {
                definition = row;
                return true;
            }
        return false;
    }

    public static Color QualityColor(MS3DQuality quality)
    {
        switch (quality)
        {
            case MS3DQuality.Rare: return new Color(.31f, .79f, 1f);
            case MS3DQuality.Epic: return new Color(.76f, .41f, 1f);
            case MS3DQuality.Legendary: return new Color(1f, .78f, .32f);
            default: return new Color(.82f, .83f, .92f);
        }
    }
}
