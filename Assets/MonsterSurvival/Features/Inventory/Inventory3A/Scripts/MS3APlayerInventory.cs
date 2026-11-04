using System;
using System.Collections.Generic;
using UnityEngine;

// 3A: 20 slots per match. No save/load or automatic loot-table alterations.
[DisallowMultipleComponent]
public sealed class MS3APlayerInventory : MonoBehaviour
{
    public const int Capacity = 20;
    [SerializeField] private List<MS3AInventorySlot> slots = new List<MS3AInventorySlot>();
    public event Action InventoryChanged;
    private Health playerHealth;

    public IReadOnlyList<MS3AInventorySlot> Slots { get { return slots; } }

    private void Awake()
    {
        while (slots.Count < Capacity) slots.Add(new MS3AInventorySlot());
        if (slots.Count > Capacity) slots.RemoveRange(Capacity, slots.Count - Capacity);
        for (int i = 0; i < Capacity; i++)
            if (slots[i] == null) slots[i] = new MS3AInventorySlot();
        playerHealth = GetComponent<Health>();
    }

    // Returns ACCEPTED count (possibly less than requested when full).
    public int AddItem(MS3AItemData item, int count)
    {
        if (item == null || count <= 0) return 0;
        int remaining = count;
        int max = item.StackLimit;
        // Fill existing stacks first.
        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            var s = slots[i];
            if (s.item != item || s.IsEmpty || s.quantity >= max) continue;
            int amount = Mathf.Min(max - s.quantity, remaining);
            s.quantity += amount;
            remaining -= amount;
        }
        // Fill free slots, do not overwrite another item.
        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            var s = slots[i];
            if (!s.IsEmpty) continue;
            int amount = Mathf.Min(max, remaining);
            s.item = item;
            s.quantity = amount;
            remaining -= amount;
        }
        int added = count - remaining;
        if (added > 0) InventoryChanged?.Invoke();
        return added;
    }

    public bool UseSlot(int index)
    {
        if (index < 0 || index >= slots.Count) return false;
        var s = slots[index];
        if (s.IsEmpty || s.item.itemKind != MS3AItemKind.HealConsumable) return false;
        if (playerHealth == null) playerHealth = GetComponent<Health>();
        if (playerHealth == null || playerHealth.CurrentHealth <= 0f ||
            playerHealth.CurrentHealth >= playerHealth.maxHealth || s.item.healAmount <= 0f)
            return false; // never waste potion at full health
        playerHealth.Heal(s.item.healAmount);
        s.quantity--;
        if (s.quantity <= 0) { s.item = null; s.quantity = 0; }
        InventoryChanged?.Invoke();
        return true;
    }

    // 5A.3 additive restore API: validate the COMPLETE 20-slot layout first.
    // No changes occur on invalid data; notify the existing UI once on success.
    public bool TryRestoreSnapshot(MS3AItemData[] items, int[] quantities)
    {
        if (items == null || quantities == null || items.Length != Capacity ||
            quantities.Length != Capacity || slots == null || slots.Count != Capacity)
            return false;
        for (int i = 0; i < Capacity; i++)
        {
            if (slots[i] == null || quantities[i] < 0) return false;
            if (quantities[i] == 0 && items[i] != null) return false;
            if (quantities[i] > 0 && (items[i] == null || quantities[i] > items[i].StackLimit))
                return false;
        }
        for (int i = 0; i < Capacity; i++)
        {
            slots[i].item = items[i];
            slots[i].quantity = quantities[i];
        }
        InventoryChanged?.Invoke();
        return true;
    }
}