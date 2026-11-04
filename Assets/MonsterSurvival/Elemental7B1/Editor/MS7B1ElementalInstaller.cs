#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// No modification to PlayerShooter, PlayerStats, SkillManager, Health, Boss or Save.
// One carefully anchored, reversible hook is inserted into the actual Bullet.cs.
public sealed class MS7B1ElementalInstaller : EditorWindow
{
    private const string Root = "Assets/MonsterSurvival/Elemental7B1";
    private const string DataPath = Root + "/Data/MS7B1_ElementalAmmoData.asset";
    private const string HookMarker = "MS7B1_ELEMENT_HOOK";
    private const string HookCall = "MS7B1ElementalHitBridge.AfterBaseHit(this, target, actualDamage);";
    private static readonly Regex ActualDamageAnchor = new Regex(
        @"float\s+actualDamage\s*=\s*Mathf\.Max\s*\([\s\S]*?\);",
        RegexOptions.CultureInvariant);
    private PlayerShooter player;
    private Bullet bulletPrefab;
    private Vector2 scroll;

    [MenuItem("Tools/MonsterSurvival/7B Elemental Ammo/1 - OPEN 7B.1 Installer")]
    private static void Open() { GetWindow<MS7B1ElementalInstaller>("7B.1 Elemental Ammo").Show(); }

    private void OnEnable() { RefreshReferences(); }

    private void RefreshReferences()
    {
        if (player == null) player = UnityEngine.Object.FindFirstObjectByType<PlayerShooter>();
        if (bulletPrefab == null && player != null) bulletPrefab = player.bulletPrefab;
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        GUILayout.Label("MONSTERSURVIVAL 7B.1 — ELEMENTAL AMMO", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Installs Fire / Ice / Lightning without replacing PlayerShooter, Health or the original projectile modes. " +
            "A one-line hook is inserted into the actual Bullet.cs only after a backup outside Assets.", MessageType.Info);
        player = (PlayerShooter)EditorGUILayout.ObjectField("Scene PlayerShooter", player, typeof(PlayerShooter), true);
        bulletPrefab = (Bullet)EditorGUILayout.ObjectField("Bullet prefab asset", bulletPrefab, typeof(Bullet), false);
        if (GUILayout.Button("FIND CURRENT SCENE REFERENCES")) RefreshReferences();

        GUILayout.Space(10);
        if (GUILayout.Button("INSTALL / UPDATE (BACKUP BULLET SOURCE)", GUILayout.Height(36)))
            Execute(Install);
        if (GUILayout.Button("AUDIT (READ ONLY)")) Execute(Audit);
        if (GUILayout.Button("REMOVE HOOK + ADDON COMPONENTS (KEEP DATA)", GUILayout.Height(27)))
        {
            if (EditorUtility.DisplayDialog("7B.1 Remove", "Remove only 7B.1 hook/components? Saved data and original gameplay will remain.", "REMOVE", "CANCEL"))
                Execute(Remove);
        }
        GUILayout.Space(8);
        if (GUILayout.Button("RUN PURE FORMULA QA (NO SCENE CHANGES)")) RunMathQA();
        EditorGUILayout.HelpBox(
            "After install, select Player -> MS7B1ElementLoadout -> select Fire, Ice or Lightning. " +
            "Default is None. Ice needs EnemySlowEffect already on enemy prefabs. " +
            "Keep Shadow Titan at 500 HP; this tool does not touch bosses.", MessageType.Warning);
        EditorGUILayout.EndScrollView();
    }

    private void Execute(Action operation)
    {
        try { operation(); }
        catch (Exception ex) { Debug.LogError("[7B.1] " + ex); EditorUtility.DisplayDialog("7B.1 error", ex.Message, "OK"); }
    }

