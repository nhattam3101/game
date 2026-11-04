using System;
using System.Collections.Generic;

// Plain serializable data only. No Unity Object, MonoBehaviour, item assets, or scene refs.
// 5A.1: Fields are reserved for FUTURE integration. Nothing auto-syncs from gameplay yet.
[Serializable]
public sealed class MS5A1UpgradeEntry
{
    public string id;
    public int level;
}

[Serializable]
public sealed class MS5A3SavedStack
{
    public string itemId = "";
    public int quantity;
}

[Serializable]
public sealed class MS5A3SavedEquipped
{
    public int type;  // MS3DEquipmentType: 0 Weapon, 1 Armor, 2 Accessory
    public string itemId = "";
}

[Serializable]
public sealed class MS5A1SaveData
{
    public const int SchemaVersion = 1;

    public int schemaVersion = SchemaVersion;
    public string profileId;
    public string createdUtc;
    public string lastSavedUtc;
    public int revision;

    // Future persistent progression, NOT the current PlayerProgression.coins.
    public int bankGold;

    // 5A.3 optional extension. An older 5A.1/5A.2 profile with no snapshot
    // remains valid: inventorySnapshotPresent=false, no items are injected.
    public bool inventorySnapshotPresent;
    public List<MS5A3SavedStack> inventorySlots = new List<MS5A3SavedStack>();
    public List<MS5A3SavedEquipped> equippedItems = new List<MS5A3SavedEquipped>();
    public string lastCommittedRunId = "";
    public List<string> unlockedContentIds = new List<string>();
    public List<MS5A1UpgradeEntry> permanentUpgrades = new List<MS5A1UpgradeEntry>();
}

[Serializable]
internal sealed class MS5A1SaveEnvelope
{
    public string magic;
    public int formatVersion;
    public string sha256;
    public string payloadJson;
}
