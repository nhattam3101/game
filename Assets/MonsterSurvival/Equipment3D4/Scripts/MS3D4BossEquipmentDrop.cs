using UnityEngine;

// Install on the same GameObject as Health of the REAL spawned boss prefab.
// No edits to Health, AI, 3B loot tables, EXP or gold are required.
[DisallowMultipleComponent]
[RequireComponent(typeof(Health))]
public sealed class MS3D4BossEquipmentDrop : MonoBehaviour
{
    public MS3D4BossEquipmentTable table;
    public Transform dropOrigin;
    public bool logResults = false;

    private Health health;
    private bool alreadyDropped;

    private void OnEnable()
    {
        alreadyDropped = false;
        health = GetComponent<Health>();
        if (health != null) health.OnDied += HandleDied;
    }

    private void OnDisable()
    {
        if (health != null) health.OnDied -= HandleDied;
        health = null;
    }

    private void HandleDied()
    {
        if (alreadyDropped) return;
        alreadyDropped = true;
        if (health == null || health.CompareTag("Player") || table == null || table.entries == null) return;

        Vector3 position = dropOrigin ? dropOrigin.position : health.transform.position;
        int limit = Mathf.Max(1, table.maxEquipmentDrops);
        int spawnedCount = 0;
        foreach (MS3D4BossEquipmentEntry rule in table.entries)
        {
            if (spawnedCount >= limit) break;
            if (rule == null || rule.item == null || rule.pickupPrefab == null) continue;
            if (rule.chancePercent <= 0f) continue;
            if (rule.chancePercent < 100f && Random.Range(0f, 100f) >= rule.chancePercent) continue;
            MS3AItemPickup prefabPickup = rule.pickupPrefab.GetComponent<MS3AItemPickup>();
            if (prefabPickup == null)
            {
                Debug.LogWarning("[3D.4] Pickup prefab lacks MS3AItemPickup: " + rule.pickupPrefab.name, this);
                continue;
            }
            int min = Mathf.Clamp(rule.minAmount, 1, 99);
            int max = Mathf.Clamp(rule.maxAmount, min, 99);
            int quantity = Random.Range(min, max + 1);
            Vector2 offset = Random.insideUnitCircle * Mathf.Max(0f, table.spreadRadius);
            GameObject go = Instantiate(rule.pickupPrefab,
                position + new Vector3(offset.x, offset.y, 0f), Quaternion.identity);
            MS3AItemPickup pickup = go.GetComponent<MS3AItemPickup>();
            if (pickup == null)
            {
                Destroy(go);
                continue;
            }
            pickup.item = rule.item;
            pickup.amount = quantity;
            if (table.pickupLifetimeSeconds > 0f) Destroy(go, table.pickupLifetimeSeconds);
            spawnedCount++;
            if (logResults) Debug.Log("[3D.4] " + name + " dropped " + rule.item.displayName + " x" + quantity, this);
        }
        if (logResults) Debug.Log("[3D.4] " + name + " equipment drops: " + spawnedCount, this);
    }
}
