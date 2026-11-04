using UnityEngine;

// Add to the SAME GameObject as Health (or its parent).
// 3B: no changes to Health, Enemy AI, WaveManager, EXP or Gold.
[DisallowMultipleComponent]
public sealed class MS3BEnemyLootDrop : MonoBehaviour
{
    public MS3BLootTable lootTable;
    public Transform dropOrigin;
    public bool logDrops;

    private Health health;
    private bool dropped;

    private void OnEnable()
    {
        dropped = false;
        health = GetComponent<Health>();
        if (health == null) health = GetComponentInChildren<Health>(true);
        if (health != null) health.OnDied += OnDied;
        else Debug.LogWarning("[Inventory 3B] Health missing on " + name, this);
    }

    private void OnDisable()
    {
        if (health != null) health.OnDied -= OnDied;
        health = null;
    }

    private void OnDied()
    {
        if (dropped) return;
        dropped = true;
        if (lootTable == null || lootTable.entries == null) return;
        if (health != null && health.CompareTag("Player")) return;

        Vector3 origin = dropOrigin ? dropOrigin.position : (health ? health.transform.position : transform.position);
        int limit = Mathf.Max(1, lootTable.maxDropObjects);
        int count = 0;
        foreach (MS3BLootEntry entry in lootTable.entries)
        {
            if (count >= limit) break;
            if (entry == null || entry.item == null || entry.pickupPrefab == null) continue;
            float chance = Mathf.Clamp(entry.chancePercent, 0f, 100f);
            if (chance < 100f && Random.Range(0f, 100f) >= chance) continue;
            int min = Mathf.Clamp(entry.minAmount, 1, 99);
            int max = Mathf.Clamp(entry.maxAmount, min, 99);
            int amount = Random.Range(min, max + 1);
            if (entry.pickupPrefab.GetComponent<MS3AItemPickup>() == null)
            {
                Debug.LogWarning("[Inventory 3B] Pickup prefab does not have MS3AItemPickup: " + entry.pickupPrefab.name, this);
                continue;
            }

            Vector2 offset = Random.insideUnitCircle * Mathf.Max(0f, lootTable.dropSpread);
            GameObject spawned = Instantiate(entry.pickupPrefab, origin + new Vector3(offset.x, offset.y, 0f), Quaternion.identity);
            MS3AItemPickup pickup = spawned.GetComponent<MS3AItemPickup>();
            if (pickup == null) { Destroy(spawned); continue; }
            pickup.item = entry.item;
            pickup.amount = amount;
            // Existing 3A Pickup handles partial collection / full inventory.
            if (lootTable.pickupLifetimeSeconds > 0f)
                Destroy(spawned, lootTable.pickupLifetimeSeconds);
            count++;
            if (logDrops) Debug.Log("[Inventory 3B] " + name + " dropped " + entry.item.displayName + " x" + amount, this);
        }
    }
}
