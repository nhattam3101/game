#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>7F.4 safe, opt-in Scene installer and read-only QA.</summary>
public sealed class MS7F4MapBalanceInstaller : EditorWindow
{
    private readonly string[] manualTests =
    {
        "Each real map uses its actual Scene name or an explicitly assigned map ID",
        "Wave2 Twin Strike shows the selected map's warning/damage/intensity",
        "Wave3 Crossfire shows the selected map's warning/damage/intensity",
        "Wave4 Hollow Ring retains a clear safe centre and dodge path",
        "Wave5 Boss stays unchanged (including HP-follow UI; Shadow Titan base 500 HP)",
        "On Wave break / boss entry: all 11 7F.2 settings return to baseline",
        "Disable 7F.4 during gameplay: 7F.2 continues with its original settings",
        "7F.2 still suspends and correctly restores 7F.1 when it is disabled",
        "Pause freezes existing warnings/impacts; Game Over and Retry work",
        "Enemy/Elite 7E, Rewards, Equipment 7D, Skill 7C remain correct",
        "Save/Profile, victory banking, defeat and Gold unaffected",
        "1080p / 720p: 7F.3 VFX readable and HUD uncluttered"
    };

    [SerializeField] private bool[] confirmed = new bool[12];
    private Vector2 scroll;
    private string lastAudit = "Run 1 - AUDIT SCENE before installing 7F.4.";

    [MenuItem("Tools/MonsterSurvival/7F Arena Events/13 - Open 7F.4 Map Balance")]
    private static void Open()
    {
        MS7F4MapBalanceInstaller window = GetWindow<MS7F4MapBalanceInstaller>(false, "7F.4 Map Balance");
        window.minSize = new Vector2(600f, 570f);
        window.Show();
    }

    private void OnGUI()
    {
        if (confirmed == null || confirmed.Length != manualTests.Length)
            confirmed = new bool[manualTests.Length];
        scroll = EditorGUILayout.BeginScrollView(scroll);
        GUILayout.Label("7F.4 - MAP-SPECIFIC ARENA BALANCE", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Add-on ONLY: no 7F.1/7F.2/7F.3 scripts replaced.\n" +
            "Map by Scene name, or explicit Map ID in Inspector (single-scene projects).\n" +
            "The existing 7F.2 event-type order stays Twin / Crossfire / Ring.\n" +
            "Install defaults OFF. Wave5 Boss remains excluded.", MessageType.Info);
        if (GUILayout.Button("1 - AUDIT SCENE / MAP PROFILE (READ ONLY)", GUILayout.Height(29)))
        { lastAudit = Audit(); Debug.Log(lastAudit); }
        if (GUILayout.Button("2 - BACKUP SCENE + INSTALL / REPAIR (OFF)", GUILayout.Height(30))) Install();
        if (GUILayout.Button("3 - BACKUP SCENE + ENABLE 7F.4", GUILayout.Height(30))) ChangeEnabled(true);
        if (GUILayout.Button("4 - BACKUP SCENE + DISABLE 7F.4", GUILayout.Height(28))) ChangeEnabled(false);
        if (GUILayout.Button("5 - PURE SAFETY / MAP MATCH TESTS", GUILayout.Height(28)))
        { lastAudit = PureTests(); Debug.Log(lastAudit); }
        if (GUILayout.Button("6 - EXPORT 7F.4 QA TXT (BEST IN PLAY MODE)", GUILayout.Height(30))) Export();
        GUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "The installer seeds three EDITABLE profile examples: SampleScene, Map_Forest_DEMO, Map_Ruins_DEMO. " +
            "Rename them to your ACTUAL scene/map IDs. No unknown map is assigned a fake profile.\n" +
            "For one gameplay Scene with multiple maps, set Map ID Override to the real selected map ID yourself; this version does NOT auto-read map-selection Save data.",
            MessageType.Warning);
        GUILayout.Label("MANUAL QA — tick only after actual tests", EditorStyles.boldLabel);
        for (int i = 0; i < manualTests.Length; i++)
            confirmed[i] = EditorGUILayout.ToggleLeft((i + 1).ToString("00") + "  " + manualTests[i], confirmed[i]);
        EditorGUILayout.LabelField("Confirmed", CountConfirmed() + "/" + manualTests.Length);
        GUILayout.Space(8);
        GUILayout.Label("Latest audit", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(lastAudit, GUILayout.MinHeight(180));
        EditorGUILayout.EndScrollView();
    }

