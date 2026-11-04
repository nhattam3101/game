using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Install exactly ONE copy in Assets/MonsterSurvival/Elemental7B3/Editor.
// Safely patches the existing 7B.1 elemental source; NEVER touches Bullet.cs.
public sealed class MS7B3VFXInstaller : EditorWindow
{
    private const string HookScriptName = "MS7B1ElementalProjectile.cs";
    private const string AudioDir = "Assets/MonsterSurvival/Elemental7B3/Audio/";
    private MS7B1ElementLoadout player;

    private const string FireOriginal = "burn.Apply(settings.burnDuration, settings.burnDamagePerSecond, settings.burnTickSeconds);";
    private const string FirePatch = FireOriginal + "\n            MS7B3VisualBridge.AfterFire(primary, settings);";

    private const string IceOriginal = "if (slow != null) slow.ApplySlow(settings.slowPercent, settings.slowDuration);";
    private const string IcePatch = @"if (slow != null)
            {
                // Compatibility with legacy Frost/Slow Shot: EXACTLY ONE ApplySlow per hit.
                // Bullet.HandleSlow executes after the 7B.1 elemental bridge.
                Bullet oldBullet = GetComponent<Bullet>();
                bool hasLegacySlow = oldBullet != null &&
                    oldBullet.projectileEffect != null &&
                    oldBullet.projectileEffect.effectType == ProjectileEffectType.Slow;
                if (!hasLegacySlow)
                    slow.ApplySlow(settings.slowPercent, settings.slowDuration);
                MS7B3VisualBridge.AfterIce(primary, settings);
            }";

    private const string PrimaryOriginal = "ChainFrom(primary, actualDamage, enemyMask);";
    private const string PrimaryPatch = "MS7B3VisualBridge.AfterLightningPrimary(primary);\n            " + PrimaryOriginal;
    private const string ArcOriginal = "DrawArc(center, destination);";
    private const string ArcPatch = ArcOriginal + "\n            MS7B3VisualBridge.AfterLightningJump(center, destination, settings);";

    [MenuItem("Tools/MonsterSurvival/7B Elemental VFX/1 - OPEN 7B.3 Installer")]
    public static void Open() { GetWindow<MS7B3VFXInstaller>("7B.3 VFX & Audio"); }

    [MenuItem("Tools/MonsterSurvival/7B Elemental VFX/2 - AUDIT (READ ONLY)")]
    public static void AuditMenu() { Audit(); }

    private void OnEnable() { DetectPlayer(); }
    private void OnGUI()
    {
        EditorGUILayout.LabelField("MONSTERSURVIVAL 7B.3 — ELEMENTAL VFX & AUDIO", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Non-destructive additive VFX. Legacy Frost Shot takes priority over Ice Ammo on the SAME bullet, avoiding duplicate ApplySlow. Fire/Lightning unaffected. Requires 7B.1 installed and 0 compile errors.", MessageType.Info);
        player = (MS7B1ElementLoadout)EditorGUILayout.ObjectField("Scene Player Loadout", player, typeof(MS7B1ElementLoadout), true);
        if (GUILayout.Button("FIND CURRENT SCENE PLAYER")) DetectPlayer();
        EditorGUILayout.Space(5);
        if (GUILayout.Button("INSTALL / UPDATE 7B.3 (BACKUP FIRST)", GUILayout.Height(34)))
            Run("Install", Install);
        if (GUILayout.Button("AUDIT ONLY (DO NOT MODIFY)")) Audit();
        if (GUILayout.Button("REMOVE 7B.3 (RESTORE ELEMENTAL SOURCE)", GUILayout.Height(27)))
            Run("Remove", Remove);
        EditorGUILayout.HelpBox("Install in Edit Mode and SAVE your Scene. After import Unity recompiles. Original 7B.1/7B.2 scripts except the tiny 7B.1 hook are preserved. No Save/Inventory/Equipment/Boss modifications.", MessageType.Warning);
    }

    private void Run(string title, Action work)
    {
        try
        {
            work();
            Debug.Log("[7B.3] " + title + " complete. Re-run AUDIT after recompile.");
        }
        catch (Exception ex)
        {
            Debug.LogError("[7B.3] " + title + " aborted: " + ex.Message);
            EditorUtility.DisplayDialog("7B.3 " + title + " failed", ex.Message, "OK");
        }
    }

    private void DetectPlayer()
    {
        player = FindFirstObjectByType<MS7B1ElementLoadout>();
        if (player != null && (!player.gameObject.scene.IsValid() || !player.gameObject.scene.isLoaded))
            player = null;
    }

    private static void EnsureEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before using Installer.");
    }

