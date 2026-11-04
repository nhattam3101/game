using System;
using System.Collections.Generic;
using UnityEngine;

// Pure snapshot validation/capture/resolve, with no game-object mutation.
public static class MS5A3SnapshotCodec
{
    public const int Capacity = 20;

    public static bool TryCapture(MS3APlayerInventory inventory, MS3DPlayerEquipment gear,
                                  MS5A3ItemRegistry registry, MS5A1SaveData profile, out string error)
    {
        if (!inventory || !gear || !registry || profile == null)
        { error = "Missing Inventory, Equipment, Registry or profile"; return false; }
        Dictionary<string, MS3AItemData> lookup;
        if (!registry.TryBuild(out lookup, out error)) return false;
        if (inventory.Slots == null || inventory.Slots.Count != Capacity)
        { error = "Inventory does not have 20 slots"; return false; }
        List<MS5A3SavedStack> slots = new List<MS5A3SavedStack>();
        for (int i = 0; i < Capacity; i++)
        {
            MS3AInventorySlot s = inventory.Slots[i];
            if (s == null) { error = "Null slot " + i; return false; }
            MS5A3SavedStack next = new MS5A3SavedStack();
            if (!s.IsEmpty)
            {
                if (!s.item || s.quantity <= 0 || s.quantity > s.item.StackLimit ||
                    !lookup.ContainsKey(s.item.itemId) || lookup[s.item.itemId] != s.item)
                { error = "Invalid/unknown item or stack at slot " + i; return false; }
                next.itemId = s.item.itemId;
                next.quantity = s.quantity;
            }
            slots.Add(next);
        }
        List<MS5A3SavedEquipped> equipped = new List<MS5A3SavedEquipped>();
        if (!gear.catalog) { error = "Equipment catalog missing"; return false; }
        for (int i = 0; i < 3; i++)
        {
            var item = gear.GetEquipped((MS3DEquipmentType)i);
            var entry = new MS5A3SavedEquipped { type = i, itemId = "" };
            if (item)
            {
                MS3DEquipmentDefinition definition;
                if (!lookup.ContainsKey(item.itemId) || lookup[item.itemId] != item ||
                    !gear.catalog.TryGet(item, out definition) || definition == null ||
                    (int)definition.type != i)
                { error = "Invalid equipped item at slot " + i; return false; }
                entry.itemId = item.itemId;
            }
            equipped.Add(entry);
        }
        // Only assign after ALL checks pass; never write partial snapshots.
        profile.inventorySlots = slots;
        profile.equippedItems = equipped;
        profile.inventorySnapshotPresent = true;
        error = "Captured exact 20+3 slot snapshot";
        return true;
    }

    public static bool TryResolve(MS5A1SaveData data, MS5A3ItemRegistry registry,
                                  MS3DEquipmentCatalog catalog, out MS3AItemData[] items,
                                  out int[] counts, out MS3AItemData[] worn, out string error)
    {
        items = null; counts = null; worn = null;
        if (data == null || !data.inventorySnapshotPresent)
        { error = "No equipment snapshot in this profile"; return false; }
        Dictionary<string, MS3AItemData> lookup;
        if (!registry)
        {
            error = "Item Registry missing";
            return false;
        }

        if (!registry.TryBuild(out lookup, out error))
            return false;
        if (!catalog) { error = "Equipment catalog missing"; return false; }
        if (data.inventorySlots == null || data.inventorySlots.Count != Capacity ||
            data.equippedItems == null || data.equippedItems.Count != 3)
        { error = "Snapshot expects exactly 20 inventory slots and 3 equipment slots"; return false; }
        var newItems = new MS3AItemData[Capacity];
        var newQtys = new int[Capacity];
        var newWorn = new MS3AItemData[3];
        for (int i = 0; i < Capacity; i++)
        {
            var entry = data.inventorySlots[i];
            if (entry == null || entry.quantity < 0)
            { error = "Invalid inventory entry " + i; return false; }
            if (entry.quantity == 0)
            {
                if (!string.IsNullOrEmpty(entry.itemId))
                { error = "Empty slot has itemId at " + i; return false; }
                continue;
            }
            if (string.IsNullOrEmpty(entry.itemId) ||
                !lookup.TryGetValue(entry.itemId, out newItems[i]) ||
                entry.quantity > newItems[i].StackLimit)
            { error = "Unknown item / invalid stack at " + i + ": " + entry.itemId; return false; }
            newQtys[i] = entry.quantity;
        }
        for (int i = 0; i < 3; i++)
        {
            var entry = data.equippedItems[i];
            if (entry == null || entry.type != i)
            { error = "Invalid gear type/order " + i; return false; }
            if (string.IsNullOrEmpty(entry.itemId)) continue;
            MS3DEquipmentDefinition definition;
            if (!lookup.TryGetValue(entry.itemId, out newWorn[i]) ||
                !catalog.TryGet(newWorn[i], out definition) || definition == null ||
                (int)definition.type != i)
            { error = "Unknown/incompatible equipped gear at " + i; return false; }
        }
        items = newItems; counts = newQtys; worn = newWorn;
        error = "Validated 20 inventory + 3 equipment slots";
        return true;
    }

    public static bool IsEmptyPlayer(MS3APlayerInventory inventory, MS3DPlayerEquipment equipment)
    {
        if (!inventory || !equipment || inventory.Slots == null || inventory.Slots.Count != Capacity)
            return false;
        foreach (var s in inventory.Slots)
            if (s == null || !s.IsEmpty) return false;
        for (int i = 0; i < 3; i++)
            if (equipment.GetEquipped((MS3DEquipmentType)i)) return false;
        return true;
    }
}
