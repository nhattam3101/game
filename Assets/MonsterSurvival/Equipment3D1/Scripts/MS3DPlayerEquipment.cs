using System;
using UnityEngine;

// Stage 3D.1: equipment slots only. No stats changes until 3D.2.
// Uses EXISTING MS3AInventorySlot references; never increases 20-slot capacity.
[DisallowMultipleComponent]
[RequireComponent(typeof(MS3APlayerInventory))]
public sealed class MS3DPlayerEquipment : MonoBehaviour
{
    public MS3DEquipmentCatalog catalog;
    [SerializeField] private MS3AItemData weapon;
    [SerializeField] private MS3AItemData armor;
    [SerializeField] private MS3AItemData accessory;
    public event Action EquipmentChanged;

    private MS3APlayerInventory inventory;
    public MS3APlayerInventory Inventory
    {
        get
        {
            if (inventory == null) inventory = GetComponent<MS3APlayerInventory>();
            return inventory;
        }
    }

    public MS3AItemData GetEquipped(MS3DEquipmentType type)
    {
        switch (type)
        {
            case MS3DEquipmentType.Weapon: return weapon;
            case MS3DEquipmentType.Armor: return armor;
            default: return accessory;
        }
    }

    private void SetEquipped(MS3DEquipmentType type, MS3AItemData item)
    {
        switch (type)
        {
            case MS3DEquipmentType.Weapon: weapon = item; break;
            case MS3DEquipmentType.Armor: armor = item; break;
            default: accessory = item; break;
        }
    }

    private int FindRoom(MS3AItemData item, int excluding = -1)
    {
        if (item == null || Inventory == null) return -1;
        var slots = Inventory.Slots;
        for (int i = 0; i < slots.Count; i++)
        {
            if (i == excluding || slots[i] == null) continue;
            if (!slots[i].IsEmpty && slots[i].item == item && slots[i].quantity < item.StackLimit)
                return i;
        }
        for (int i = 0; i < slots.Count; i++)
            if (i != excluding && slots[i] != null && slots[i].IsEmpty) return i;
        return -1;
    }

    private void InsertOne(int index, MS3AItemData item)
    {
        MS3AInventorySlot slot = Inventory.Slots[index];
        if (slot.IsEmpty) { slot.item = item; slot.quantity = 1; }
        else slot.quantity++;
    }

    public bool TryEquip(int inventoryIndex, out string message)
    {
        message = "Cannot equip this item.";
        if (Inventory == null || catalog == null || inventoryIndex < 0 ||
            inventoryIndex >= Inventory.Slots.Count) return false;
        MS3AInventorySlot source = Inventory.Slots[inventoryIndex];
        if (source == null || source.IsEmpty) return false;
        MS3DEquipmentDefinition definition;
        if (!catalog.TryGet(source.item, out definition))
        {
            message = "This item is not equipment. Use the USE button for potions.";
            return false;
        }
        MS3AItemData incoming = source.item;
        MS3AItemData previous = GetEquipped(definition.type);
        if (previous == incoming) { message = "This item is already equipped."; return false; }

        // Atomic swap: if a stack remains, old equipment must have another slot.
        int target = -1;
        if (previous != null && source.quantity > 1)
        {
            target = FindRoom(previous, inventoryIndex);
            if (target < 0)
            {
                message = "Inventory is full. Free a slot before swapping gear.";
                return false;
            }
        }

        source.quantity--;
        if (source.quantity <= 0) { source.quantity = 0; source.item = null; }
        if (previous != null)
        {
            if (source.IsEmpty) { source.item = previous; source.quantity = 1; }
            else InsertOne(target, previous);
        }
        SetEquipped(definition.type, incoming);
        EquipmentChanged?.Invoke();
        message = "Equipped " + incoming.displayName + " (" + definition.type + ").";
        return true;
    }

    public bool TryUnequip(MS3DEquipmentType type, out string message)
    {
        message = "Nothing equipped in this slot.";
        if (Inventory == null) return false;
        MS3AItemData current = GetEquipped(type);
        if (current == null) return false;
        int target = FindRoom(current);
        if (target < 0) { message = "Inventory is full. Cannot unequip safely."; return false; }
        InsertOne(target, current);
        SetEquipped(type, null);
        EquipmentChanged?.Invoke();
        message = "Unequipped " + current.displayName + ".";
        return true;
    }

    // 5A.3 additive restore API. Gear comes from the SAME verified save
    // as the Inventory layout; do not insert it back into the bag.
    // Fires a single change event so 3D.2/3D.3 recalculate gear delta once.
    public bool TryRestoreSnapshot(MS3AItemData[] byType)
    {
        if (byType == null || byType.Length != 3 || catalog == null) return false;
        for (int i = 0; i < 3; i++)
        {
            MS3AItemData item = byType[i];
            if (item == null) continue;
            MS3DEquipmentDefinition definition;
            if (!catalog.TryGet(item, out definition) || definition == null ||
                (int)definition.type != i) return false;
        }
        weapon = byType[0];
        armor = byType[1];
        accessory = byType[2];
        EquipmentChanged?.Invoke();
        return true;
    }
}