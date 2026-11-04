#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>7E.1 Editor-only safe installer / prefab backup / read-only diagnostics.</summary>
public sealed class MS7E1EliteAffixInstaller : EditorWindow
{
    [MenuItem("Tools/MonsterSurvival/7E Elite Enemies/1 - Open 7E.1 Elite Affixes")]
    public static void Open()
    {
        GetWindow<MS7E1EliteAffixInstaller>("7E.1 Elite Affixes").minSize = new Vector2(565f, 330f);
    }

    private Vector2 scroll;
    private string lastStatus = "Ready. Open SampleScene in Edit Mode.";

    private void OnGUI()
    {
        EditorGUILayout.LabelField("MONSTERSURVIVAL — 7E.1 ELITE AFFIXES", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Adds Bulwark / Swift / Regenerator to the EXISTING Elite Prefab. " +
            "Does NOT replace EliteEnemy, EliteSpawnDirector, EnemyController or WaveManager.",
            MessageType.Info);
        EditorGUILayout.Space(6);

        if (GUILayout.Button("1 — AUDIT SCENE & ELITE PREFAB (READ ONLY)", GUILayout.Height(30)))
            lastStatus = BuildAudit();

        GUI.enabled = !EditorApplication.isPlaying;
        if (GUILayout.Button("2 — INSTALL AFFIX ON EXISTING ELITE PREFAB (BACKUP)", GUILayout.Height(32)))
            Install();
        GUI.enabled = true;

        if (GUILayout.Button("3 — EXPORT QA TXT (READ ONLY)", GUILayout.Height(30)))
            Export();

        EditorGUILayout.Space(8);
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
        EditorGUILayout.TextArea(lastStatus, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private static bool TryFindPrefab(out GameObject prefab, out string path, out string message)
    {
        prefab = null;
        path = string.Empty;
        message = string.Empty;
        Scene active = EditorSceneManager.GetActiveScene();
        if (!active.IsValid() || !active.isLoaded)
        {
            message = "No active, loaded scene.";
            return false;
        }

        MonoBehaviour matchingDirector = null;
        int directorCount = 0;
        MonoBehaviour[] scripts = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (MonoBehaviour component in scripts)
        {
            if (component == null || component.gameObject.scene != active ||
                component.GetType().Name != "EliteSpawnDirector")
                continue;

            matchingDirector = component;
            directorCount++;
        }

        if (directorCount != 1)
        {
            message = "Expected exactly one EliteSpawnDirector in active scene; found " + directorCount + ".";
            return false;
        }

        SerializedObject serialized = new SerializedObject(matchingDirector);
        SerializedProperty property = serialized.FindProperty("elitePrefab");
        if (property == null)
        {
            message = "EliteSpawnDirector.elitePrefab was not found. No changes were made.";
            return false;
        }

        prefab = property.objectReferenceValue as GameObject;
        if (prefab == null)
        {
            message = "EliteSpawnDirector.elitePrefab is EMPTY. Assign the existing Elite Prefab first.";
            return false;
        }

        path = AssetDatabase.GetAssetPath(prefab);
        if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        {
            message = "The assigned Elite Prefab is not a writable .prefab asset: " + path;
            return false;
        }

        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null)
        {
            message = "Cannot load Elite Prefab asset from: " + path;
            return false;
        }

        // Prevent accidentally turning a normal enemy or Boss into an Elite.
        if (asset.GetComponent<EnemyController>() == null || asset.GetComponent<Health>() == null)
        {
            message = "Prefab requires EnemyController + Health. No changes made: " + path;
            return false;
        }

        bool existingElite = false;
        foreach (MonoBehaviour component in asset.GetComponents<MonoBehaviour>())
        {
            if (component != null && component.GetType().Name == "EliteEnemy")
                existingElite = true;
        }
        if (!existingElite)
        {
            message = "The assigned prefab has no existing EliteEnemy component. " +
                      "7E.1 only extends the original EliteEnemy; it will not create a replacement.";
            return false;
        }

        message = "OK: one EliteSpawnDirector; Elite Prefab=" + path;
        return true;
    }

    private static string BuildAudit()
    {
        StringBuilder result = new StringBuilder();
        result.AppendLine("MONSTERSURVIVAL 7E.1 — ELITE AFFIX QA / READ ONLY");
        result.AppendLine("Unity=" + Application.unityVersion + " | Scene=" +
                          EditorSceneManager.GetActiveScene().name +
                          " | PlayMode=" + EditorApplication.isPlaying +
                          " | Time=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

        GameObject prefab;
        string path;
        string message;
        if (!TryFindPrefab(out prefab, out path, out message))
        {
            result.AppendLine("[WARN] " + message);
        }
        else
        {
            result.AppendLine("[PASS] " + message);
            MS7E1EliteAffix affix = prefab.GetComponent<MS7E1EliteAffix>();
            result.AppendLine(affix != null
                ? "[PASS] MS7E1EliteAffix installed on EXISTING Elite Prefab"
                : "[WARN] Affix not yet installed; run button 2 in Edit Mode");
            result.AppendLine("[PASS] Existing EnemyController, Health and EliteEnemy present");
        }

        MS7E1EliteAffix[] active = UnityEngine.Object.FindObjectsByType<MS7E1EliteAffix>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int bulwark = 0, swift = 0, regen = 0, uninitialized = 0;
        foreach (MS7E1EliteAffix affix in active)
        {
            if (affix == null || !affix.gameObject.scene.IsValid())
                continue;

            if (!affix.Initialized)
            {
                uninitialized++;
                continue;
            }

            switch (affix.ActiveAffix)
            {
                case MS7E1EliteAffix.AffixKind.Bulwark: bulwark++; break;
                case MS7E1EliteAffix.AffixKind.Swift: swift++; break;
                case MS7E1EliteAffix.AffixKind.Regenerator: regen++; break;
            }
        }
        result.AppendLine("[LIVE] Elite affixes: Bulwark=" + bulwark +
                          ", Swift=" + swift + ", Regenerator=" + regen +
                          ", not-started=" + uninitialized);
        result.AppendLine("[INFO] Existing EliteSpawnDirector controls Wave timing, boss exclusions and max alive.");
        result.AppendLine("[INFO] This report does not prove real-time balance, damage output, rewards or Save.");
        result.AppendLine("[MANUAL] Observe all three affixes. Test death, Enrage, EXP/Gold and pause.");
        result.AppendLine("[MANUAL] Check waves 3/4/5, especially boss wave 5, and GameOver/Retry.");
        return result.ToString();
    }

    private void Install()
    {
        GameObject prefab;
        string path;
        string message;
        if (!TryFindPrefab(out prefab, out path, out message))
        {
            lastStatus = "[STOP] " + message;
            return;
        }

        if (prefab.GetComponent<MS7E1EliteAffix>() != null)
        {
            lastStatus = "[PASS] Already installed; installer is idempotent. " + path;
            return;
        }

        if (!EditorUtility.DisplayDialog("7E.1 Safe install",
                "Add ONE MS7E1EliteAffix to existing prefab?\n\n" + path +
                "\n\nOriginal prefab will be copied to MS_Backups/7E1/.\n" +
                "No Wave, Save, Boss or Player files will be changed.",
                "Backup + Install", "Cancel"))
            return;

        string backupPath = string.Empty;
        GameObject editable = null;
        try
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string source = Path.GetFullPath(Path.Combine(projectRoot, path));
            string backups = Path.Combine(projectRoot, "MS_Backups", "7E1");
            Directory.CreateDirectory(backups);
            backupPath = Path.Combine(backups,
                Path.GetFileNameWithoutExtension(path) + "_before_7E1_" +
                DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".prefab");
            File.Copy(source, backupPath, false);

            editable = PrefabUtility.LoadPrefabContents(path);
            if (editable.GetComponent<MS7E1EliteAffix>() == null)
                editable.AddComponent<MS7E1EliteAffix>();
            PrefabUtility.SaveAsPrefabAsset(editable, path);
            AssetDatabase.SaveAssets();

            lastStatus = "[PASS] 7E.1 installed on existing Elite Prefab.\n" +
                         "Prefab: " + path + "\nBackup: " + backupPath +
                         "\nNo EliteSpawnDirector, WaveManager, Boss, Save or rewards touched.\n\n" + BuildAudit();
        }
        catch (Exception ex)
        {
            lastStatus = "[ERROR] Install failed. Original backup if created: " +
                         backupPath + "\n" + ex.Message;
            Debug.LogException(ex);
        }
        finally
        {
            if (editable != null)
                PrefabUtility.UnloadPrefabContents(editable);
        }
    }

    private void Export()
    {
        lastStatus = BuildAudit();
        string filename = "MS7E1_EliteAffixQA_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt";
        string path = EditorUtility.SaveFilePanel("Export 7E.1 Elite Affix QA",
            Path.GetFullPath(Path.Combine(Application.dataPath, "..")), filename, "txt");
        if (string.IsNullOrEmpty(path))
            return;

        try
        {
            File.WriteAllText(path, lastStatus, new UTF8Encoding(false));
            EditorUtility.RevealInFinder(path);
        }
        catch (Exception ex)
        {
            lastStatus += "\n[ERROR] Export failed: " + ex.Message;
            Debug.LogException(ex);
        }
    }
}
#endif
