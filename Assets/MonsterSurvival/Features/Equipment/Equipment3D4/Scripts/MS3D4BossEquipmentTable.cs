using System;
using System.Collections.Generic;
using UnityEngine;

// Equipment-only drops. Separate from existing MS3BLootTable (potions, crystals, etc.).
[Serializable]
public sealed class MS3D4BossEquipmentEntry
{
    public MS3AItemData item;
    public GameObject pickupPrefab;
    [Range(0f, 100f)] public float chancePercent = 25f;
    [Min(1)] public int minAmount = 1;
    [Min(1)] public int maxAmount = 1;
}

[CreateAssetMenu(fileName = "BossGearDrops", menuName = "MonsterSurvival/Equipment 3D4/Boss Equipment Table")]
public sealed class MS3D4BossEquipmentTable : ScriptableObject
{
    public List<MS3D4BossEquipmentEntry> entries = new List<MS3D4BossEquipmentEntry>();
    [Min(1)] public int maxEquipmentDrops = 3;
    [Min(0f)] public float spreadRadius = 0.65f;
    [Min(0f)] public float pickupLifetimeSeconds = 120f;
}
