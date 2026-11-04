#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Explicit, reviewable prefab installer. NEVER patches scene objects, Health or AI scripts.
public sealed class MS3BItemDropInstaller : EditorWindow
{
    private enum EnemyKind { Normal, Ranged, Healer, Summoner, Boss1, Boss2 }

    private sealed class Candidate
    {
        public string path;
        public EnemyKind kind;
        public bool enabled = true;
    }

    private const string Root = "Assets/MonsterSurvival/Features/Inventory/Inventory3B";
    private const string Tables = Root + "/LootTables";
    private const string Demo3A = "Assets/MonsterSurvival/Features/Inventory/Inventory3A/Content";
    private static readonly string[] TableNames =
    {
        "Loot_Normal", "Loot_Ranged", "Loot_Healer", "Loot_Summoner", "Loot_Boss1_ShadowTitan", "Loot_Boss2_GraveWarden"
    };

    private readonly List<Candidate> candidates = new List<Candidate>();
    private Vector2 scroll;
    private bool includeAssetsWithExistingComponent;

    [MenuItem("Tools/MonsterSurvival/3B Item Drops/1 - SETUP and REVIEW Enemy Prefabs")]
    private static void Open() { GetWindow<MS3BItemDropInstaller>("3B - Item Drops").Show(); }