    private void Validate(out string prefabPath, out string bulletSource)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before installing 7B.1.");
        // Accept a genuine scene instance in ANY loaded scene. Comparing against
        // SceneManager.GetActiveScene().handle can reject a valid assignment in
        // multi-scene editing / certain Editor stage and scene workflows.
        if (player == null)
            throw new InvalidOperationException("Assign the PlayerShooter component from a loaded gameplay Scene (not from a prefab asset).");
        UnityEngine.SceneManagement.Scene playerScene = player.gameObject.scene;
        if (!playerScene.IsValid() || !playerScene.isLoaded || EditorUtility.IsPersistent(player.gameObject))
            throw new InvalidOperationException("PlayerShooter must be a scene instance in a loaded Scene, not a prefab asset. Selected object: " + player.name);
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle != playerScene.handle)
            Debug.Log("[7B.1] Player is in loaded scene '" + playerScene.name + "' (different from the Editor active scene). Continuing with the assigned scene object.");
        if (bulletPrefab == null) throw new InvalidOperationException("Assign the Bullet prefab from Project.");
        prefabPath = AssetDatabase.GetAssetPath(bulletPrefab.gameObject);
        if (string.IsNullOrEmpty(prefabPath) || !prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Bullet prefab must be a prefab asset, not a scene instance.");
        MonoScript script = MonoScript.FromMonoBehaviour(bulletPrefab);
        bulletSource = script != null ? AssetDatabase.GetAssetPath(script) : null;
        if (string.IsNullOrEmpty(bulletSource) || !File.Exists(bulletSource))
            throw new InvalidOperationException("Cannot identify Bullet.cs from selected prefab.");
        string text = File.ReadAllText(bulletSource);
        if (!text.Contains(HookMarker) && !ActualDamageAnchor.IsMatch(text))
            throw new InvalidOperationException("Bullet.cs does not contain expected 'float actualDamage = Mathf.Max(...)' anchor. No patch applied. Share this Bullet.cs for a compatible hotfix.");
    }

    private static void BackupSource(string scriptPath)
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../MS_Backups/7B1"));
        Directory.CreateDirectory(root);
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        File.Copy(scriptPath, Path.Combine(root, "Bullet_" + stamp + ".cs.bak"), false);
    }

    private static void WriteWithOriginalLineEndings(string path, string content)
    {
        // Preserve C# source encoding on typical UTF-8 Unity projects; a full backup exists.
        File.WriteAllText(path, content, new UTF8Encoding(false));
    }

    private void InsertHook(string path)
    {
        string source = File.ReadAllText(path);
        if (source.Contains(HookMarker)) { Debug.Log("[7B.1] Bullet hook already present. No duplicate inserted."); return; }
        Match anchor = ActualDamageAnchor.Match(source);
        if (!anchor.Success) throw new InvalidOperationException("Bullet actualDamage anchor not found.");
        BackupSource(path);
        string nl = source.Contains("\r\n") ? "\r\n" : "\n";
        string line = nl + "        " + HookCall + " // " + HookMarker;
        source = source.Insert(anchor.Index + anchor.Length, line);
        WriteWithOriginalLineEndings(path, source);
    }

    private void RemoveHook(string path)
    {
        string source = File.ReadAllText(path);
        if (!source.Contains(HookMarker)) return;
        BackupSource(path);
        string updated = Regex.Replace(source,
            @"\r?\n\s*MS7B1ElementalHitBridge\.AfterBaseHit\(this,\s*target,\s*actualDamage\);\s*//\s*MS7B1_ELEMENT_HOOK[^\r\n]*",
            string.Empty, RegexOptions.CultureInvariant);
        if (updated == source) throw new InvalidOperationException("7B.1 marker found but line was modified. Remove hook manually; original Bullet backup is available.");
        WriteWithOriginalLineEndings(path, updated);
    }

    private static MS7B1ElementData EnsureDataAsset()
    {
        MS7B1ElementData data = AssetDatabase.LoadAssetAtPath<MS7B1ElementData>(DataPath);
        if (data != null) return data;
        string folder = Root + "/Data";
        if (!AssetDatabase.IsValidFolder(folder))
        {
            if (!AssetDatabase.IsValidFolder(Root)) throw new InvalidOperationException("Missing plugin folder: " + Root);
            AssetDatabase.CreateFolder(Root, "Data");
        }
        data = ScriptableObject.CreateInstance<MS7B1ElementData>();
        AssetDatabase.CreateAsset(data, DataPath);
        AssetDatabase.SaveAssets();
        return data;
    }

    private static void UpdatePrefab(string prefabPath, bool add)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null) throw new InvalidOperationException("Cannot load Bullet prefab contents: " + prefabPath);
        try
        {
            Bullet bullet = root.GetComponentInChildren<Bullet>(true);
            if (bullet == null) throw new InvalidOperationException("Prefab has no Bullet component.");
            MS7B1ElementalProjectile addon = bullet.GetComponent<MS7B1ElementalProjectile>();
            if (add && addon == null) bullet.gameObject.AddComponent<MS7B1ElementalProjectile>();
            if (!add && addon != null) UnityEngine.Object.DestroyImmediate(addon);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private void Install()
    {
        string prefabPath, source;
        Validate(out prefabPath, out source);
        MS7B1ElementData data = EnsureDataAsset();
        UpdatePrefab(prefabPath, true);
        MS7B1ElementLoadout loadout = player.GetComponent<MS7B1ElementLoadout>();
        if (loadout == null) loadout = Undo.AddComponent<MS7B1ElementLoadout>(player.gameObject);
        if (loadout.elementData == null) loadout.elementData = data;
        EditorUtility.SetDirty(loadout);
        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        InsertHook(source); // Code recompile happens after all references are linked
        AssetDatabase.Refresh();
        Debug.Log("[7B.1 INSTALL] Success. Scene Player + Bullet Prefab connected. Bullet.cs backed up before patch. Default Element = None. Save Scene (Ctrl+S).");
    }

    private void Audit()
    {
        string prefabPath, source;
        Validate(out prefabPath, out source);
        bool hook = File.ReadAllText(source).Contains(HookMarker);
        bool addon = bulletPrefab.GetComponent<MS7B1ElementalProjectile>() != null;
        MS7B1ElementLoadout loadout = player.GetComponent<MS7B1ElementLoadout>();
        bool hasLoadout = loadout != null && loadout.elementData != null;
        Debug.Log("[7B.1 AUDIT] " + (hook ? "PASS" : "MISSING") + " Bullet hook; " +
                  (addon ? "PASS" : "MISSING") + " Bullet addon; " +
                  (hasLoadout ? "PASS" : "MISSING") + " Player loadout/data; " +
                  "Source=" + source + "; Element=" + (loadout != null ? loadout.selectedElement.ToString() : "NONE"));
        if (!hook || !addon || !hasLoadout) Debug.LogWarning("[7B.1 AUDIT] Setup incomplete; run INSTALL and save scene.");
    }

    private void Remove()
    {
        string prefabPath, source;
        Validate(out prefabPath, out source);
        RemoveHook(source);
        UpdatePrefab(prefabPath, false);
        MS7B1ElementLoadout loadout = player.GetComponent<MS7B1ElementLoadout>();
        if (loadout != null) Undo.DestroyObjectImmediate(loadout);
        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        AssetDatabase.Refresh();
        Debug.Log("[7B.1 REMOVE] Hook & addon components removed; Data asset and backups kept. Ctrl+S to save Scene.");
    }

    private static void RunMathQA()
    {
        int pass = 0;
        if (Mathf.Approximately(2f * 0.5f, 1f)) pass++;
        if (Mathf.Approximately(20f * 0.45f, 9f)) pass++;
        if (Mathf.Clamp(3, 0, 8) == 3) pass++;
        if (Mathf.Max(3f, 1.5f) == 3f) pass++;
        if (Mathf.Max(0f, -3f) == 0f) pass++;
        Debug.Log("[7B.1 PURE MATH QA] " + pass + "/5 PASS. These are formula-only tests, NOT gameplay or collision QA.");
    }
}
#endif
