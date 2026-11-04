using System;
using System.Collections.Generic;
using UnityEngine;

// Stage 3B. Rules roll independently; each successful rule spawns one 3A pickup.
[Serializable]
public sealed class MS3BLootEntry
{
    public MS3AItemData item;
    public GameObject pickupPrefab;
    [Range(0f, 100f)] public float chancePercent = 10f;
    [Min(1)] public int minAmount = 1;
    [Min(1)] public int maxAmount = 1;
}

[CreateAssetMenu(fileName = "Loot_Enemy", menuName = "MonsterSurvival/Inventory 3B/Loot Table")]
public sealed class MS3BLootTable : ScriptableObject
{
    public List<MS3BLootEntry> entries = new List<MS3BLootEntry>();
    [Min(1)] public int maxDropObjects = 3;
    [Min(0f)] public float dropSpread = 0.48f;
    [Min(0f)] public float pickupLifetimeSeconds = 90f;
}
