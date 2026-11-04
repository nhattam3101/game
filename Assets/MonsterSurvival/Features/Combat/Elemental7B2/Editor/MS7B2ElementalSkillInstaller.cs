#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Unique class and path: NEVER copy to Assets/MonsterSurvival/Code/Editor or duplicate this file.
public sealed class MS7B2ElementalSkillInstaller : EditorWindow
{
    private const string Root = "Assets/MonsterSurvival/Features/Combat/Elemental7B2";
    private const string DataFolder = Root + "/Data";
    private const string HookApply = "if (!MS7B2ElementalSkillBridge.TryApply(this, stats, selected, newLevel))\n            stats.ApplySkill(selected);";
    private const string HookOffer = "MS7B2ElementalSkillBridge.DecorateOffer(this, skill, i, currentLevel, nextLevel);";

    private SkillManager manager;
    private MS7B1ElementLoadout loadout;
    private Vector2 scroll;
    private string lastReport = "Chua chay Audit.";

    [MenuItem("Tools/MonsterSurvival/7B Elemental Skills/1 - OPEN 7B.2 Installer")]
    public static void Open()
    {
        var w = GetWindow<MS7B2ElementalSkillInstaller>("7B.2 Elemental Skills");
        w.minSize = new Vector2(550, 490);
        w.AutoDetect();
    }

    [MenuItem("Tools/MonsterSurvival/7B Elemental Skills/2 - AUDIT 7B.2 (READ ONLY)")]
    public static void AuditMenu() { var w = GetWindow<MS7B2ElementalSkillInstaller>(); w.AutoDetect(); w.Audit(); }

