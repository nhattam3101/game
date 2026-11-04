using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 7D.1: READ ONLY equipment adapter, using a user-selected component and field/property paths.
/// Does not change Inventory, Equipment, Scene saving or Profile data.
/// Maps exactly three equipped SLOT entries, not the inventory bag.
/// </summary>
[DisallowMultipleComponent]
public sealed class MS7D1EquippedItemReader : MonoBehaviour
{
    [Header("Drag the existing EQUIPMENT component (NOT the Inventory bag)")]
    public MonoBehaviour equipmentSource;

    [Header("Option A: array/list containing EXACTLY 3 equipped slots")]
    [Tooltip("Example: equippedItems or equipment.slots. Must resolve to a 3-element list/array.")]
    public string threeSlotCollectionPath = "";

    [Header("Option B: paths to three separate equipped slots (if Option A is blank)")]
    public string slot1Path = "";
    public string slot2Path = "";
    public string slot3Path = "";

    [Header("How to read an ID from each entry")]
    [Tooltip("Optional path on EACH item, e.g. itemData.itemId or definition.id. Blank = safe known-name candidates.")]
    public string itemIdPath = "";
    [Tooltip("Keep OFF to avoid accidentally matching display names with real item IDs.")]
    public bool allowObjectNameFallback = false;

    private static readonly string[] DefaultIds = {
        "itemId", "ItemId", "itemID", "ItemID", "uniqueId", "UniqueId", "id", "ID",
        "item.itemId", "item.id", "itemData.itemId", "itemData.id",
        "data.itemId", "data.id", "definition.itemId", "definition.id",
        "equipmentData.itemId", "equipmentData.id"
    };

    public bool Configured {
        get {
            return equipmentSource != null &&
                (!string.IsNullOrWhiteSpace(threeSlotCollectionPath) ||
                 (!string.IsNullOrWhiteSpace(slot1Path) && !string.IsNullOrWhiteSpace(slot2Path) && !string.IsNullOrWhiteSpace(slot3Path)));
        }
    }

    /// <summary>Returns false if no verified 3-slot equipment source is connected.</summary>
    public bool TryRead(out string[] ids, out string diagnostic)
    {
        ids = new string[3] { "", "", "" };
        diagnostic = "";
        if (!Configured) { diagnostic = "Chua gan equipmentSource va duong dan 3 slot"; return false; }
        if (!equipmentSource.gameObject.scene.IsValid()) { diagnostic = "Source khong phai Scene component"; return false; }
        try
        {
            if (!string.IsNullOrWhiteSpace(threeSlotCollectionPath))
            {
                object raw = GetPath(equipmentSource, threeSlotCollectionPath);
                if (raw == null) { diagnostic = "Collection path tra ve null (hoac sai ten)"; return false; }
                if (raw is string || !(raw is IEnumerable)) { diagnostic = "Collection path khong phai array/list"; return false; }
                var list = new System.Collections.Generic.List<object>();
                foreach (object entry in (IEnumerable)raw) {
                    list.Add(entry);
                    if (list.Count > 3) break;
                }
                if (list.Count != 3) { diagnostic = "Can dung 3 equipment slots, tim thay " + list.Count; return false; }
                for (int i = 0; i < 3; i++) ids[i] = ResolveId(list[i]);
            }
            else
            {
                ids[0] = ResolveId(GetPath(equipmentSource, slot1Path));
                ids[1] = ResolveId(GetPath(equipmentSource, slot2Path));
                ids[2] = ResolveId(GetPath(equipmentSource, slot3Path));
            }
            diagnostic = "Read 3 equipped slots OK; empty slots are allowed. IDs: " +
                (string.IsNullOrEmpty(ids[0]) ? "-" : ids[0]) + ", " +
                (string.IsNullOrEmpty(ids[1]) ? "-" : ids[1]) + ", " +
                (string.IsNullOrEmpty(ids[2]) ? "-" : ids[2]);
            return true;
        }
        catch (Exception e)
        {
            diagnostic = "Reader error: " + e.GetType().Name + ": " + e.Message;
            return false;
        }
    }

    private string ResolveId(object entry)
    {
        if (entry == null) return "";
        // Destroyed Unity objects can persist as boxed null references.
        if (entry is UnityEngine.Object && (UnityEngine.Object)entry == null) return "";
        if (entry is string) return ((string)entry).Trim();
        if (!string.IsNullOrWhiteSpace(itemIdPath)) {
            return ToId(GetPath(entry, itemIdPath));
        }
        foreach (string candidate in DefaultIds) {
            object value = GetPath(entry, candidate);
            string id = ToId(value);
            if (!string.IsNullOrWhiteSpace(id)) return id;
        }
        if (allowObjectNameFallback && entry is UnityEngine.Object)
            return ((UnityEngine.Object)entry).name.Trim();
        return "";
    }

    private static string ToId(object value)
    {
        if (value == null) return "";
        if (value is string) return ((string)value).Trim();
        if (value is int || value is long || value is Guid) return value.ToString();
        return ""; // Do not treat arbitrary ToString() as an item ID.
    }

    public static object GetPath(object root, string path)
    {
        if (root == null || string.IsNullOrWhiteSpace(path)) return null;
        object current = root;
        foreach (string token in path.Split('.')) {
            if (current == null) return null;
            Type type = current.GetType();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            FieldInfo field = null;
            // Base types may hold [SerializeField] private members.
            for (Type t = type; t != null && field == null; t = t.BaseType)
                field = t.GetField(token, flags | BindingFlags.DeclaredOnly);
            if (field != null) { current = field.GetValue(current); continue; }
            PropertyInfo property = type.GetProperty(token, flags);
            if (property == null || property.GetIndexParameters().Length != 0 || property.GetGetMethod(true) == null) return null;
            current = property.GetValue(current, null);
        }
        return current;
    }
}
