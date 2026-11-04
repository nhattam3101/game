using System;
using UnityEngine;

// 5C.3: safety-first map access. Profiles created before this feature keep legacy
// map access unless their owner explicitly activates restrictions in MainMenu.
public static class MS5C3MapPolicy
{
    public const string Marker = "system.map_gate_5c3_v1";
    private static readonly string[] MapIds = {
        "map.green_meadow", "map.sand_dunes", "map.ancient_ruins"
    };

    public struct State
    {
        public bool valid;
        public bool isNew;
        public bool legacy;
        public bool recovered;
        public bool gated;
        public bool allowed;
        public int slot;
        public int mapIndex;
        public string status;
    }

    public static bool TryGet(int slot, int mapIndex, out State state)
    {
        state = new State { slot = slot, mapIndex = mapIndex };
        if (slot < 1 || slot > 3 || mapIndex < 0 || mapIndex > 2)
        { state.status = "INVALID_SLOT_OR_MAP"; return false; }
        MS5A1SaveData data;
        bool fromBackup;
        string reason;
        bool exists = MS5A1SaveService.TryLoad(slot, out data, out reason, out fromBackup);
        if (!exists)
        {
            if (string.IsNullOrEmpty(reason) || !reason.StartsWith("NO_SAVE", StringComparison.Ordinal))
            { state.status = "SAVE_UNAVAILABLE: " + reason; return false; }
            state.valid = true;
            state.isNew = true;
            state.gated = true;
            state.allowed = mapIndex == 0;
            state.status = "NEW_PROFILE";
            return true;
        }
        if (data.unlockedContentIds == null)
        { state.status = "MISSING_UNLOCK_LIST"; return false; }
        state.valid = true;
        state.recovered = fromBackup;
        state.gated = data.unlockedContentIds.Contains(Marker);
        state.legacy = !state.gated;
        state.allowed = mapIndex == 0 || !state.gated || data.unlockedContentIds.Contains(MapIds[mapIndex]);
        state.status = fromBackup ? "BACKUP_ONLY_READ_ONLY" : state.legacy ? "LEGACY_ACCESS" : "ENFORCED";
        return true;
    }

    // Create a marker only when an actual NEW profile enters Map Selection.
    // This prevents that new profile from accidentally gaining legacy access
    // after its first Victory creates a save. Never overwrites existing files.
    public static bool TryInitializeNewProfile(int slot, out string result)
    {
        State s;
        if (!TryGet(slot, 0, out s)) { result = s.status; return false; }
        if (!s.isNew) { result = "NOT_NEW_PROFILE"; return true; }
        MS5A1SaveData fresh = MS5A1SaveService.CreateFresh(slot);
        fresh.unlockedContentIds.Add(Marker);
        MS5A1SaveData saved;
        return MS5A1SaveService.TrySave(slot, fresh, out saved, out result);
    }

    // Explicit legacy opt-in only; does not grant/charge any gold.
    public static bool TryActivateLegacy(int slot, out string result)
    {
        State state;
        if (!TryGet(slot, 0, out state)) { result = state.status; return false; }
        if (state.isNew) return TryInitializeNewProfile(slot, out result);
        if (state.recovered) { result = "BACKUP_ONLY_RESTORE_FIRST"; return false; }
        if (!state.legacy) { result = "ALREADY_ENFORCED"; return false; }
        MS5A1SaveData data;
        bool recovered;
        string reason;
        if (!MS5A1SaveService.TryLoad(slot, out data, out reason, out recovered) || recovered)
        { result = "LOAD_BLOCKED: " + reason; return false; }
        if (!data.unlockedContentIds.Contains(Marker)) data.unlockedContentIds.Add(Marker);
        MS5A1SaveData saved;
        return MS5A1SaveService.TrySave(slot, data, out saved, out result);
    }
}
