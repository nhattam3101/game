#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Safe 7E.4 installer: only touches the existing Elite prefab referenced by EliteSpawnDirector.
/// Never changes PlayerProgression, EnemyController, save data, or scenes.
/// </summary>
public sealed class MS7E4EliteRewardsInstaller : EditorWindow
{
    [MenuItem("Tools/MonsterSurvival/7E Elite Enemies/7 - Open 7E.4 Elite Rewards & QA")]
    public static void Open()
    {
        MS7E4EliteRewardsInstaller window = GetWindow<MS7E4EliteRewardsInstaller>("7E.4 Elite Rewards");
        window.minSize = new Vector2(660f, 510f);
    }

    private readonly string[] manualNames =
    {
        "Elite still grants its ORIGINAL base EXP and Gold on death",
        "Bulwark extra reward checked against the displayed preview",
        "Swift extra reward checked against the displayed preview",
        "Regenerator extra reward checked against the displayed preview",
        "Same Elite cannot award extra reward more than once",
        "Wave 5 Boss / normal enemies do NOT receive Elite extra reward",
        "Victory saves run Gold; Game Over/Retry does not incorrectly bank it",
        "Profile slots, Elite 7E.1/7E.3, Dynamic Waves 7E.2, Equipment 7D intact"
    };

    private readonly bool[] manual = new bool[8];
    private Vector2 scroll;
    private string status = "Ready. Start with button 1 in SampleScene.";

    private void OnGUI()
    {
        EditorGUILayout.LabelField("MONSTERSURVIVAL 7E.4 — ELITE REWARDS & BALANCE", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Adds only an EXTRA one-time Elite kill reward. Existing EnemyController base rewards, " +
            "Gold bank, Save/Profile and Bosses are untouched. SAFETY GATE defaults OFF.", MessageType.Info);
        EditorGUILayout.Space(4);
        if (GUILayout.Button("1 — READ-ONLY AUDIT + PREVIEW", GUILayout.Height(30)))
            status = Audit();

        GUI.enabled = !EditorApplication.isPlayingOrWillChangePlaymode;
        if (GUILayout.Button("2 — BACKUP + INSTALL ON EXISTING ELITE PREFAB", GUILayout.Height(30)))
            Install();
        if (GUILayout.Button("3 — BACKUP + ENABLE REAL EXTRA REWARDS (EXPLICIT OPT-IN)", GUILayout.Height(30)))
            SetGate(true);
        if (GUILayout.Button("4 — BACKUP + DISABLE REAL EXTRA REWARDS", GUILayout.Height(30)))
            SetGate(false);
        GUI.enabled = true;
        if (GUILayout.Button("5 — EXPORT PLAY MODE QA TXT", GUILayout.Height(30)))
            Export();

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("MANUAL QA (tick only after actually testing)", EditorStyles.boldLabel);
        for (int i = 0; i < manual.Length; i++)
            manual[i] = EditorGUILayout.ToggleLeft((i + 1).ToString("00") + ". " + manualNames[i], manual[i]);
        EditorGUILayout.Space(6);
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
        EditorGUILayout.TextArea(status, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private static bool TryElitePrefab(out GameObject prefab, out string path, out string reason)
    {
        prefab = null;
        path = string.Empty;
        reason = string.Empty;
        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            reason = "Open an existing loaded Scene first.";
            return false;
        }

        EliteSpawnDirector[] directors = UnityEngine.Object.FindObjectsByType<EliteSpawnDirector>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        int sceneCount = 0;
        EliteSpawnDirector director = null;
        foreach (EliteSpawnDirector candidate in directors)
        {
            if (candidate == null || candidate.gameObject.scene != scene) continue;
            director = candidate;
            sceneCount++;
        }
        if (sceneCount != 1)
        {
            reason = "Expected exactly one EliteSpawnDirector in scene; found " + sceneCount;
            return false;
        }
        if (director.elitePrefab == null)
        {
            reason = "EliteSpawnDirector.elitePrefab is not assigned.";
            return false;
        }

        path = AssetDatabase.GetAssetPath(director.elitePrefab);
        if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
            !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        {
            reason = "Assigned Elite prefab must be a writable .prefab asset in Assets/: " + path;
            return false;
        }
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null || prefab.GetComponent<Health>() == null ||
            prefab.GetComponent<EnemyController>() == null ||
            prefab.GetComponent<EliteEnemy>() == null ||
            prefab.GetComponent<MS7E1EliteAffix>() == null)
        {
            reason = "Elite prefab must contain Health, EnemyController, EliteEnemy and MS7E1EliteAffix.";
            return false;
        }
        reason = "EliteSpawnDirector references EXISTING prefab: " + path;
        return true;
    }

