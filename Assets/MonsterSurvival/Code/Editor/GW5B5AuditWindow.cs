#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// GRAVE WARDEN 5B.5: READ-ONLY INSPECTOR AUDIT.
// Does not create/change/delete any Prefab or Scene object unless the user
// explicitly presses "Add Test Monitor to OPEN scene".
public sealed class GW5B5AuditWindow : EditorWindow
{
    private enum Level { Pass, Warning, Error, Info }
    private sealed class Finding
    {
        public Level level;
        public string text;
        public Finding(Level level, string text) { this.level = level; this.text = text; }
    }

    private GameObject bossPrefab;
    private Vector2 scroll;
    private readonly List<Finding> findings = new List<Finding>();
    private bool hasAudited;
    private const string ToolTitle = "GW 5B.5 Audit";
    private static readonly string[] NeededRootScripts = {
        "GraveWardenBossAI", "Health", "Rigidbody2D", "GraveWardenTombWall5B1",
        "GraveWardenSummons5B1", "GraveWardenSpiritOrbit5B1", "GraveWardenSoulHarvest5B1",
        "GraveWardenAnimatorBridge5B1", "GraveWardenVFX5B3", "GraveWardenAudio5B4"
    };
    private static readonly string[] NeededBossClips = {
        "Idle", "Move", "Spawn_Intro", "TombWall_Prepare", "TombWall_Summon",
        "Wraith_Prepare", "Wraith_Summon", "Reanimate_Cast", "Reanimate_Rise",
        "SpiritOrbit_Cast", "SpiritOrbit_Loop", "SoulHarvest_Absorb", "Empowered_Cast",
        "Hit", "Phase2_Transform", "Phase2_Idle", "Phase2_Move", "Death"
    };
    private static readonly string[] ExpectedAudioFields = {
        "phase1BattleMusic", "phase2BattleMusic", "spawnIntro", "tombWallPrepare",
        "tombWallEmerge", "wraithPrepare", "wraithSummon", "reanimateCast",
        "reanimateRise", "spiritOrbitCast", "soulHarvestAbsorb", "empoweredCast",
        "phase2Transform", "hit", "death"
    };

