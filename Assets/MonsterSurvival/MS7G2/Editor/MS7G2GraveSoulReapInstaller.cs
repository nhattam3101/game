#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Safe, additive 7G.2 installer. All mutations/dialogs run OUTSIDE IMGUI layout.
public sealed class MS7G2GraveSoulReapInstaller : EditorWindow
{
    [SerializeField] private GameObject graveWardenPrefab;
    [SerializeField] private Vector2 scroll;
    private string lastResult = "Select your existing Grave Warden prefab and click Audit.";
    private bool queued;

    [MenuItem("Tools/MonsterSurvival/7G Boss Skills/2 - Open 7G.2 Grave Soul Reap")]
    public static void Open()
    {
        var window = GetWindow<MS7G2GraveSoulReapInstaller>(false, "7G.2 Grave Soul Reap");
        window.minSize = new Vector2(650f, 520f);
        window.Show();
    }

    private void OnEnable() { if (graveWardenPrefab == null) graveWardenPrefab = FindPrefab(); }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        try
        {
            EditorGUILayout.LabelField("7G.2 - GRAVE WARDEN: SOUL REAP ARC", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Additive Phase 2 sector attack. Does not edit the old boss AI, skills, HP bar, WaveManager, Save, or Profile. " +
                "OFF after first install. Prefab backup is mandatory before any change.", MessageType.Info);
            graveWardenPrefab = (GameObject)EditorGUILayout.ObjectField(
                "Existing Grave Warden Prefab", graveWardenPrefab, typeof(GameObject), false);
            EditorGUI.BeginDisabledGroup(queued);
            if (GUILayout.Button("1 - AUDIT EXISTING BOSS PREFAB (READ ONLY)", GUILayout.Height(32)))
                Schedule(() => { lastResult = Audit(); Debug.Log(lastResult); });
            if (GUILayout.Button("2 - BACKUP + INSTALL ADDITIVE SKILL (OFF)", GUILayout.Height(32)))
                Schedule(() => Mutate("INSTALL_OFF"));
            if (GUILayout.Button("3 - BACKUP + ENABLE SOUL REAP", GUILayout.Height(32)))
                Schedule(() => Mutate("ENABLE"));
            if (GUILayout.Button("4 - BACKUP + DISABLE SOUL REAP", GUILayout.Height(30)))
                Schedule(() => Mutate("DISABLE"));
            if (GUILayout.Button("5 - GEOMETRY / SAFETY RULE TESTS", GUILayout.Height(30)))
                Schedule(() => { lastResult = PureTests(); Debug.Log(lastResult); });
            if (GUILayout.Button("6 - EXPORT LIVE QA TXT (in Play Mode)", GUILayout.Height(30)))
                Schedule(Export);
            if (GUILayout.Button("7 - BACKUP + REMOVE ONLY 7G.2 COMPONENT", GUILayout.Height(30)))
                Schedule(() => Mutate("REMOVE"));
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.HelpBox(
                "Test on Wave 5 with a real Grave Warden in Phase 2; export BEFORE stopping Play Mode. " +
                "No automated counter proves visual readability, Boss HP bar, or Save/Profile correctness.", MessageType.Warning);
            GUILayout.Label("Latest audit / test report", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(lastResult, GUILayout.MinHeight(250));
        }
        finally { EditorGUILayout.EndScrollView(); }
    }

    private void Schedule(Action action)
    {
        if (queued) return;
        queued = true;
        EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            try { action(); }
            catch (Exception ex) { lastResult = "[ERROR] " + ex; Debug.LogException(ex); }
            finally { queued = false; if (this != null) Repaint(); }
        };
    }

    private static GameObject FindPrefab()
    {
        // Only auto-select when there is exactly ONE valid candidate.
        // Multiple variants require the user to select the real prefab explicitly.
        GameObject unique = null;
        int count = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("/MS_Backups/") || path.Contains("/Backups/")) continue;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null && prefab.GetComponent<GraveWardenBossAI>() != null)
            {
                unique = prefab;
                count++;
            }
        }
        return count == 1 ? unique : null;
    }

    private bool Validate(out string path, out string message)
    {
        path = "";
        message = "";
        if (graveWardenPrefab == null) graveWardenPrefab = FindPrefab();
        if (graveWardenPrefab == null)
        {
            message = "No unique Grave Warden prefab found (possibly multiple variants). Drag the REAL boss prefab asset from Project, not a Scene object.";
            return false;
        }
        path = AssetDatabase.GetAssetPath(graveWardenPrefab);
        if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/") || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
            !PrefabUtility.IsPartOfPrefabAsset(graveWardenPrefab))
        {
            message = "Please select a prefab ASSET (not Scene instance)";
            return false;
        }
        if (graveWardenPrefab.GetComponent<GraveWardenBossAI>() == null ||
            graveWardenPrefab.GetComponent<Health>() == null)
        {
            message = "Selected prefab is NOT the correct Grave Warden root (requires GraveWardenBossAI and Health on the root).";
            return false;
        }
        message = "Verified the original Grave Warden AI and Health on " + path;
        return true;
    }

    private string Audit()
    {
        var s = new StringBuilder();
        s.AppendLine("MONSTERSURVIVAL 7G.2 - GRAVE SOUL REAP / PREFAB AUDIT (READ ONLY)");
        s.AppendLine("Unity=" + Application.unityVersion + " Scene=" + SceneManager.GetActiveScene().name +
                     " PlayMode=" + EditorApplication.isPlaying);
        string path, issue;
        if (!Validate(out path, out issue)) { s.AppendLine("[ERROR] " + issue); return s.ToString(); }
        s.AppendLine("[PASS] " + issue);
        s.AppendLine("[INFO] Grave Warden original maxHealth=" + graveWardenPrefab.GetComponent<Health>().maxHealth + " (never modified by 7G.2)");
        var attack = graveWardenPrefab.GetComponent<MS7G2GraveSoulReap>();
        if (attack == null) s.AppendLine("[PENDING] Additive 7G.2 component not installed");
        else
        {
            s.AppendLine("[PASS] Exactly one 7G.2 component on prefab (DisallowMultipleComponent)");
            s.AppendLine("[INFO] EnableSoulReap=" + attack.enableSoulReap + " PhaseTwoOnly=" + attack.phaseTwoOnly +
                         " Warn=" + attack.warningDuration + " Damage=" + attack.damage + " Cooldown=" + attack.cooldown);
        }
        s.AppendLine("[INFO] Old Summon/TombWall/SpiritOrbit/Reanimate scripts are NOT edited by installer.");
        s.AppendLine("[INFO] Shadow Titan 500 HP, follow-head bars, Save/Profile and 7F are not edited.");
        return s.ToString();
    }

    private static string Backup(string assetPath)
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string source = Path.GetFullPath(Path.Combine(root, assetPath));
        if (!File.Exists(source)) throw new FileNotFoundException("Existing prefab source missing", source);
        string folder = Path.Combine(root, "MS_Backups", "7G2");
        Directory.CreateDirectory(folder);
        string backup = Path.Combine(folder, Path.GetFileNameWithoutExtension(assetPath) +
            "_BEFORE_7G2_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".prefab");
        File.Copy(source, backup, false);
        return backup;
    }

    private void Mutate(string action)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("7G.2", "Stop Play Mode before editing the prefab asset.", "OK");
            return;
        }
        string path, issue;
        if (!Validate(out path, out issue))
        {
            EditorUtility.DisplayDialog("7G.2 - Invalid Prefab", issue, "OK");
            lastResult = "[ERROR] " + issue;
            return;
        }
        if (!EditorUtility.DisplayDialog("7G.2 - " + action,
            "Make a full file backup of the EXISTING Grave Warden prefab, then " + action +
            " ONLY the additive 7G.2 component?\n\nOld AI, VFX, boss bar, Save and WaveManager will not be edited.",
            "Continue", "Cancel")) return;

        string backup = Backup(path);
        GameObject root = null;
        try
        {
            root = PrefabUtility.LoadPrefabContents(path);
            if (root == null || root.GetComponent<GraveWardenBossAI>() == null)
                throw new InvalidOperationException("Prefab contents do not contain the expected boss AI on the root.");
            MS7G2GraveSoulReap skill = root.GetComponent<MS7G2GraveSoulReap>();
            if (action == "REMOVE")
            {
                if (skill != null) DestroyImmediate(skill);
            }
            else
            {
                if (skill == null) skill = root.AddComponent<MS7G2GraveSoulReap>();
                if (action == "INSTALL_OFF") skill.enableSoulReap = false;
                else skill.enableSoulReap = action == "ENABLE";
            }
            if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                throw new IOException("Could not save modified Grave Warden prefab.");
        }
        finally { if (root != null) PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        graveWardenPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        lastResult = "[OK] " + action + "\nPrefab=" + path + "\nBackup=" + backup + "\n" + Audit();
        Debug.Log(lastResult);
        EditorUtility.DisplayDialog("7G.2 updated", "Prefab backed up and action completed. See Console for backup location.", "OK");
    }

    private static string PureTests()
    {
        int pass = 0, fail = 0;
        var s = new StringBuilder("MONSTERSURVIVAL 7G.2 - PURE SECTOR GEOMETRY TESTS\n");
        Action<string, bool> check = (label, ok) =>
        {
            if (ok) pass++; else fail++;
            s.AppendLine((ok ? "[PASS] " : "[FAIL] ") + label);
        };
        Vector2 center = Vector2.zero, east = Vector2.right;
        check("middle sector danger", MS7G2GraveSoulReap.IsInsideSector(new Vector2(3, 0), center, east, 1.35f, 4.3f, 48f));
        check("inner sanctuary safe", !MS7G2GraveSoulReap.IsInsideSector(new Vector2(.7f, 0), center, east, 1.35f, 4.3f, 48f));
        check("outside outer radius safe", !MS7G2GraveSoulReap.IsInsideSector(new Vector2(5, 0), center, east, 1.35f, 4.3f, 48f));
        check("outside angle safe", !MS7G2GraveSoulReap.IsInsideSector(new Vector2(0, 3), center, east, 1.35f, 4.3f, 48f));
        check("behind boss safe", !MS7G2GraveSoulReap.IsInsideSector(new Vector2(-3, 0), center, east, 1.35f, 4.3f, 48f));
        check("outer boundary included", MS7G2GraveSoulReap.IsInsideSector(new Vector2(4.3f, 0), center, east, 1.35f, 4.3f, 48f));
        check("diagonal in sector", MS7G2GraveSoulReap.IsInsideSector(new Vector2(2f, 1f), center, east, 1.35f, 4.3f, 48f));
        s.AppendLine("PURE RESULT PASS=" + pass + " FAIL=" + fail);
        s.AppendLine("[INFO] These are static rules, NOT proof of live Unity damage / visibility / Save.");
        return s.ToString();
    }

    private void Export()
    {
        var s = new StringBuilder();
        s.AppendLine("MONSTERSURVIVAL 7G.2 - GRAVE WARDEN FINAL QA (READ ONLY)");
        s.AppendLine("Generated=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " Unity=" + Application.unityVersion);
        s.AppendLine(Audit());
        var live = UnityEngine.Object.FindObjectsByType<MS7G2GraveSoulReap>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        int count = 0;
        foreach (var skill in live)
        {
            if (skill == null || skill.gameObject.scene != SceneManager.GetActiveScene() ||
                !skill.gameObject.scene.IsValid()) continue;
            count++;
            s.AppendLine(skill.BuildLiveReport());
        }
        s.AppendLine("[LIVE] SceneInstances=" + count);
        if (!EditorApplication.isPlaying) s.AppendLine("[PENDING] Must export in Play Mode during Wave 5 Grave Warden.");
        else if (count < 1) s.AppendLine("[PENDING] No live Grave Warden 7G.2 scene instance seen; use a match that spawns Grave Warden.");
        else s.AppendLine("[OBSERVED] Live instance found; counts above may still be zero until Phase 2.");
        s.AppendLine("=== MANUAL QA (user confirmation needed) ===");
        s.AppendLine("[ ] Phase2 displays the purple->gold arc; Player can dodge at sides and near boss.");
        s.AppendLine("[ ] Old Summons/TombWall/Orbit/Reanimate continue normally and cannot overlap impact.");
        s.AppendLine("[ ] Inside arc invokes one Health/Shield check; outside/beyond wall avoids hit.");
        s.AppendLine("[ ] Pause stops warning and cooldown, then resumes correctly.");
        s.AppendLine("[ ] Grave Warden purple-gold follow-head HP bar unchanged; Shadow Titan still 500 HP.");
        s.AppendLine("[ ] Victory/GameOver/Retry and Boss variants unaffected.");
        s.AppendLine("[ ] Save/Profile, Equipment 7D, Skills 7C, Elite 7E, Arena 7F unaffected.");
        s.AppendLine("MANUAL=PENDING - EVENT COUNTERS ARE NOT PIXEL/WALLET PROOF");
        s.AppendLine("RESULT=PENDING UNTIL LIVE CAST + MANUAL QA");
        string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string folder = Path.Combine(project, "MS_QAReports", "7G2");
        Directory.CreateDirectory(folder);
        string output = Path.Combine(folder, "MS7G2_GraveSoulReapQA_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
        File.WriteAllText(output, s.ToString(), new UTF8Encoding(false));
        lastResult = s.ToString();
        Debug.Log("[7G.2] QA TXT exported: " + output);
        EditorUtility.RevealInFinder(output);
    }
}
#endif