    private static string PreviewLine(MS7E4EliteRewards bonus, string name,
        MS7E1EliteAffix.AffixKind kind)
    {
        int exp;
        int gold;
        bonus.PreviewBonus(kind, out exp, out gold);
        return "[BALANCE] " + name + " = BASE reward from EnemyController + EXTRA " +
               exp + " EXP, " + gold + " Gold (capped; independent of 7D/5B)";
    }

    private string Audit()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("MONSTERSURVIVAL 7E.4 — ELITE REWARDS QA / READ ONLY");
        sb.AppendLine("Unity=" + Application.unityVersion + " | Scene=" +
                      EditorSceneManager.GetActiveScene().name + " | PlayMode=" +
                      EditorApplication.isPlaying + " | Time=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        GameObject prefab;
        string path;
        string reason;
        bool safe = TryElitePrefab(out prefab, out path, out reason);
        if (!safe)
        {
            sb.AppendLine("[WARN] " + reason);
        }
        else
        {
            sb.AppendLine("[PASS] " + reason);
            MS7E4EliteRewards[] bonuses = prefab.GetComponents<MS7E4EliteRewards>();
            if (bonuses.Length != 1)
            {
                sb.AppendLine("[WARN] Expected ONE 7E.4 reward component; found " + bonuses.Length);
            }
            else
            {
                MS7E4EliteRewards bonus = bonuses[0];
                EnemyController enemy = prefab.GetComponent<EnemyController>();
                sb.AppendLine("[PASS] Exactly one MS7E4EliteRewards on existing Elite prefab");
                sb.AppendLine("[PASS] Old EliteEnemy / 7E.1 Affix / EnemyController / Health intact");
                sb.AppendLine("[INFO] Safety gate EnableExtraRewards=" + bonus.ExtraRewardsEnabled);
                sb.AppendLine("[INFO] Original EnemyController rewards: EXP=" + enemy.expReward +
                              ", Gold=" + enemy.coinReward);
                sb.AppendLine(PreviewLine(bonus, "Bulwark", MS7E1EliteAffix.AffixKind.Bulwark));
                sb.AppendLine(PreviewLine(bonus, "Swift", MS7E1EliteAffix.AffixKind.Swift));
                sb.AppendLine(PreviewLine(bonus, "Regenerator", MS7E1EliteAffix.AffixKind.Regenerator));
            }
        }
        sb.AppendLine("=== PURE REWARD MATH QA ===");
        int exp;
        int gold;
        MS7E4EliteRewards.ComputeExtraForBalance(10, 4, 0.7f, 0.5f, 12, 4, out exp, out gold);
        sb.AppendLine(exp == 7 && gold == 2 ? "[PASS] Scaling 10/4 => 7/2" : "[ERROR] Scaling 10/4 mismatch");
        MS7E4EliteRewards.ComputeExtraForBalance(1000, 1000, 2f, 2f, 12, 4, out exp, out gold);
        sb.AppendLine(exp == 12 && gold == 4 ? "[PASS] Hard extra caps 12/4" : "[ERROR] Hard caps mismatch");
        MS7E4EliteRewards.ComputeExtraForBalance(0, 0, 2f, 2f, 12, 4, out exp, out gold);
        sb.AppendLine(exp == 0 && gold == 0 ? "[PASS] Zero base yields zero extra" : "[ERROR] Zero baseline mismatch");
        sb.AppendLine("=== PLAY SESSION EVENT COUNTERS ===");
        sb.AppendLine("DeathEvents=" + MS7E4EliteRewards.DeathEvents +
                      " | GrantedDeaths=" + MS7E4EliteRewards.GrantedDeaths +
                      " | SkippedDisabled=" + MS7E4EliteRewards.SkippedDisabled +
                      " | NoPlayer=" + MS7E4EliteRewards.SkippedNoPlayer +
                      " | AffixNotReady=" + MS7E4EliteRewards.SkippedNotInitialized);
        sb.AppendLine("Extra EXP granted=" + MS7E4EliteRewards.ExtraExpGranted +
                      " | Extra Gold granted=" + MS7E4EliteRewards.ExtraGoldGranted);
        sb.AppendLine("Affix payouts Bulwark=" + MS7E4EliteRewards.BulwarkPayouts +
                      " Swift=" + MS7E4EliteRewards.SwiftPayouts +
                      " Regenerator=" + MS7E4EliteRewards.RegeneratorPayouts);
        sb.AppendLine("=== RECENT EVENTS ===");
        sb.AppendLine(EditorApplication.isPlaying
            ? MS7E4EliteRewards.RecentEvents()
            : "(Edit Mode; start Play Mode and kill Elites to collect runtime evidence.)");
        sb.AppendLine("=== MANUAL QA ===");
        int done = 0;
        for (int i = 0; i < manual.Length; i++)
        {
            if (manual[i]) done++;
            sb.AppendLine((manual[i] ? "[CONFIRMED] " : "[NOT CONFIRMED] ") +
                          (i + 1).ToString("00") + " | " + manualNames[i]);
        }
        sb.AppendLine("MANUAL=" + done + "/" + manual.Length);
        bool runtime = EditorApplication.isPlaying &&
                       MS7E4EliteRewards.GrantedDeaths > 0 &&
                       MS7E4EliteRewards.ExtraExpGranted + MS7E4EliteRewards.ExtraGoldGranted > 0;
        bool allKinds = MS7E4EliteRewards.BulwarkPayouts > 0 &&
                        MS7E4EliteRewards.SwiftPayouts > 0 &&
                        MS7E4EliteRewards.RegeneratorPayouts > 0;
        sb.AppendLine("LIVE_REWARD_EVIDENCE=" + (runtime ? "OBSERVED" : "PENDING"));
        sb.AppendLine("ALL_THREE_AFFIXES_OBSERVED=" + allKinds);
        sb.AppendLine("RESULT=" + (safe && prefab.GetComponents<MS7E4EliteRewards>().Length == 1 &&
                       runtime && allKinds && done == manual.Length ? "READY_FOR_USER_REVIEW" : "PENDING"));
        sb.AppendLine("[NOTE] Counters verify reward method calls, not persistence or combat economy.");
        return sb.ToString();
    }