    [MenuItem("Tools/MonsterSurvival/5B.5 Grave Warden Audit")]
    private static void OpenWindow()
    {
        GW5B5AuditWindow window = GetWindow<GW5B5AuditWindow>(ToolTitle);
        window.minSize = new Vector2(570, 420);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("5B.5 - GRAVE WARDEN QUALITY CHECK", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Chi KIEM TRA, khong thay code, khong sua chi so/Prefab. Chon GraveWardenBoss PREFAB trong Project, khong chon instance o Scene.", MessageType.Info);
        bossPrefab = (GameObject)EditorGUILayout.ObjectField("GraveWardenBoss Prefab", bossPrefab, typeof(GameObject), false);
        if (bossPrefab == null && Selection.activeObject is GameObject && PrefabUtility.IsPartOfPrefabAsset(Selection.activeObject))
            bossPrefab = Selection.activeObject as GameObject;

        EditorGUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("1. Audit Prefab + Active Scene", GUILayout.Height(32))) RunAudit();
        using (new EditorGUI.DisabledScope(!hasAudited))
        {
            if (GUILayout.Button("Copy Report", GUILayout.Height(32))) GUIUtility.systemCopyBuffer = FormatReport();
            if (GUILayout.Button("Save TXT", GUILayout.Height(32))) SaveReport();
        }
        EditorGUILayout.EndHorizontal();
        if (GUILayout.Button("2. Add Test Monitor to OPEN Scene (optional)", GUILayout.Height(29))) InstallMonitor();
        EditorGUILayout.HelpBox("Chi bam (2) khi da mo SampleScene (scene gameplay). Monitor chi hien thong tin, khong lam thay doi AI, HP, cooldown, VFX hay nhac.", MessageType.None);

        int ok = 0, warn = 0, err = 0;
        foreach (Finding f in findings)
        {
            if (f.level == Level.Pass) ok++;
            else if (f.level == Level.Warning) warn++;
            else if (f.level == Level.Error) err++;
        }
        if (hasAudited) EditorGUILayout.LabelField("PASS: " + ok + "    WARNING: " + warn + "    ERROR: " + err, EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (Finding f in findings)
        {
            MessageType style = f.level == Level.Error ? MessageType.Error :
                                f.level == Level.Warning ? MessageType.Warning :
                                f.level == Level.Pass ? MessageType.Info : MessageType.None;
            EditorGUILayout.HelpBox("[" + f.level.ToString().ToUpperInvariant() + "] " + f.text, style);
        }
        EditorGUILayout.EndScrollView();
    }

    private void Add(Level level, string message) { findings.Add(new Finding(level, message)); }
    private static MonoBehaviour GetScript(GameObject go, string typeName)
    {
        if (go == null) return null;
        MonoBehaviour[] list = go.GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour m in list)
            if (m != null && m.GetType().Name == typeName) return m;
        return null;
    }
    private static bool HasScriptInChildren(GameObject go, string name)
    {
        if (go == null) return false;
        MonoBehaviour[] children = go.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour b in children)
            if (b != null && b.GetType().Name == name) return true;
        return false;
    }
    private static SerializedProperty FindField(MonoBehaviour component, string name)
    {
        if (component == null) return null;
        return new SerializedObject(component).FindProperty(name);
    }
    private static UnityEngine.Object ObjectField(MonoBehaviour component, string name)
    {
        SerializedProperty p = FindField(component, name);
        return p != null && p.propertyType == SerializedPropertyType.ObjectReference ? p.objectReferenceValue : null;
    }
    private static int IntField(MonoBehaviour component, string name, int fallback)
    {
        SerializedProperty p = FindField(component, name);
        return p != null && p.propertyType == SerializedPropertyType.Integer ? p.intValue : fallback;
    }
    private void RequiredField(MonoBehaviour component, string field, string name)
    {
        if (component == null) return;
        if (ObjectField(component, field) == null) Add(Level.Error, name + ": " + field + " = None.");
        else Add(Level.Pass, name + ": " + field + " da gan.");
    }
    private void CheckSpriteArray(MonoBehaviour script, string field, string group)
    {
        if (script == null) return;
        SerializedProperty p = FindField(script, field);
        if (p == null || !p.isArray) { Add(Level.Error, "VFX " + group + " khong co Sprite array " + field); return; }
        int missing = 0;
        for (int i = 0; i < p.arraySize; i++)
        {
            SerializedProperty item = p.GetArrayElementAtIndex(i);
            if (item.objectReferenceValue == null) missing++;
        }
        if (p.arraySize == 6 && missing == 0) Add(Level.Pass, "VFX " + group + ": 6/6 frames.");
        else Add(Level.Error, "VFX " + group + ": frames = " + p.arraySize + ", references None = " + missing + " (mong doi 6).");
    }

    private void RunAudit()
    {
        findings.Clear();
        hasAudited = true;
        if (bossPrefab == null || !PrefabUtility.IsPartOfPrefabAsset(bossPrefab))
        {
            Add(Level.Error, "Chon asset Prefab GraveWardenBoss trong Project. Khong chon Boss trong Hierarchy.");
            return;
        }
        string path = AssetDatabase.GetAssetPath(bossPrefab);
        Add(Level.Info, "Boss Prefab: " + path);
        foreach (string script in NeededRootScripts)
        {
            if (script == "Health" || script == "Rigidbody2D")
            {
                Type t = script == "Health" ? typeof(MonoBehaviour) : typeof(Rigidbody2D);
                bool found = script == "Health" ? GetScript(bossPrefab, "Health") != null : bossPrefab.GetComponent(t) != null;
                Add(found ? Level.Pass : Level.Error, script + (found ? " OK" : " THIEU tren GameObject goc"));
            }
            else Add(GetScript(bossPrefab, script) != null ? Level.Pass : Level.Error,
                     script + (GetScript(bossPrefab, script) != null ? " OK" : " THIEU tren Boss Prefab"));
        }
        CheckBossVisual();
        CheckWall();
        CheckMinions();
        CheckOrbitAndSouls();
        CheckVFX();
        CheckAudio();
        CheckOpenScene();
        Repaint();
    }

    private void CheckBossVisual()
    {
        Transform child = bossPrefab.transform.Find("GW_Visual");
        if (child == null) { Add(Level.Error, "Khong co child GW_Visual."); return; }
        Animator animator = child.GetComponent<Animator>();
        SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
        if (renderer == null) Add(Level.Error, "GW_Visual thieu SpriteRenderer.");
        else Add(renderer.enabled && renderer.sprite != null ? Level.Pass : Level.Error,
                 "GW_Visual SpriteRenderer " + (renderer.enabled && renderer.sprite != null ? "OK" : "chua bat / Sprite None"));
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            Add(Level.Error, "GW_Visual Animator/Controller chua gan.");
        }
        else
        {
            Add(Level.Pass, "GW_Visual Animator controller: " + animator.runtimeAnimatorController.name);
            HashSet<string> available = new HashSet<string>();
            foreach (AnimationClip c in animator.runtimeAnimatorController.animationClips)
                if (c != null) available.Add(c.name);
            int missing = 0;
            foreach (string required in NeededBossClips)
                if (!available.Contains(required)) { Add(Level.Warning, "Missing clip (ten clip): " + required); missing++; }
            if (missing == 0) Add(Level.Pass, "Du 18 ten animation Boss (5B.2).");
            string[] neededParameters = { "IsMoving", "IsPhaseTwo", "TombWall_Prepare", "TombWall_Summon", "Wraith_Prepare", "Wraith_Summon", "Reanimate_Cast", "Reanimate_Rise", "SpiritOrbit_Cast", "SoulHarvest_Absorb", "Empowered_Cast", "Phase2_Transform", "Death" };
            HashSet<string> paramsSet = new HashSet<string>();
            foreach (AnimatorControllerParameter p in animator.parameters) paramsSet.Add(p.name);
            foreach (string key in neededParameters)
                if (!paramsSet.Contains(key)) Add(Level.Warning, "Animator missing parameter: " + key);
        }
        MonoBehaviour bridge = GetScript(bossPrefab, "GraveWardenAnimatorBridge5B1");
        if (bridge != null)
        {
            UnityEngine.Object linked = ObjectField(bridge, "animator");
            if (linked != animator) Add(Level.Error, "AnimatorBridge.animator phai tro den GW_Visual -> Animator.");
            else Add(Level.Pass, "AnimatorBridge da tham chieu dung GW_Visual.");
        }
        SpriteRenderer rootSprite = bossPrefab.GetComponent<SpriteRenderer>();
        if (rootSprite != null && rootSprite.enabled && rootSprite.sprite != null)
            Add(Level.Warning, "SpriteRenderer tren Boss ROOT dang bat (co the chong hinh TankEnemy). Nen tat rieng renderer ROOT, KHONG tat GameObject.");
    }

    private void CheckWall()
    {
        MonoBehaviour script = GetScript(bossPrefab, "GraveWardenTombWall5B1");
        if (script == null) return;
        RequiredField(script, "warningSprite", "TombWall Warning Sprite");
        GameObject wall = ObjectField(script, "wallPrefab") as GameObject;
        if (wall == null) { Add(Level.Error, "TombWall.wallPrefab = NONE (loi da tung gap trong du an!)."); return; }
        Add(Level.Pass, "TombWall wallPrefab: " + wall.name);
        Collider2D col = wall.GetComponentInChildren<Collider2D>(true);
        SpriteRenderer renderer = wall.GetComponentInChildren<SpriteRenderer>(true);
        if (col == null) Add(Level.Error, "Wall Prefab thieu Collider2D.");
        else
        {
            if (col.isTrigger) Add(Level.Error, "Wall Collider2D.IsTrigger = true. Can OFF.");
            else Add(Level.Pass, "Wall Collider2D blocking OK.");
            int layerId = LayerMask.NameToLayer("Obstacles");
            if (layerId < 0) Add(Level.Error, "Project chua co Layer 'Obstacles'.");
            else if (col.gameObject.layer != layerId) Add(Level.Warning, "Layer cua Wall Collider khong phai 'Obstacles'. Script co dat lai layer luc spawn nhung nen sua trong Prefab.");
        }
        if (renderer == null || renderer.sprite == null) Add(Level.Error, "Wall Prefab thieu SpriteRenderer/Sprite.");
        else Add(Level.Pass, "Wall co SpriteRenderer + Sprite.");
        int cap = IntField(script, "maxAliveWalls", 3);
        if (cap > 3) Add(Level.Warning, "maxAliveWalls = " + cap + ": cao hon gioi han de xuat 3.");
    }

    private void CheckMinions()
    {
        MonoBehaviour script = GetScript(bossPrefab, "GraveWardenSummons5B1");
        if (script == null) return;
        CheckMinion(ObjectField(script, "wraithPrefab") as GameObject, "Wraith");
        CheckMinion(ObjectField(script, "reanimatedWarriorPrefab") as GameObject, "Skeleton Warrior");
        RequiredField(script, "soulMarkerSprite", "Summons Soul Marker");
        int wraithLimit = IntField(script, "maxLiveWraiths", 4);
        int warriorLimit = IntField(script, "maxLiveWarriors", 2);
        if (wraithLimit > 4 || warriorLimit > 2)
            Add(Level.Warning, "Summon limit: Wraith " + wraithLimit + " / Warrior " + warriorLimit + ". De xuat <= 4 va <= 2.");
        else Add(Level.Pass, "Summon limits: Wraith <= " + wraithLimit + ", Warrior <= " + warriorLimit + ".");
    }

    private void CheckMinion(GameObject prefab, string role)
    {
        if (prefab == null) { Add(Level.Error, role + " Prefab = None."); return; }
        Add(Level.Pass, role + " prefab: " + prefab.name);
        if (!PrefabUtility.IsPartOfPrefabAsset(prefab)) Add(Level.Warning, role + " nen la Prefab Asset, khong phai Scene instance.");
        foreach (string script in new [] { "Health", "EnemyController" })
            if (GetScript(prefab, script) == null) Add(Level.Error, role + " missing " + script + ".");
        if (prefab.GetComponent<Rigidbody2D>() == null) Add(Level.Error, role + " missing Rigidbody2D.");
        if (prefab.GetComponentInChildren<Collider2D>(true) == null) Add(Level.Error, role + " missing Collider2D.");
        if (HasScriptInChildren(prefab, "SummonedMinion")) Add(Level.Warning, role + " con SummonedMinion cu: co the tu huy sai khi trieu hoi.");
        if (GetScript(prefab, "GWMinionAnimationBridge5B3") == null)
            Add(Level.Warning, role + " thieu GWMinionAnimationBridge5B3 (8 animation rieng se khong chuyen state).");
        Transform child = prefab.transform.Find("GW_Visual");
        if (child == null) Add(Level.Warning, role + " thieu GW_Visual (co the van dung sprite MinionEnemy cu).");
        else
        {
            Animator a = child.GetComponent<Animator>();
            SpriteRenderer sr = child.GetComponent<SpriteRenderer>();
            if (a == null || a.runtimeAnimatorController == null) Add(Level.Error, role + " child GW_Visual thieu Animator Controller.");
            if (sr == null || sr.sprite == null || !sr.enabled) Add(Level.Error, role + " GW_Visual SpriteRenderer chua san sang.");
        }
        SpriteRenderer rootSprite = prefab.GetComponent<SpriteRenderer>();
        if (rootSprite != null && rootSprite.enabled && rootSprite.sprite != null && child != null)
            Add(Level.Warning, role + " SpriteRenderer ROOT dang bat: de tranh hinh Minion cu chong len, tat RENDERER cu.");
    }

    private void CheckOrbitAndSouls()
    {
        MonoBehaviour orbit = GetScript(bossPrefab, "GraveWardenSpiritOrbit5B1");
        if (orbit != null) RequiredField(orbit, "orbitSprite", "Spirit Orbit Sprite");
        MonoBehaviour harvest = GetScript(bossPrefab, "GraveWardenSoulHarvest5B1");
        if (harvest != null)
        {
            int capacity = IntField(harvest, "capacity", 5);
            int cost = IntField(harvest, "empowerCost", 3);
            if (cost > capacity) Add(Level.Error, "Soul Harvest empowerCost > capacity: khong bao gio du souls de cast.");
            else Add(Level.Pass, "Soul Harvest capacity = " + capacity + ", empowerCost = " + cost);
        }
    }

    private void CheckVFX()
    {
        MonoBehaviour vfx = GetScript(bossPrefab, "GraveWardenVFX5B3");
        if (vfx == null) return;
        CheckSpriteArray(vfx, "tombWallRise", "Tomb Wall");
        CheckSpriteArray(vfx, "wraithPortal", "Wraith Portal");
        CheckSpriteArray(vfx, "reanimateRise", "Reanimate Fallen");
        CheckSpriteArray(vfx, "spiritOrbit", "Spirit Orbit");
        CheckSpriteArray(vfx, "soulHarvest", "Soul Harvest");
        if (GetScript(bossPrefab, "GW5B3PlayModeTesterV3") != null)
            Add(Level.Warning, "Con GW5B3PlayModeTesterV3 (chi de test, nen go khoi ban phat hanh).");
    }

    private void CheckAudio()
    {
        MonoBehaviour audio = GetScript(bossPrefab, "GraveWardenAudio5B4");
        if (audio == null) return;
        int linked = 0;
        foreach (string name in ExpectedAudioFields)
        {
            if (ObjectField(audio, name) == null) Add(Level.Warning, "AudioClip None: " + name);
            else linked++;
        }
        if (linked == ExpectedAudioFields.Length) Add(Level.Pass, "Du " + linked + "/15 AudioClips 5B.4.");
        Animator visual = bossPrefab.transform.Find("GW_Visual") != null ?
                          bossPrefab.transform.Find("GW_Visual").GetComponent<Animator>() : null;
        if (ObjectField(audio, "visualAnimator") != visual) Add(Level.Warning, "Audio5B4.visualAnimator chua gan dung GW_Visual (SFX theo chi eu co the im lang).");
        SerializedProperty testPanel = FindField(audio, "showTestPanelInPlayMode");
        if (testPanel != null && testPanel.boolValue) Add(Level.Warning, "Audio Test Panel dang bat: tat khi phat hanh.");
    }

    private void CheckOpenScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        Add(Level.Info, "Active Scene: " + scene.name);
        if (!scene.IsValid() || !scene.isLoaded) return;
        var present = new HashSet<string>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb != null) present.Add(mb.GetType().Name);
        }
        if (!present.Contains("WaveManager") && !present.Contains("GameManager"))
        {
            Add(Level.Info, "Scene hien tai co the la MainMenu. Mo SampleScene de kiem tra WaveManager/Maps.");
            return;
        }
        foreach (string needed in new [] { "WaveManager", "GameManager", "WaveMapSwitcher", "GraveBossVariantSelector5A", "MapSafeSpawn2D", "MapPathfinding2D" })
            Add(present.Contains(needed) ? Level.Pass : Level.Warning, "Active Scene component " + needed + (present.Contains(needed) ? " OK" : " chua thay"));
        Add(Level.Info, "Battle tests: Phase2, BossHP, Victory, Retry, Pause va 3 Maps can kiem tra THU CONG o Play Mode.");
    }

    private string FormatReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("GRAVE WARDEN 5B.5 AUDIT - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine("Prefab: " + (bossPrefab != null ? AssetDatabase.GetAssetPath(bossPrefab) : "NONE"));
        sb.AppendLine("Scene: " + SceneManager.GetActiveScene().name);
        sb.AppendLine("READ-ONLY report; no gameplay code changed.");
        sb.AppendLine(new string('-', 72));
        foreach (Finding f in findings) sb.AppendLine("[" + f.level.ToString().ToUpperInvariant() + "] " + f.text);
        return sb.ToString();
    }

    private void SaveReport()
    {
        string destination = EditorUtility.SaveFilePanel("Save GW 5B.5 Audit", "", "GW_5B5_Audit_Report.txt", "txt");
        if (string.IsNullOrEmpty(destination)) return;
        File.WriteAllText(destination, FormatReport(), Encoding.UTF8);
        EditorUtility.RevealInFinder(destination);
    }

    private void InstallMonitor()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.name == "MainMenu")
        {
            EditorUtility.DisplayDialog("5B.5", "Mo SampleScene (Game scene) truoc khi them monitor. Khong them vao MainMenu.", "OK");
            return;
        }
        GW5B5PlaytestMonitor existing = UnityEngine.Object.FindAnyObjectByType<GW5B5PlaytestMonitor>();
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            EditorUtility.DisplayDialog("5B.5", "Monitor da co trong Scene, minh da chon no.", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("5B.5 TEST ONLY", "Tao GW_5B5_PlaytestMonitor trong Scene dang mo?\nChi theo doi va hien thi, khong thay gameplay.\nGo no truoc khi build game.", "Create", "Cancel")) return;
        GameObject go = new GameObject("GW_5B5_PlaytestMonitor_TEST_ONLY");
        Undo.RegisterCreatedObjectUndo(go, "Create GW 5B5 Test Monitor");
        go.AddComponent<GW5B5PlaytestMonitor>();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = go;
    }
}
#endif