    [MenuItem("Tools/MonsterSurvival/3B Item Drops/2 - AUDIT drop tables (READ ONLY)")]
    private static void AuditFromMenu()
    {
        int valid = 0, missing = 0;
        for (int i = 0; i < TableNames.Length; i++)
        {
            MS3BLootTable t = AssetDatabase.LoadAssetAtPath<MS3BLootTable>(Tables + "/" + TableNames[i] + ".asset");
            if (t && t.entries != null && t.entries.Count > 0) valid++;
            else missing++;
        }
        int installed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset && asset.GetComponentInChildren<MS3BEnemyLootDrop>(true)) installed++;
        }
        Debug.Log("[Inventory 3B QA] Loot tables=" + valid + "/6, missing=" + missing + ", prefabs with MS3BEnemyLootDrop=" + installed +
            ". Open 1 - SETUP and REVIEW to assign missing prefabs.");
    }

    private void OnEnable() { Scan(); }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("MONSTERSURVIVAL 3B  /  ENEMY ITEM DROPS", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("This installer adds one MS3BEnemyLootDrop component to selected enemy PREFABS. It never edits SampleScene, Health, Enemy AI, Boss AI, WaveManager, EXP or Gold. Backups are created outside Assets.", MessageType.Info);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("1. CREATE SIX LOOT TABLES", GUILayout.Height(32))) CreateTables();
        if (GUILayout.Button("2. RESCAN PREFABS", GUILayout.Height(32))) Scan();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(8);
        includeAssetsWithExistingComponent = EditorGUILayout.ToggleLeft("Show already-configured prefabs", includeAssetsWithExistingComponent);
        EditorGUILayout.LabelField("Detected eligible prefabs: " + candidates.Count + " (review carefully)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Choose the correct enemy role in each dropdown before applying. Uncheck any backup, test or unused prefab. If your prefab isn't listed, see README: it must have Health and an enemy-like name/script.", MessageType.None);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (Candidate c in candidates)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(c.path);
            bool configured = asset && asset.GetComponentInChildren<MS3BEnemyLootDrop>(true);
            if (configured && !includeAssetsWithExistingComponent) continue;
            EditorGUILayout.BeginHorizontal();
            c.enabled = EditorGUILayout.Toggle(c.enabled, GUILayout.Width(22));
            EditorGUILayout.LabelField(Path.GetFileNameWithoutExtension(c.path), GUILayout.MinWidth(150));
            c.kind = (EnemyKind)EditorGUILayout.EnumPopup(c.kind, GUILayout.Width(114));
            if (configured) GUILayout.Label("CONFIGURED", EditorStyles.miniLabel, GUILayout.Width(94));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField(c.path, EditorStyles.miniLabel);
            EditorGUILayout.Space(3);
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("3. APPLY TO CHECKED PREFABS", GUILayout.Height(36))) Apply();
        if (GUILayout.Button("REMOVE 3B FROM CHECKED", GUILayout.Height(36))) Remove();
        EditorGUILayout.EndHorizontal();
        if (GUILayout.Button("AUDIT PREFAB ATTACHMENTS (READ ONLY)")) AuditSelection();
        if (GUILayout.Button("Open loot table assets in Project"))
        {
            MS3BLootTable table = AssetDatabase.LoadAssetAtPath<MS3BLootTable>(Tables + "/" + TableNames[0] + ".asset");
            if (table) { Selection.activeObject = table; EditorGUIUtility.PingObject(table); }
        }
    }

    private void Scan()
    {
        candidates.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path) || path.Contains("/Backups/") || path.Contains("/Inventory3A/Content/") || path.Contains("/Inventory3B/")) continue;
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!asset || !asset.GetComponentInChildren<Health>(true)) continue;
            EnemyKind role;
            if (!TryIdentify(asset, out role)) continue;
            candidates.Add(new Candidate { path = path, kind = role, enabled = !asset.GetComponentInChildren<MS3BEnemyLootDrop>(true) });
        }
        candidates.Sort((a, b) => string.Compare(a.path, b.path, StringComparison.OrdinalIgnoreCase));
        Repaint();
    }

    private static bool TryIdentify(GameObject asset, out EnemyKind role)
    {
        role = EnemyKind.Normal;
        string text = asset.name.ToLowerInvariant();
        foreach (MonoBehaviour behaviour in asset.GetComponentsInChildren<MonoBehaviour>(true))
            if (behaviour != null) text += " " + behaviour.GetType().Name.ToLowerInvariant();
        if (text.Contains("player") || text.Contains("pickup") || text.Contains("projectile") || text.Contains("bullet") || text.Contains("dummy")) return false;
        if (text.Contains("gravewarden") || text.Contains("grave_warden") || text.Contains("grave warden") || text.Contains("boss2") || text.Contains("boss_2")) { role = EnemyKind.Boss2; return true; }
        if (text.Contains("shadowtitan") || text.Contains("shadow_titan") || text.Contains("shadow titan") || text.Contains("boss")) { role = EnemyKind.Boss1; return true; }
        if (text.Contains("summoner")) { role = EnemyKind.Summoner; return true; }
        if (text.Contains("healer")) { role = EnemyKind.Healer; return true; }
        if (text.Contains("ranged") || text.Contains("archer") || text.Contains("sniper")) { role = EnemyKind.Ranged; return true; }
        if (text.Contains("enemy") || text.Contains("monster") || text.Contains("zombie") || text.Contains("skeleton") || text.Contains("goblin")) { role = EnemyKind.Normal; return true; }
        return false;
    }

    private static bool CreateTables()
    {
        MS3AItemData potion = AssetDatabase.LoadAssetAtPath<MS3AItemData>(Demo3A + "/Potion.asset");
        MS3AItemData crystal = AssetDatabase.LoadAssetAtPath<MS3AItemData>(Demo3A + "/Crystal.asset");
        GameObject potionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Demo3A + "/Pickup_Potion.prefab");
        GameObject crystalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Demo3A + "/Pickup_Crystal.prefab");
        if (!potion || !crystal || !potionPrefab || !crystalPrefab)
        {
            EditorUtility.DisplayDialog("Inventory 3B missing dependency", "3A Demo/Potion.asset, Crystal.asset and Pickup prefabs are required. Run the original 3A installer first, then reinstall the 3A.1 click fix if needed. Do NOT delete existing inventory objects.", "OK");
            return false;
        }
        if (!AssetDatabase.IsValidFolder(Root))
        {
            EnsureFolder("Assets/MonsterSurvival");
            AssetDatabase.CreateFolder("Assets/MonsterSurvival", "Inventory3B");
        }
        if (!AssetDatabase.IsValidFolder(Tables)) AssetDatabase.CreateFolder(Root, "LootTables");
        // Chance in percent; each rule rolls independently. Boss guaranteed rules produce two separate pickup objects.
        float[,] defaults = {
            { 14f, 30f, 1f, 1f, 1f, 2f },
            { 17f, 36f, 1f, 1f, 1f, 2f },
            { 45f, 25f, 1f, 2f, 1f, 2f },
            { 24f, 45f, 1f, 2f, 1f, 3f },
            { 100f, 100f, 2f, 3f, 4f, 6f },
            { 100f, 100f, 3f, 5f, 5f, 8f }
        };
        for (int i = 0; i < TableNames.Length; i++)
        {
            string path = Tables + "/" + TableNames[i] + ".asset";
            if (AssetDatabase.LoadAssetAtPath<MS3BLootTable>(path)) continue; // preserve user edits
            MS3BLootTable table = ScriptableObject.CreateInstance<MS3BLootTable>();
            table.entries.Add(new MS3BLootEntry { item = potion, pickupPrefab = potionPrefab, chancePercent = defaults[i,0], minAmount = (int)defaults[i,2], maxAmount = (int)defaults[i,3] });
            table.entries.Add(new MS3BLootEntry { item = crystal, pickupPrefab = crystalPrefab, chancePercent = defaults[i,1], minAmount = (int)defaults[i,4], maxAmount = (int)defaults[i,5] });
            table.maxDropObjects = 3;
            table.dropSpread = i >= 4 ? .80f : .45f;
            table.pickupLifetimeSeconds = 90f;
            AssetDatabase.CreateAsset(table, path);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Inventory 3B] Six loot tables ready. Existing assets were preserved.");
        return true;
    }

    private void Apply()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Inventory 3B", "Stop Play Mode first.", "OK"); return;
        }
        if (!CreateTables()) return;
        List<Candidate> chosen = candidates.FindAll(c => c.enabled);
        if (chosen.Count == 0) { EditorUtility.DisplayDialog("Inventory 3B", "No prefab checked. Click RESCAN and check enemy prefabs.", "OK"); return; }
        if (!EditorUtility.DisplayDialog("Attach 3B loot component?",
            "Apply to " + chosen.Count + " selected enemy prefabs? The installer creates raw backups outside Assets and will NOT replace any gameplay script. Confirm the types and paths shown in the window.",
            "APPLY", "Cancel")) return;
        int changed = 0, errors = 0;
        foreach (Candidate c in chosen)
        {
            MS3BLootTable table = AssetDatabase.LoadAssetAtPath<MS3BLootTable>(Tables + "/" + TableNames[(int)c.kind] + ".asset");
            if (!table) { errors++; continue; }
            GameObject instance = null;
            try
            {
                BackupPrefab(c.path);
                instance = PrefabUtility.LoadPrefabContents(c.path);
                Health health = instance.GetComponent<Health>();
                if (health == null) health = instance.GetComponentInChildren<Health>(true);
                if (health == null) { errors++; continue; }
                MS3BEnemyLootDrop component = instance.GetComponentInChildren<MS3BEnemyLootDrop>(true);
                if (!component) component = health.gameObject.AddComponent<MS3BEnemyLootDrop>();
                component.lootTable = table;
                EditorUtility.SetDirty(component);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, c.path);
                if (!saved) { errors++; Debug.LogError("[3B] Could not save: " + c.path); }
                else { changed++; Debug.Log("[3B] " + c.kind + " -> " + c.path); }
            }
            catch (Exception ex) { errors++; Debug.LogError("[3B] Cannot patch " + c.path + ": " + ex.Message); }
            finally { if (instance) PrefabUtility.UnloadPrefabContents(instance); }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Scan();
        EditorUtility.DisplayDialog("Inventory 3B", "Configured " + changed + " prefab(s), errors: " + errors + ". Check Console and test an enemy death in Play Mode.", "OK");
    }

    private void Remove()
    {
        if (EditorApplication.isPlaying) return;
        List<Candidate> chosen = candidates.FindAll(c => c.enabled);
        if (chosen.Count == 0) { EditorUtility.DisplayDialog("Inventory 3B", "Check the prefabs you want to remove 3B from. Enable 'Show already-configured prefabs' first.", "OK"); return; }
        if (!EditorUtility.DisplayDialog("Remove 3B components?", "Remove ONLY MS3BEnemyLootDrop from " + chosen.Count + " checked prefabs? No other components or loot tables are removed.", "REMOVE", "Cancel")) return;
        int count = 0;
        foreach (Candidate c in chosen)
        {
            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(c.path);
                MS3BEnemyLootDrop[] components = root.GetComponentsInChildren<MS3BEnemyLootDrop>(true);
                if (components.Length == 0) continue;
                foreach (MS3BEnemyLootDrop component in components) DestroyImmediate(component);
                PrefabUtility.SaveAsPrefabAsset(root, c.path);
                count++;
            }
            catch (Exception ex) { Debug.LogError("[3B] REMOVE failed: " + c.path + " " + ex.Message); }
            finally { if (root) PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets(); Scan();
        Debug.Log("[Inventory 3B] Removed component from " + count + " prefab(s). Original AI scripts untouched.");
    }

    private void AuditSelection()
    {
        foreach (Candidate c in candidates)
        {
            if (!c.enabled) continue;
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(c.path);
            MS3BEnemyLootDrop drop = go ? go.GetComponentInChildren<MS3BEnemyLootDrop>(true) : null;
            Debug.Log("[3B Audit] " + c.path + " | role=" + c.kind + " | component=" + (drop ? "YES" : "NO") +
                " | table=" + (drop && drop.lootTable ? drop.lootTable.name : "MISSING"));
        }
    }

    private static void BackupPrefab(string path)
    {
        string guid = AssetDatabase.AssetPathToGUID(path);
        if (string.IsNullOrEmpty(guid)) return;
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string folder = Path.Combine(projectRoot, "MonsterSurvival_3B_Backups");
        Directory.CreateDirectory(folder);
        string destination = Path.Combine(folder, guid + ".prefab");
        if (!File.Exists(destination)) File.Copy(Path.Combine(projectRoot, path), destination);
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif
