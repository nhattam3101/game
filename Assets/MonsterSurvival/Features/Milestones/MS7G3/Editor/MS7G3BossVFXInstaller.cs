#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// 7G.3 installer: additive ONLY, backups mandatory, IMGUI actions delayed.
public sealed class MS7G3BossVFXInstaller : EditorWindow
{
    [SerializeField] private GameObject titanPrefab;
    [SerializeField] private GameObject gravePrefab;
    [SerializeField] private Vector2 scroll;
    private string lastReport = "Select the two ORIGINAL boss prefab assets. Audit first.";
    private bool pending;

    [MenuItem("Tools/MonsterSurvival/7G Boss Skills/3 - Open 7G.3 Boss VFX Polish")]
    public static void Open()
    {
        MS7G3BossVFXInstaller w = GetWindow<MS7G3BossVFXInstaller>(false, "7G.3 Boss VFX");
        w.minSize = new Vector2(650, 530);
        w.Show();
    }

    private void OnEnable()
    {
        if (titanPrefab == null) titanPrefab = FindUnique(true);
        if (gravePrefab == null) gravePrefab = FindUnique(false);
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        try
        {
            GUILayout.Label("7G.3 - SHADOW TITAN + GRAVE WARDEN: VISUAL POLISH", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Adds glow, moving charge trace, boss energy aura, and impact flash to the existing " +
                "7G.1/7G.2 outline shapes. No edits to attack damage, Health, Boss AI, Animator, HUD or Save. " +
                "Existing skill geometry remains authoritative.", MessageType.Info);
            titanPrefab = (GameObject)EditorGUILayout.ObjectField("Shadow Titan PREFAB", titanPrefab, typeof(GameObject), false);
            gravePrefab = (GameObject)EditorGUILayout.ObjectField("Grave Warden PREFAB", gravePrefab, typeof(GameObject), false);
            EditorGUI.BeginDisabledGroup(pending);
            if (GUILayout.Button("1 - AUDIT BOTH BOSS PREFABS (READ ONLY)", GUILayout.Height(32)))
                Queue(() => { lastReport = Audit(); Debug.Log(lastReport); });
            if (GUILayout.Button("2 - BACKUP + INSTALL VISUALS (OFF)", GUILayout.Height(32)))
                Queue(() => Change(Edit.Install));
            if (GUILayout.Button("3 - BACKUP + ENABLE VISUALS", GUILayout.Height(32)))
                Queue(() => Change(Edit.Enable));
            if (GUILayout.Button("4 - BACKUP + DISABLE VISUALS", GUILayout.Height(30)))
                Queue(() => Change(Edit.Disable));
            if (GUILayout.Button("5 - PURE VISUAL MATH / SOURCES QA", GUILayout.Height(30)))
                Queue(() => { lastReport = PureTests(); Debug.Log(lastReport); });
            if (GUILayout.Button("6 - EXPORT 7G.3 QA TXT", GUILayout.Height(32)))
                Queue(Export);
            if (GUILayout.Button("7 - BACKUP + REMOVE ONLY 7G.3 COMPONENT", GUILayout.Height(30)))
                Queue(() => Change(Edit.Remove));
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.HelpBox(
                "Stop Play Mode before editing prefab assets. 7G.1/7G.2 skill must already be installed and ENABLED " +
                "to show VFX in a real Phase 2 boss fight. Export BEFORE stopping Play Mode. " +
                "Manual QA still required for actual Game View rendering, visibility, Pause and Save.", MessageType.Warning);
            GUILayout.Label("Latest audit / report", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(lastReport, GUILayout.MinHeight(240));
        }
        finally { EditorGUILayout.EndScrollView(); }
    }

    private void Queue(Action action)
    {
        if (pending) return;
        pending = true;
        EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            try { action(); }
            catch (Exception e) { lastReport = "[ERROR] " + e; Debug.LogException(e); }
            finally { pending = false; if (this != null) Repaint(); }
        };
    }