    private static WaveManager FindWave(out string issue)
    {
        Scene active = SceneManager.GetActiveScene();
        int count = 0;
        WaveManager selected = null;
        foreach (WaveManager w in UnityEngine.Object.FindObjectsByType<WaveManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (w == null || w.gameObject.scene != active) continue;
            count++;
            selected = w;
        }
        issue = count == 1 ? "Existing WaveManager=" + selected.name : "Expected exactly one WaveManager in scene, found " + count;
        return count == 1 ? selected : null;
    }

    private static bool CanEdit(out WaveManager wave)
    {
        wave = null;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("7F.4", "Leave Play Mode to install/change persistent Scene settings. Export QA while playing.", "OK");
            return false;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
        {
            EditorUtility.DisplayDialog("7F.4", "Save SampleScene before proceeding.", "OK");
            return false;
        }
        string why;
        wave = FindWave(out why);
        if (wave == null || wave.enemySpawner == null)
        {
            EditorUtility.DisplayDialog("7F.4", wave == null ? why : "Existing WaveManager.enemySpawner is missing", "OK");
            return false;
        }
        MS7F2ArenaEventDirector old = wave.GetComponent<MS7F2ArenaEventDirector>();
        if (old == null || old.waveManager != wave || old.enemySpawner != wave.enemySpawner)
        {
            EditorUtility.DisplayDialog("7F.4", "7F.2 is absent or references do not match WaveManager. Repair 7F.2 first.", "OK");
            return false;
        }
        return true;
    }