    private static string SourcePath()
    {
        string[] guids = AssetDatabase.FindAssets("MS7B1ElementalProjectile t:MonoScript");
        string chosen = null;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileName(path) != HookScriptName) continue;
            if (chosen != null) throw new InvalidOperationException("Two MS7B1ElementalProjectile.cs files found. Remove duplicates first.");
            chosen = path;
        }
        if (string.IsNullOrEmpty(chosen))
            throw new InvalidOperationException("Cannot find 7B.1 MS7B1ElementalProjectile.cs. Install and compile 7B.1 first.");
        return chosen;
    }

    private static string Absolute(string assetPath)
    {
        return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath));
    }

    private static int Occurrences(string text, string pattern)
    {
        int count = 0, start = 0;
        while (true)
        {
            int idx = text.IndexOf(pattern, start, StringComparison.Ordinal);
            if (idx < 0) return count;
            count++;
            start = idx + pattern.Length;
        }
    }

    private static string ReplaceUnique(string source, string before, string after)
    {
        if (Occurrences(source, before) != 1)
            throw new InvalidOperationException("7B.1 source layout differs from expected. Expected ONE match for: " + before.Substring(0, Math.Min(70, before.Length)) + " . No source has been written.");
        return source.Replace(before, after);
    }

    private static string MakePatchedSource(string current)
    {
        if (current.Contains("MS7B3VisualBridge.AfterFire("))
        {
            if (current.Contains("MS7B3VisualBridge.AfterIce(") &&
                current.Contains("MS7B3VisualBridge.AfterLightningPrimary(") &&
                current.Contains("MS7B3VisualBridge.AfterLightningJump("))
                return current;
            throw new InvalidOperationException("Partial 7B.3 hook found. Restore 7B.1 source from backup, then retry.");
        }
        string next = ReplaceUnique(current, FireOriginal, FirePatch);
        next = ReplaceUnique(next, IceOriginal, IcePatch);
        next = ReplaceUnique(next, PrimaryOriginal, PrimaryPatch);
        next = ReplaceUnique(next, ArcOriginal, ArcPatch);
        return next;
    }

    private static string MakeRestoredSource(string current)
    {
        if (!current.Contains("MS7B3VisualBridge.")) return current;
        string next = ReplaceUnique(current, FirePatch, FireOriginal);
        next = ReplaceUnique(next, IcePatch, IceOriginal);
        next = ReplaceUnique(next, PrimaryPatch, PrimaryOriginal);
        next = ReplaceUnique(next, ArcPatch, ArcOriginal);
        if (next.Contains("MS7B3VisualBridge."))
            throw new InvalidOperationException("Unknown 7B.3 references remain. No source has been written.");
        return next;
    }

    private static string Normalize(string source) { return source.Replace("\r\n", "\n"); }
    private static string Unnormalize(string source, bool crlf) { return crlf ? source.Replace("\n", "\r\n") : source; }

    private static void BackupSourceAndScene(string sourcePath, string stage)
    {
        string root = Directory.GetParent(Application.dataPath).FullName;
        string dir = Path.Combine(root, "MS_Backups", "7B3", DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + stage);
        Directory.CreateDirectory(dir);
        File.Copy(Absolute(sourcePath), Path.Combine(dir, HookScriptName), true);
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.IsValid() && !string.IsNullOrEmpty(scene.path))
        {
            string sceneSource = Absolute(scene.path);
            if (File.Exists(sceneSource)) File.Copy(sceneSource, Path.Combine(dir, Path.GetFileName(sceneSource)), true);
        }
        Debug.Log("[7B.3] Backed up source/scene to: " + dir);
    }

    private void Install()
    {
        EnsureEditMode();
        if (player == null) DetectPlayer();
        if (player == null || EditorUtility.IsPersistent(player) ||
            !player.gameObject.scene.IsValid() || !player.gameObject.scene.isLoaded)
            throw new InvalidOperationException("Open SampleScene and assign a loaded SCENE Player with MS7B1ElementLoadout (not a Prefab asset).");

        string sourcePath = SourcePath();
        string oldRaw = File.ReadAllText(Absolute(sourcePath));
        bool crlf = oldRaw.Contains("\r\n");
        string patched = MakePatchedSource(Normalize(oldRaw)); // verify ALL patterns before side effects
        BackupSourceAndScene(sourcePath, "install");

        MS7B3VisualHub hub = player.GetComponent<MS7B3VisualHub>();
        if (hub == null) hub = Undo.AddComponent<MS7B3VisualHub>(player.gameObject);
        Undo.RecordObject(hub, "7B.3 Elemental VFX config");
        hub.fireHit = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "MS7B3_FireHit.wav");
        hub.iceHit = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "MS7B3_IceHit.wav");
        hub.lightningHit = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "MS7B3_LightningHit.wav");
        EditorUtility.SetDirty(hub);
        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        if (!EditorSceneManager.SaveScene(player.gameObject.scene))
            throw new InvalidOperationException("Could not save gameplay Scene. Source patch has NOT been written.");
        if (patched != Normalize(oldRaw))
        {
            File.WriteAllText(Absolute(sourcePath), Unnormalize(patched, crlf), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(sourcePath, ImportAssetOptions.ForceUpdate);
        }
        Debug.Log("[7B.3 INSTALL] Hub attached once, audio linked, compatible hook=" + patched.Contains("MS7B3VisualBridge.AfterFire(") + ". No Bullet.cs / Save changes.");
    }

    private void Remove()
    {
        EnsureEditMode();
        string sourcePath = SourcePath();
        string oldRaw = File.ReadAllText(Absolute(sourcePath));
        bool crlf = oldRaw.Contains("\r\n");
        string restored = MakeRestoredSource(Normalize(oldRaw)); // verify reversible first
        BackupSourceAndScene(sourcePath, "remove");
        if (player == null) DetectPlayer();
        if (player != null)
        {
            MS7B3VisualHub hub = player.GetComponent<MS7B3VisualHub>();
            if (hub != null) Undo.DestroyObjectImmediate(hub);
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            EditorSceneManager.SaveScene(player.gameObject.scene);
        }
        if (restored != Normalize(oldRaw))
        {
            File.WriteAllText(Absolute(sourcePath), Unnormalize(restored, crlf), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(sourcePath, ImportAssetOptions.ForceUpdate);
        }
        Debug.Log("[7B.3 REMOVE] All 7B.3 hooks removed. 7B.1, 7B.2, original Frost Shot remain. Leave 7B.3 files until recompilation completes.");
    }

    private static void Audit()
    {
        string source;
        try { source = Normalize(File.ReadAllText(Absolute(SourcePath()))); }
        catch (Exception e) { Debug.LogError("[7B.3 AUDIT] Source unavailable: " + e.Message); return; }
        MS7B3VisualHub[] hubs = FindObjectsByType<MS7B3VisualHub>(FindObjectsSortMode.None);
        bool four = source.Contains("MS7B3VisualBridge.AfterFire(") &&
                    source.Contains("MS7B3VisualBridge.AfterIce(") &&
                    source.Contains("MS7B3VisualBridge.AfterLightningPrimary(") &&
                    source.Contains("MS7B3VisualBridge.AfterLightningJump(");
        bool guard = source.Contains("hasLegacySlow");
        bool audio = hubs.Length == 1 && hubs[0].fireHit != null && hubs[0].iceHit != null && hubs[0].lightningHit != null;
        string summary = "[7B.3 AUDIT] " + (four && guard && hubs.Length == 1 && audio ? "PASS" : "CHECK") +
            " | 4 hooks=" + four + " | legacy Frost safety=" + guard +
            " | scene hubs=" + hubs.Length + " | 3 audio links=" + audio +
            " | VFX: fire/frost/arc + impact rings. Read only. Gameplay Play QA still required.";
        if (four && guard && hubs.Length == 1 && audio) Debug.Log(summary);
        else Debug.LogWarning(summary);
    }
}
