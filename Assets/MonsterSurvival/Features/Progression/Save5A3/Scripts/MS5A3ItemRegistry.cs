using System.Collections.Generic;
using UnityEngine;

// Explicit ID -> ScriptableObject mapping. Asset names / Unity instance IDs are NEVER saved.
[CreateAssetMenu(fileName="MS5A3ItemRegistry", menuName="MonsterSurvival/Save 5A.3/Item Registry")]
public sealed class MS5A3ItemRegistry : ScriptableObject
{
    public List<MS3AItemData> items = new List<MS3AItemData>();

    public bool TryBuild(out Dictionary<string, MS3AItemData> lookup, out string error)
    {
        lookup = new Dictionary<string, MS3AItemData>(System.StringComparer.Ordinal);
        if (items == null) { error = "Registry.items is null"; return false; }
        foreach (var item in items)
        {
            if (!item) { error = "Null item in registry"; return false; }
            string id = item.itemId;
            if (string.IsNullOrEmpty(id) || id.Length > 96)
            { error = "Invalid itemId on " + item.name; return false; }
            foreach (char c in id)
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.'))
                { error = "Invalid itemId character: " + id; return false; }
            if (lookup.ContainsKey(id)) { error = "DUPLICATE itemId: " + id; return false; }
            lookup.Add(id, item);
        }
        error = "OK: " + lookup.Count + " unique Item IDs";
        return true;
    }
}