    private static bool BackupScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!EditorSceneManager.SaveScene(scene))
        {
            Debug.LogError("[7F.4] Scene save failed; no changes made.");
            return false;
        }
        if (!AssetDatabase.IsValidFolder("Assets/MS_Backups")) AssetDatabase.CreateFolder("Assets", "MS_Backups");
        if (!AssetDatabase.IsValidFolder("Assets/MS_Backups/7F4")) AssetDatabase.CreateFolder("Assets/MS_Backups", "7F4");
        string destination = AssetDatabase.GenerateUniqueAssetPath("Assets/MS_Backups/7F4/" +
            Path.GetFileNameWithoutExtension(scene.path) + "_7F4_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity");
        if (!AssetDatabase.CopyAsset(scene.path, destination))
        {
            Debug.LogError("[7F.4] Backup FAILED; no changes made.");
            return false;
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[7F.4] Scene backup: " + destination);
        return true;
    }

    private void Install()
    {
        WaveManager wave;
        if (!CanEdit(out wave)) return;
        if (!EditorUtility.DisplayDialog("7F.4 INSTALL", "Save a Scene backup and add the Map Balance component to the EXISTING WaveManager?\n\n7F.4 stays OFF; 7F.1/7F.2/7F.3 source scripts remain unchanged.", "Install", "Cancel")) return;
        if (!BackupScene()) return;
        MS7F4MapBalanceDirector comp = wave.GetComponent<MS7F4MapBalanceDirector>();
        bool newlyAdded = comp == null;
        if (newlyAdded) comp = Undo.AddComponent<MS7F4MapBalanceDirector>(wave.gameObject);
        Undo.RecordObject(comp, "7F.4 setup");
        comp.waveManager = wave;
        comp.arena7F2 = wave.GetComponent<MS7F2ArenaEventDirector>();
        if (newlyAdded)
        {
            comp.enableMapBalancing = false;
            comp.SeedDemoProfiles();
        }
        EditorUtility.SetDirty(comp);
        EditorSceneManager.MarkSceneDirty(wave.gameObject.scene);
        EditorSceneManager.SaveScene(wave.gameObject.scene);
        Selection.activeObject = wave.gameObject;
        lastAudit = Audit();
        Debug.Log("[7F.4] " + (newlyAdded ? "Installed OFF" : "Repaired references without resetting existing profiles"));
    }

    private void ChangeEnabled(bool target)
    {
        WaveManager wave;
        if (!CanEdit(out wave)) return;
        MS7F4MapBalanceDirector comp = wave.GetComponent<MS7F4MapBalanceDirector>();
        if (comp == null)
        {
            EditorUtility.DisplayDialog("7F.4", "Install 7F.4 first", "OK");
            return;
        }
        MS7F2ArenaEventDirector old = wave.GetComponent<MS7F2ArenaEventDirector>();
        if (target && !old.enableMultipleEvents)
        {
            EditorUtility.DisplayDialog("7F.4", "7F.2 Multiple Events must be enabled first. 7F.4 does not change the 7F.2 opt-in.", "OK");
            return;
        }
        bool matching = false;
        string effectiveId = string.IsNullOrWhiteSpace(comp.mapIdOverride) ? SceneManager.GetActiveScene().name : comp.mapIdOverride.Trim();
        if (comp.mapProfiles != null)
            foreach (MS7F4MapBalanceDirector.MapProfile p in comp.mapProfiles)
                if (p != null && p.enabled && MS7F4MapBalanceDirector.SameMap(p.mapId, effectiveId) && p.waves != null && p.waves.Count > 0)
                    matching = true;
        if (target && !matching)
        {
            EditorUtility.DisplayDialog("7F.4", "No enabled Map Profile matching '" + effectiveId + "'. Edit Map ID Override or Map Profiles before enabling.", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("7F.4", (target ? "ENABLE" : "DISABLE") + " 7F.4 map-specific modifiers?\nThis changes only fields on 7F.2 during eligible Waves, and restores them on exit. Make backup first?", target ? "Enable" : "Disable", "Cancel")) return;
        if (!BackupScene()) return;
        Undo.RecordObject(comp, "7F.4 enable map modifiers");
        comp.enableMapBalancing = target;
        EditorUtility.SetDirty(comp);
        EditorSceneManager.MarkSceneDirty(wave.gameObject.scene);
        EditorSceneManager.SaveScene(wave.gameObject.scene);
        lastAudit = Audit();
    }

    private static string Audit()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("MONSTERSURVIVAL 7F.4 MAP BALANCE AUDIT — READ ONLY");
        sb.AppendLine("Unity=" + Application.unityVersion + " Scene=" + SceneManager.GetActiveScene().name + " PlayMode=" + EditorApplication.isPlaying);
        string why;
        WaveManager wave = FindWave(out why);
        if (wave == null) { sb.AppendLine("[ERROR] " + why); return sb.ToString(); }
        sb.AppendLine("[PASS] " + why);
        MS7F2ArenaEventDirector old = wave.GetComponent<MS7F2ArenaEventDirector>();
        if (old == null) { sb.AppendLine("[ERROR] Missing 7F.2"); return sb.ToString(); }
        sb.AppendLine(old.waveManager == wave && old.enemySpawner == wave.enemySpawner && wave.enemySpawner != null ?
            "[PASS] Existing 7F.2 WaveManager / EnemySpawner references" : "[ERROR] Broken 7F.2 references");
        MS7F4MapBalanceDirector comp = wave.GetComponent<MS7F4MapBalanceDirector>();
        if (comp == null) { sb.AppendLine("[PENDING] Install 7F.4 on existing WaveManager"); return sb.ToString(); }
        sb.AppendLine(comp.waveManager == wave && comp.arena7F2 == old ? "[PASS] 7F.4 references exact existing Wave / 7F.2" : "[ERROR] 7F.4 mismatched references");
        sb.AppendLine("[INFO] 7F.2 OptIn=" + old.enableMultipleEvents + " | 7F.4 OptIn=" + comp.enableMapBalancing + " | BossWave=" + wave.bossWave);
        sb.AppendLine("[INFO] EffectiveMapId=" + comp.ActiveMapId + " ProfileCount=" + (comp.mapProfiles == null ? 0 : comp.mapProfiles.Count));
        int matching = 0, duplicate = 0;
        if (comp.mapProfiles != null)
            for (int i = 0; i < comp.mapProfiles.Count; ++i)
            {
                MS7F4MapBalanceDirector.MapProfile p = comp.mapProfiles[i];
                if (p == null) continue;
                if (p.enabled && MS7F4MapBalanceDirector.SameMap(p.mapId, comp.ActiveMapId)) matching++;
                for (int j = 0; j < i; ++j)
                {
                    MS7F4MapBalanceDirector.MapProfile other = comp.mapProfiles[j];
                    if (other != null && p.enabled && other.enabled && MS7F4MapBalanceDirector.SameMap(p.mapId, other.mapId)) duplicate++;
                }
                sb.AppendLine("[PROFILE] " + p.mapId + " enabled=" + p.enabled + " waveRules=" + (p.waves == null ? 0 : p.waves.Count));
            }
        sb.AppendLine(matching > 0 ? "[PASS] Matching active map profile" : "[WARN] No profile for this map; baseline unchanged");
        sb.AppendLine(duplicate == 0 ? "[PASS] No duplicate enabled map names" : "[WARN] Duplicate enabled map IDs=" + duplicate);
        sb.AppendLine("[INFO] Existing event types remain 7F.2 default: W2=" + MS7F2ArenaEventDirector.ForWave(2, old.firstAffectedWave) +
            " W3=" + MS7F2ArenaEventDirector.ForWave(3, old.firstAffectedWave) +
            " W4=" + MS7F2ArenaEventDirector.ForWave(4, old.firstAffectedWave));
        sb.AppendLine("[INFO] Legacy7F1Suspensions=" + old.LegacySuspensions + " Legacy7F1Restores=" + old.LegacyRestores +
            " (restoration must be manually tested if applicable)");
        if (EditorApplication.isPlaying) sb.AppendLine(comp.GetRuntimeReport());
        else sb.AppendLine("[PENDING] Edit Mode cannot prove per-wave application or restoration; export inside Play Mode");
        return sb.ToString();
    }

    private static void Test(StringBuilder sb, ref int pass, ref int fail, bool result, string name)
    {
        if (result) { pass++; sb.AppendLine("[PASS] " + name); }
        else { fail++; sb.AppendLine("[FAIL] " + name); }
    }

    private static string PureTests()
    {
        int pass = 0, fail = 0;
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("7F.4 PURE SAFETY / MAP MATCH TESTS (NO SCENE MODIFICATIONS)");
        Test(sb, ref pass, ref fail, MS7F4MapBalanceDirector.SameMap("SampleScene", " samplescene "), "Map ID ignores case and trim");
        Test(sb, ref pass, ref fail, !MS7F4MapBalanceDirector.SameMap("Map_A", "Map_B"), "Different maps remain isolated");
        Test(sb, ref pass, ref fail, !MS7F4MapBalanceDirector.SameMap("", "SampleScene"), "Empty map cannot match");
        Test(sb, ref pass, ref fail, !MS7F4MapBalanceDirector.WaveEligible(true, true, 5, 2, 5, 5, true, false), "Boss Wave 5 protected");
        Test(sb, ref pass, ref fail, !MS7F4MapBalanceDirector.WaveEligible(true, true, 1, 2, 5, 5, true, false), "Wave1 excluded");
        Test(sb, ref pass, ref fail, MS7F4MapBalanceDirector.WaveEligible(true, true, 2, 2, 5, 5, true, false), "Wave2 eligible");
        Test(sb, ref pass, ref fail, !MS7F4MapBalanceDirector.WaveEligible(true, true, 2, 2, 5, 5, false, false), "Wave break excluded");
        Test(sb, ref pass, ref fail, !MS7F4MapBalanceDirector.WaveEligible(true, false, 2, 2, 5, 5, true, false), "7F.2 disabled blocks tuning");
        Test(sb, ref pass, ref fail, !MS7F4MapBalanceDirector.WaveEligible(true, true, 2, 2, 5, 5, true, true), "Game Over excluded");
        Test(sb, ref pass, ref fail, Mathf.Approximately(MS7F4MapBalanceDirector.SafeDamage(6f, 2f, 8f), 8f), "Damage capped to 8");
        Test(sb, ref pass, ref fail, Mathf.Approximately(MS7F4MapBalanceDirector.SafeDamage(6f, 0.5f, 8f), 3f), "Damage scaling downward");
        Test(sb, ref pass, ref fail, MS7F4MapBalanceDirector.SafeWarning(0.3f, 0.65f, 0.9f) >= 0.9f, "Warning never shorter than 0.9s");
        Test(sb, ref pass, ref fail, MS7F2ArenaEventDirector.ForWave(2, 2) == MS7F2ArenaEvent.EventKind.TwinStrike, "Original Wave2 TwinStrike unchanged");
        Test(sb, ref pass, ref fail, MS7F2ArenaEventDirector.ForWave(3, 2) == MS7F2ArenaEvent.EventKind.Crossfire, "Original Wave3 Crossfire unchanged");
        Test(sb, ref pass, ref fail, MS7F2ArenaEventDirector.ForWave(4, 2) == MS7F2ArenaEvent.EventKind.HollowRing, "Original Wave4 HollowRing unchanged");
        sb.AppendLine("PURE RESULT PASS=" + pass + " FAIL=" + fail);
        return sb.ToString();
    }

    private int CountConfirmed()
    {
        int count = 0;
        if (confirmed != null) foreach (bool value in confirmed) if (value) count++;
        return count;
    }

    private void Export()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("MONSTERSURVIVAL 7F.4 MAP-SPECIFIC EVENT BALANCE QA");
        sb.AppendLine("Generated=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " Unity=" + Application.unityVersion +
            " PlayMode=" + EditorApplication.isPlaying);
        sb.AppendLine(Audit());
        sb.AppendLine(PureTests());
        sb.AppendLine("--- MANUAL QA ---");
        for (int i = 0; i < manualTests.Length; ++i)
            sb.AppendLine((confirmed[i] ? "[CONFIRMED] " : "[NOT CONFIRMED] ") + (i + 1).ToString("00") + " | " + manualTests[i]);
        sb.AppendLine("MANUAL=" + CountConfirmed() + "/" + manualTests.Length);
        sb.AppendLine("--- RELEASE GATE ---");
        sb.AppendLine(!EditorApplication.isPlaying ? "RUNTIME=PENDING (export again while playing)" : "RUNTIME=REVIEW TRACE ABOVE");
        sb.AppendLine(CountConfirmed() == manualTests.Length ? "MANUAL=CONFIRMED BY USER" : "MANUAL=PENDING");
        sb.AppendLine("RESULT=PENDING USER REVIEW: live event visibility, boss behavior and persistence are never automatically certified");
        string root = Directory.GetParent(Application.dataPath).FullName;
        string folder = Path.Combine(root, "MS_QAReports", "7F4");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "MS7F4_MapBalanceQA_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        lastAudit = sb.ToString();
        Debug.Log("[7F.4] QA exported to: " + path);
        EditorUtility.RevealInFinder(path);
    }
}
#endif