    private static GameObject FindUnique(bool titan)
    {
        GameObject candidate = null;
        int count = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                path.IndexOf("/Backups/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("/MS_Backups/", StringComparison.OrdinalIgnoreCase) >= 0) continue;
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;
            bool matches = titan ? go.GetComponentInChildren<MS7G1TitanRiftLanes>(true) != null :
                                   go.GetComponentInChildren<MS7G2GraveSoulReap>(true) != null;
            if (!matches) continue;
            candidate = go;
            count++;
            if (count > 1) return null;
        }
        return count == 1 ? candidate : null;
    }

    private bool Valid(GameObject prefab, bool titan, out string path, out string message)
    {
        path = prefab != null ? AssetDatabase.GetAssetPath(prefab) : "";
        string name = titan ? "Shadow Titan" : "Grave Warden";
        if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
            !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
            !PrefabUtility.IsPartOfPrefabAsset(prefab))
        { message = name + ": choose an actual PREFAB asset, not Scene object."; return false; }
        Component source = titan ? (Component)prefab.GetComponentInChildren<MS7G1TitanRiftLanes>(true) :
                                   prefab.GetComponentInChildren<MS7G2GraveSoulReap>(true);
        if (source == null || source.GetComponent<Health>() == null)
        { message = name + ": missing exact 7G.1/7G.2 component and Health on same Boss object."; return false; }
        if (titan && source.GetComponent<BossPhase2Controller>() == null)
        { message = name + ": BossPhase2Controller missing on target."; return false; }
        if (!titan && source.GetComponent<GraveWardenBossAI>() == null)
        { message = name + ": GraveWardenBossAI missing on target."; return false; }
        message = name + ": valid skill prefab " + path;
        return true;
    }

    private static MS7G3BossSkillVFX FindEffect(GameObject prefab, bool titan)
    {
        Component skill = titan ? (Component)prefab.GetComponentInChildren<MS7G1TitanRiftLanes>(true) :
                                  prefab.GetComponentInChildren<MS7G2GraveSoulReap>(true);
        return skill != null ? skill.GetComponent<MS7G3BossSkillVFX>() : null;
    }

    private string Audit()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("MONSTERSURVIVAL 7G.3 - BOSS VISUAL POLISH / READ ONLY AUDIT");
        sb.AppendLine("Unity=" + Application.unityVersion + " Scene=" + SceneManager.GetActiveScene().name +
                      " PlayMode=" + EditorApplication.isPlaying);
        for (int n = 0; n < 2; n++)
        {
            bool titan = n == 0;
            GameObject prefab = titan ? titanPrefab : gravePrefab;
            string path, detail;
            if (!Valid(prefab, titan, out path, out detail))
            { sb.AppendLine("[ERROR] " + detail); continue; }
            sb.AppendLine("[PASS] " + detail);
            MS7G3BossSkillVFX vfx = FindEffect(prefab, titan);
            sb.AppendLine(vfx != null ?
                "[PASS] Additive VFX present, enabled=" + vfx.enableEnhancedVFX + " target=" + vfx.targetSkill :
                "[PENDING] 7G.3 visual component not installed");
            if (titan) sb.AppendLine("[INFO] Titan Health.maxHealth=" + prefab.GetComponentInChildren<MS7G1TitanRiftLanes>(true).GetComponent<Health>().maxHealth + " (expected 500 for user's balance)");
            else sb.AppendLine("[INFO] Grave Health.maxHealth=" + prefab.GetComponentInChildren<MS7G2GraveSoulReap>(true).GetComponent<Health>().maxHealth + " (previously 750)");
        }
        if (EditorApplication.isPlaying)
        {
            int liveCount = 0;
            foreach (MS7G3BossSkillVFX vfx in UnityEngine.Object.FindObjectsByType<MS7G3BossSkillVFX>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (vfx == null || vfx.gameObject.scene != SceneManager.GetActiveScene()) continue;
                if (!vfx.gameObject.activeInHierarchy) continue;
                liveCount++;
                sb.AppendLine("--- LIVE BOSS " + liveCount + " ---");
                sb.Append(vfx.RuntimeReport());
            }
            sb.AppendLine("[LIVE] Active Boss instances with 7G.3=" + liveCount);
        }
        else sb.AppendLine("[PENDING] Live boss observations require Play Mode and Phase 2.");
        sb.AppendLine("[MANUAL] Boss warning colors/impact flash readable; no animation overlap; Pause freezes; boss bar follows; old attacks and Save/Profile work.");
        sb.AppendLine("[NOTE] VFX counters are NOT proof of actual pixels, gameplay damage, or persistence.");
        return sb.ToString();
    }

    private enum Edit { Install, Enable, Disable, Remove }

    private static string BackupAsset(string assetPath)
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string source = Path.GetFullPath(Path.Combine(root, assetPath));
        if (!File.Exists(source)) throw new FileNotFoundException("Prefab source not on disk", source);
        string folder = Path.Combine(root, "MS_Backups", "7G3");
        Directory.CreateDirectory(folder);
        string backup = Path.Combine(folder, Path.GetFileNameWithoutExtension(assetPath) +
                        "_BEFORE_7G3_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".prefab");
        File.Copy(source, backup, false);
        return backup;
    }

    private void Change(Edit mode)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorUtility.DisplayDialog("7G.3", "Stop Play Mode before modifying a prefab.", "OK"); return; }
        string p1, p2, m1, m2;
        bool t = Valid(titanPrefab, true, out p1, out m1);
        bool g = Valid(gravePrefab, false, out p2, out m2);
        if (!t && !g)
        { lastReport = "[ERROR] No valid prefab selected.\n" + m1 + "\n" + m2; Debug.LogError(lastReport); return; }
        if (t && g && p1 == p2)
        { lastReport = "[ERROR] Titan and Grave must be distinct prefab assets."; Debug.LogError(lastReport); return; }
        if (!EditorUtility.DisplayDialog("7G.3 - " + mode,
            "Back up " + (t ? "Titan " : "") + (g ? "Grave " : "") + "prefab(s) and " + mode +
            " ONLY the 7G.3 visual component?\nInvalid/missing prefabs will be skipped.\n" +
            "7G.1, 7G.2, AI, Animator, HP, Save and HUD will not be edited.", "Continue", "Cancel")) return;

        StringBuilder sb = new StringBuilder();
        if (t) ModifyOne(p1, true, mode, sb);
        else sb.AppendLine("[SKIP] " + m1);
        if (g) ModifyOne(p2, false, mode, sb);
        else sb.AppendLine("[SKIP] " + m2);
        AssetDatabase.SaveAssets();
        lastReport = sb.ToString() + "\n" + Audit();
        Debug.Log(lastReport);
        EditorUtility.DisplayDialog("7G.3", "Done. See latest report and Console. Verify prefab backups in MS_Backups/7G3.", "OK");
    }

    private static void ModifyOne(string path, bool titan, Edit mode, StringBuilder sb)
    {
        string backup = BackupAsset(path); // fail closed BEFORE mutation
        GameObject root = null;
        try
        {
            root = PrefabUtility.LoadPrefabContents(path);
            Component source = titan ? (Component)root.GetComponentInChildren<MS7G1TitanRiftLanes>(true) :
                                       root.GetComponentInChildren<MS7G2GraveSoulReap>(true);
            if (source == null || source.GetComponent<Health>() == null)
                throw new InvalidOperationException("Source changed after audit; not writing prefab.");
            MS7G3BossSkillVFX vfx = source.GetComponent<MS7G3BossSkillVFX>();
            if (mode == Edit.Remove)
            {
                if (vfx == null) { sb.AppendLine("[INFO] No 7G.3 to remove: " + path); return; }
                DestroyImmediate(vfx);
            }
            else if (mode == Edit.Install)
            {
                if (vfx == null) vfx = source.gameObject.AddComponent<MS7G3BossSkillVFX>();
                vfx.targetSkill = titan ? MS7G3BossSkillVFX.BossSkill.TitanRiftLanes : MS7G3BossSkillVFX.BossSkill.GraveSoulReap;
                vfx.enableEnhancedVFX = false;
            }
            else
            {
                if (vfx == null) throw new InvalidOperationException("7G.3 missing; press INSTALL OFF first for " + path);
                vfx.enableEnhancedVFX = mode == Edit.Enable;
                vfx.targetSkill = titan ? MS7G3BossSkillVFX.BossSkill.TitanRiftLanes : MS7G3BossSkillVFX.BossSkill.GraveSoulReap;
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            sb.AppendLine("[PASS] " + mode + " " + path + "\nBackup=" + backup);
        }
        catch (Exception e)
        {
            sb.AppendLine("[ERROR] " + path + " " + e.Message + " (backup retained)");
            Debug.LogException(e);
        }
        finally { if (root != null) PrefabUtility.UnloadPrefabContents(root); }
    }

    private static string PureTests()
    {
        int pass = 0, fail = 0;
        StringBuilder sb = new StringBuilder("MONSTERSURVIVAL 7G.3 - PURE VISUAL GEOMETRY TESTS\n");
        Check("Trail starts at origin", Mathf.Approximately(MS7G3BossSkillVFX.TrailU(0f, .17f, 0f), 0f), ref pass, ref fail, sb);
        Check("Trail progress monotonic", MS7G3BossSkillVFX.TrailU(.5f, .17f, 1f) > MS7G3BossSkillVFX.TrailU(.5f, .17f, 0f), ref pass, ref fail, sb);
        Check("Trail never exceeds perimeter", MS7G3BossSkillVFX.TrailU(1f, .17f, 1f) <= 1f, ref pass, ref fail, sb);
        Check("Trail fraction clamps at zero", MS7G3BossSkillVFX.TrailU(.4f, -1f, 1f) >= 0f, ref pass, ref fail, sb);
        Check("Trail fraction clamps at half", MS7G3BossSkillVFX.TrailU(.4f, 1f, 1f) <= 1f, ref pass, ref fail, sb);
        Check("Trail early has no wrap", MS7G3BossSkillVFX.TrailU(.95f, .2f, 0f) < MS7G3BossSkillVFX.TrailU(.95f, .2f, 1f), ref pass, ref fail, sb);
        sb.AppendLine("PURE RESULT PASS=" + pass + " FAIL=" + fail + " (not a Unity build or visual proof)");
        return sb.ToString();
    }

    private static void Check(string label, bool ok, ref int pass, ref int fail, StringBuilder sb)
    {
        if (ok) pass++; else fail++;
        sb.AppendLine((ok ? "[PASS] " : "[FAIL] ") + label);
    }

    private void Export()
    {
        string report = Audit() + "\n" + PureTests() +
            "\n=== MANUAL GAMEPLAY QA (NOT AUTOMATICALLY VERIFIED) ===\n" +
            "[ ] Titan: three Rift Lane outlines remain aligned with 7G.1 hit geometry\n" +
            "[ ] Grave: Soul Reap arc shows purple-to-gold warning and central safe radius\n" +
            "[ ] Titan/Grave: glowing trace advances smoothly and impact flash plays once\n" +
            "[ ] 7G.3 OFF leaves the original 7G.1/7G.2 warning/impact unchanged\n" +
            "[ ] Pause/Resume freezes/restarts shimmer and warning countdown\n" +
            "[ ] Boss AI original skills, HP follow-bars and Shadow Titan 500 HP intact\n" +
            "[ ] 7F Arena boss exclusion, Victory/GameOver/Retry unchanged\n" +
            "[ ] Skills 7C, Equipment 7D, Elite 7E, Gold and Save/Profile unchanged\n" +
            "MANUAL=PENDING\nRELEASE=PENDING UNTIL LIVE + MANUAL QA\n";
        string folder = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "MS_QAReports", "7G3");
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, "MS7G3_BossVFXQA_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
        File.WriteAllText(file, report, new UTF8Encoding(false));
        lastReport = report;
        Debug.Log("[7G.3] Exported read-only report: " + file);
        EditorUtility.RevealInFinder(file);
    }
}
#endif