    [MenuItem("Tools/MonsterSurvival/7B Elemental Skills/3 - REMOVE 7B.2 HOOK & SCENE ADDON")]
    public static void RemoveMenu() { var w = GetWindow<MS7B2ElementalSkillInstaller>(); w.AutoDetect(); w.Remove(); }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("MONSTERSURVIVAL — 7B.2 ELEMENTAL SKILL CARDS", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Integrates with existing 3-card SkillManager; does not replace PlayerStats, Bullet, Save or Shadow Titan HP. Requires 7B.1 installed and 0 Console Errors.", MessageType.Info);
        manager = (SkillManager)EditorGUILayout.ObjectField("Scene SkillManager", manager, typeof(SkillManager), true);
        loadout = (MS7B1ElementLoadout)EditorGUILayout.ObjectField("Player 7B.1 Loadout", loadout, typeof(MS7B1ElementLoadout), true);
        EditorGUILayout.Space(8);
        if (GUILayout.Button("AUTO-DETECT SCENE REFERENCES", GUILayout.Height(26))) AutoDetect();
        if (GUILayout.Button("INSTALL / UPDATE (BACKUP FIRST)", GUILayout.Height(40))) Install();
        if (GUILayout.Button("AUDIT (READ ONLY)", GUILayout.Height(29))) Audit();
        if (GUILayout.Button("REMOVE 7B.2 ONLY (KEEP SKILL ASSETS)", GUILayout.Height(29))) Remove();
        if (GUILayout.Button("EXPORT LAST REPORT TXT", GUILayout.Height(25))) ExportReport();
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Audit / Install output", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(lastReport, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void AutoDetect()
    {
        // Scene-only, including inactive objects; no prefabs in Project.
        var managers = Resources.FindObjectsOfTypeAll<SkillManager>();
        manager = managers.FirstOrDefault(x => x != null && x.gameObject.scene.IsValid() && x.gameObject.scene.isLoaded);
        var loadouts = Resources.FindObjectsOfTypeAll<MS7B1ElementLoadout>();
        loadout = loadouts.FirstOrDefault(x => x != null && x.gameObject.scene.IsValid() && x.gameObject.scene.isLoaded);
        Repaint();
    }

    private static string SkillSourcePath(SkillManager selectedManager)
    {
        if (selectedManager == null) return null;
        MonoScript script = MonoScript.FromMonoBehaviour(selectedManager);
        return script != null ? AssetDatabase.GetAssetPath(script) : null;
    }

    private static bool HasHook(string source) => source.Contains("MS7B2ElementalSkillBridge.TryApply(") && source.Contains("MS7B2ElementalSkillBridge.DecorateOffer(");

    private static bool TryPatch(string original, out string patched, out string problem)
    {
        patched = original;
        problem = null;
        bool apply = original.Contains("MS7B2ElementalSkillBridge.TryApply(");
        bool offer = original.Contains("MS7B2ElementalSkillBridge.DecorateOffer(");
        if (apply != offer)
        {
            problem = "One 7B.2 hook exists but the other is missing; refusing to alter partially modified SkillManager. Restore/check manually.";
            return false;
        }
        if (apply) return true;
        MatchCollection calls = Regex.Matches(original, @"\bstats\s*\.\s*ApplySkill\s*\(\s*selected\s*\)\s*;");
        MatchCollection anchors = Regex.Matches(original, @"\bint\s+buttonIndex\s*=\s*i\s*;");
        if (calls.Count != 1 || anchors.Count != 1 ||
            original.IndexOf("void ChooseSkill", StringComparison.Ordinal) < 0 ||
            original.IndexOf("void OpenSkillSelection", StringComparison.Ordinal) < 0)
        {
            problem = "Unsupported SkillManager.cs structure (expected exactly one stats.ApplySkill(selected) and one int buttonIndex = i). No source changes made.";
            return false;
        }
        patched = original.Remove(calls[0].Index, calls[0].Length).Insert(calls[0].Index, HookApply);
        // Re-match after first edit (indexes may shift).
        Match place = Regex.Match(patched, @"\bint\s+buttonIndex\s*=\s*i\s*;");
        patched = patched.Insert(place.Index, HookOffer + "\n            ");
        return true;
    }

    private static bool TryRemoveHooks(string original, out string cleaned)
    {
        cleaned = original;
        if (!original.Contains("MS7B2ElementalSkillBridge")) return true;
        if (!original.Contains(HookApply) || !original.Contains(HookOffer)) return false;
        cleaned = cleaned.Replace(HookApply, "stats.ApplySkill(selected);");
        cleaned = cleaned.Replace(HookOffer + "\n            ", "");
        return true;
    }

    private static void Folder(string path)
    {
        var parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static void Backup(string relativePath, string label)
    {
        if (string.IsNullOrEmpty(relativePath) || !File.Exists(relativePath)) return;
        string root = Path.GetDirectoryName(Application.dataPath);
        string folder = Path.Combine(root, "MS_Backups", "7B2", DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
        Directory.CreateDirectory(folder);
        File.Copy(relativePath, Path.Combine(folder, label), true);
        Debug.Log("[7B.2 BACKUP] " + folder + "/" + label);
    }

    private static SkillData EnsureSkill(string name, string display, SkillRarity rarity, string iconName)
    {
        string path = DataFolder + "/MS7B2_" + name + "_Skill.asset";
        SkillData skill = AssetDatabase.LoadAssetAtPath<SkillData>(path);
        if (skill == null) { skill = ScriptableObject.CreateInstance<SkillData>(); AssetDatabase.CreateAsset(skill, path); }
        skill.skillName = display;
        skill.description = "Elemental " + name + " (7B.2; values shown on card)";
        // The bridge intercepts BEFORE PlayerStats.ApplySkill; Heal(0) is only a safe fallback.
        skill.skillType = SkillType.Heal;
        skill.value = 0f;
        skill.rarity = rarity;
        skill.maxLevel = 3;
        string iconPath = Root + "/Icons/MS7B2_" + iconName + ".png";
        TextureImporter importer = AssetImporter.GetAtPath(iconPath) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        skill.icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
        EditorUtility.SetDirty(skill);
        return skill;
    }

    private static Image EnsureIcon(Button button)
    {
        if (button == null) return null;
        Transform child = button.transform.Find("MS7B2_ElementIcon");
        GameObject go;
        if (child != null) go = child.gameObject;
        else
        {
            go = new GameObject("MS7B2_ElementIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            Undo.RegisterCreatedObjectUndo(go, "7B.2 Add Element Icon");
            go.transform.SetParent(button.transform, false);
        }
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(42f, 42f);
        rect.anchoredPosition = new Vector2(15f, -14f);
        LayoutElement layout = go.GetComponent<LayoutElement>();
        if (layout != null) layout.ignoreLayout = true;
        Image img = go.GetComponent<Image>();
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.gameObject.SetActive(false);
        return img;
    }

    private void Install()
    {
        if (manager == null || loadout == null || loadout.elementData == null ||
            manager.skillButtons == null || manager.skillButtons.Length < 3 || manager.allSkills == null)
        {
            EditorUtility.DisplayDialog("7B.2 Preflight failed", "Assign Scene SkillManager (with 3 buttons) and 7B.1 Player Loadout with Element Data. No changes made.", "OK");
            return;
        }
        string sourcePath = SkillSourcePath(manager);
        if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
        {
            EditorUtility.DisplayDialog("7B.2", "Cannot locate the bound SkillManager.cs source.", "OK"); return;
        }
        string oldSource = File.ReadAllText(sourcePath);
        string newSource, problem;
        if (!TryPatch(oldSource, out newSource, out problem))
        {
            EditorUtility.DisplayDialog("7B.2 Safety stop", problem, "OK"); return;
        }
        if (!EditorUtility.DisplayDialog("Install 7B.2", "Backup SkillManager.cs and open Scene, create 3 SkillData assets and add two small hooks? Existing Save and other scripts are untouched.", "INSTALL", "CANCEL")) return;

        Backup(sourcePath, "SkillManager_before_7B2.cs");
        var scene = manager.gameObject.scene;
        if (!string.IsNullOrEmpty(scene.path)) Backup(scene.path, "Scene_before_7B2.unity");
        Folder(DataFolder);
        var fire = EnsureSkill("Fire", "FIRE AMMO", SkillRarity.Rare, "Fire");
        var ice = EnsureSkill("Ice", "ICE AMMO", SkillRarity.Rare, "Ice");
        var lightning = EnsureSkill("Lightning", "LIGHTNING AMMO", SkillRarity.Epic, "Lightning");
        MS7B2ElementalSkillCoordinator co = manager.GetComponent<MS7B2ElementalSkillCoordinator>();
        if (co == null) co = Undo.AddComponent<MS7B2ElementalSkillCoordinator>(manager.gameObject);
        Undo.RecordObject(manager, "7B.2 Elemental skills");
        Undo.RecordObject(co, "7B.2 Configure coordinator");
        co.fireSkill = fire; co.iceSkill = ice; co.lightningSkill = lightning;
        if (co.cardIconImages == null || co.cardIconImages.Length != 3) co.cardIconImages = new Image[3];
        co.playerLoadout = loadout;
        co.baseElementData = loadout.elementData;
        for (int i = 0; i < 3; i++) co.cardIconImages[i] = EnsureIcon(manager.skillButtons[i]);
        if (!manager.allSkills.Contains(fire)) manager.allSkills.Add(fire);
        if (!manager.allSkills.Contains(ice)) manager.allSkills.Add(ice);
        if (!manager.allSkills.Contains(lightning)) manager.allSkills.Add(lightning);
        EditorUtility.SetDirty(manager); EditorUtility.SetDirty(co);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (oldSource != newSource)
        {
            File.WriteAllText(sourcePath, newSource, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(sourcePath, ImportAssetOptions.ForceUpdate);
        }
        lastReport = "[7B.2] INSTALL DONE. Three skills + icon overlays + coordinator linked. Source hook: " + (oldSource == newSource ? "already installed" : "inserted") + ".\nAudit after compilation; test all 3 skills in Play Mode.";
        Debug.Log(lastReport);
        AssetDatabase.Refresh();
    }

    private void Audit()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("MONSTERSURVIVAL 7B.2 — ELEMENTAL SKILL CARDS / READ ONLY");
        sb.AppendLine("Unity " + Application.unityVersion + " | " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        if (manager == null) AutoDetect();
        if (manager == null) sb.AppendLine("[ERROR] No SkillManager in loaded Scene");
        else
        {
            string path = SkillSourcePath(manager);
            string source = !string.IsNullOrEmpty(path) && File.Exists(path) ? File.ReadAllText(path) : "";
            sb.AppendLine("[" + (HasHook(source) ? "PASS" : "WARN") + "] SkillManager 2 hooks installed: " + HasHook(source));
            MS7B2ElementalSkillCoordinator co = manager.GetComponent<MS7B2ElementalSkillCoordinator>();
            sb.AppendLine("[" + (co != null ? "PASS" : "ERROR") + "] Coordinator attached exactly once: " + (manager.GetComponents<MS7B2ElementalSkillCoordinator>().Length == 1));
            if (co != null)
            {
                SkillData[] skills = { co.fireSkill, co.iceSkill, co.lightningSkill };
                string[] names = { "Fire", "Ice", "Lightning" };
                for (int i = 0; i < skills.Length; i++)
                {
                    SkillData skill = skills[i];
                    bool ok = skill != null && manager.allSkills != null && manager.allSkills.Contains(skill) && skill.icon != null && skill.maxLevel == 3;
                    sb.AppendLine("[" + (ok ? "PASS" : "ERROR") + "] Element " + names[i] + ": linked/registered/icon/maxLevel");
                }
                int icons = co.cardIconImages != null ? co.cardIconImages.Count(x => x != null) : 0;
                sb.AppendLine("[" + (icons == 3 ? "PASS" : "WARN") + "] Card icon overlays " + icons + "/3");
                sb.AppendLine("[" + (co.playerLoadout != null && co.baseElementData != null ? "PASS" : "ERROR") + "] Base 7B.1 Player Loadout & Element Data");
            }
            sb.AppendLine("[INFO] Total skills in current SkillManager: " + (manager.allSkills != null ? manager.allSkills.Count : 0));
        }
        var duplicates = AssetDatabase.FindAssets("MS7B1ElementalInstaller t:MonoScript").Select(AssetDatabase.GUIDToAssetPath).Where(x => x.EndsWith("/MS7B1ElementalInstaller.cs")).ToArray();
        if (duplicates.Length > 1) sb.AppendLine("[WARN] Found " + duplicates.Length + " copies of MS7B1ElementalInstaller.cs. Remove the duplicate to fix CS0111 / CS0229.");
        sb.AppendLine("[INFO] Audit does NOT play the game or verify status-effect damage, Save, reward or 500 HP Boss.");
        lastReport = sb.ToString();
        Debug.Log(lastReport);
        Repaint();
    }

    private void Remove()
    {
        if (manager == null) { AutoDetect(); if (manager == null) { Debug.LogWarning("[7B.2] No Scene SkillManager."); return; } }
        if (!EditorUtility.DisplayDialog("Remove 7B.2", "Remove only the 7B.2 coordinator, card overlays and two SkillManager hooks? SkillData assets and ALL Save data remain. Backup your project first.", "REMOVE", "CANCEL")) return;
        string path = SkillSourcePath(manager);
        string original = path != null && File.Exists(path) ? File.ReadAllText(path) : null;
        string cleaned;
        if (original == null || !TryRemoveHooks(original, out cleaned))
        {
            EditorUtility.DisplayDialog("7B.2 Safety stop", "SkillManager hooks differ from expected patch. No changes made; inspect SkillManager manually.", "OK"); return;
        }
        Backup(path, "SkillManager_before_remove_7B2.cs");
        var scene = manager.gameObject.scene;
        if (!string.IsNullOrEmpty(scene.path)) Backup(scene.path, "Scene_before_remove_7B2.unity");
        MS7B2ElementalSkillCoordinator co = manager.GetComponent<MS7B2ElementalSkillCoordinator>();
        if (co != null)
        {
            Undo.RecordObject(manager, "Remove 7B.2 skills");
            manager.allSkills.Remove(co.fireSkill);
            manager.allSkills.Remove(co.iceSkill);
            manager.allSkills.Remove(co.lightningSkill);
            foreach (Button button in manager.skillButtons ?? new Button[0])
            {
                if (button == null) continue;
                Transform child = button.transform.Find("MS7B2_ElementIcon");
                if (child != null) Undo.DestroyObjectImmediate(child.gameObject);
            }
            Undo.DestroyObjectImmediate(co);
        }
        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (original != cleaned) { File.WriteAllText(path, cleaned, new UTF8Encoding(false)); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); }
        lastReport = "[7B.2] REMOVED two hooks and UI coordinator. Generated SkillData assets kept. 7B.1 remains intact.";
        Debug.Log(lastReport);
        AssetDatabase.Refresh();
    }

    private void ExportReport()
    {
        string path = EditorUtility.SaveFilePanel("Export 7B.2 Audit", "", "MS7B2_Audit.txt", "txt");
        if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, lastReport, Encoding.UTF8);
    }
}
#endif