    private static string BackupExistingPrefab(string path)
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string source = Path.GetFullPath(Path.Combine(projectRoot, path));
        if (!File.Exists(source)) throw new FileNotFoundException("Prefab source not found", source);
        string folder = Path.Combine(projectRoot, "MS_Backups", "7E4");
        Directory.CreateDirectory(folder);
        string backup = Path.Combine(folder, Path.GetFileNameWithoutExtension(path) +
                        "_before_7E4_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".prefab");
        File.Copy(source, backup, false);
        return backup;
    }

    private void Install()
    {
        GameObject prefab;
        string path;
        string reason;
        if (!TryElitePrefab(out prefab, out path, out reason))
        {
            status = "[STOP] " + reason;
            return;
        }
        if (prefab.GetComponent<MS7E4EliteRewards>() != null)
        {
            status = "[PASS] Already installed. No duplicate added.\n" + Audit();
            return;
        }
        if (!EditorUtility.DisplayDialog("7E.4 — Install (safety gate OFF)",
            "Backup and add MS7E4EliteRewards to: " + path +
            "\n\nNew extra rewards will initially stay OFF. " +
            "Existing Elite base rewards and Save/Profile are untouched.",
            "BACKUP + INSTALL", "CANCEL")) return;
        GameObject root = null;
        string backup = "";
        try
        {
            backup = BackupExistingPrefab(path);
            root = PrefabUtility.LoadPrefabContents(path);
            if (root.GetComponent<MS7E4EliteRewards>() == null)
                root.AddComponent<MS7E4EliteRewards>();
            if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                throw new InvalidOperationException("Could not save updated Elite prefab.");
            AssetDatabase.SaveAssets();
            status = "[PASS] 7E.4 added (BONUS DISABLED until explicit opt-in).\nBackup: " + backup + "\n" + Audit();
        }
        catch (Exception ex)
        {
            status = "[ERROR] No claim of success. Backup if created: " + backup + "\n" + ex;
            Debug.LogException(ex);
        }
        finally
        {
            if (root != null) PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private void SetGate(bool enabled)
    {
        GameObject prefab;
        string path;
        string reason;
        if (!TryElitePrefab(out prefab, out path, out reason))
        {
            status = "[STOP] " + reason;
            return;
        }
        MS7E4EliteRewards current = prefab.GetComponent<MS7E4EliteRewards>();
        if (current == null)
        {
            status = "[STOP] Install 7E.4 with button 2 first.";
            return;
        }
        if (current.ExtraRewardsEnabled == enabled)
        {
            status = "[PASS] Safety gate already " + (enabled ? "ON" : "OFF") + ". No edits.\n" + Audit();
            return;
        }
        string caption = enabled ? "ENABLE real bonus EXP/Gold" : "DISABLE real bonus EXP/Gold";
        if (!EditorUtility.DisplayDialog("7E.4 — " + caption,
            "The prefab will be backed up first.\n" + path +
            "\n\n" + (enabled
                ? "Confirm that the Elite prefab's original EXP/Gold values are intended and 7E.1 affixes are installed."
                : "Extra rewards will cease for future spawned Elites. Base rewards remain."),
            "BACKUP + CONFIRM", "CANCEL")) return;

        GameObject root = null;
        string backup = "";
        try
        {
            backup = BackupExistingPrefab(path);
            root = PrefabUtility.LoadPrefabContents(path);
            MS7E4EliteRewards component = root.GetComponent<MS7E4EliteRewards>();
            if (component == null) throw new InvalidOperationException("Component disappeared from prefab.");
            SerializedObject serialized = new SerializedObject(component);
            SerializedProperty gate = serialized.FindProperty("enableExtraRewards");
            if (gate == null) throw new InvalidOperationException("Safety gate serialized property missing.");
            gate.boolValue = enabled;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                throw new InvalidOperationException("Prefab save failed.");
            AssetDatabase.SaveAssets();
            status = "[PASS] Extra Elite rewards " + (enabled ? "ON" : "OFF") +
                     " for NEW prefabs instantiated next Play Mode.\nBackup: " + backup + "\n" + Audit();
        }
        catch (Exception ex)
        {
            status = "[ERROR] Gate change not confirmed. Backup if created: " + backup + "\n" + ex;
            Debug.LogException(ex);
        }
        finally
        {
            if (root != null) PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private void Export()
    {
        status = Audit();
        string name = "MS7E4_EliteRewardsQA_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt";
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string path = EditorUtility.SaveFilePanel("7E.4 - Export QA TXT", projectRoot, name, "txt");
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            File.WriteAllText(path, status, new UTF8Encoding(false));
            EditorUtility.RevealInFinder(path);
        }
        catch (Exception ex)
        {
            status += "\n[ERROR] Export failed: " + ex.Message;
            Debug.LogException(ex);
        }
    }
}
#endif
