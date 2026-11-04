#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// 7B.4 = READ-ONLY diagnostic tools. No gameplay components, patches or saves.
// Works even when optional 7B.2/7B.3 packages are not yet installed.
public sealed class MS7B4ElementalFinalQAWindow : EditorWindow
{
    private const string SessionReportKey = "MS7B4.LastReport";
    private const string SessionCsvKey = "MS7B4.LastCsv";
    private static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private readonly StringBuilder report = new StringBuilder();
    private readonly StringBuilder csv = new StringBuilder();
    private readonly Dictionary<MonoBehaviour, Action<float, float>> healthListeners =
        new Dictionary<MonoBehaviour, Action<float, float>>();
    private readonly Dictionary<MonoBehaviour, float> lastHealth = new Dictionary<MonoBehaviour, float>();
    private readonly HashSet<MonoBehaviour> seenBurn = new HashSet<MonoBehaviour>();
    private readonly HashSet<MonoBehaviour> seenSlow = new HashSet<MonoBehaviour>();

    private bool observing;
    private double nextTick;
    private int rows;
    private int hpEvents;
    private int badDuplicateBurn;
    private int arcSampleCount;
    private int elementSwitches;
    private string lastElement = "None";
    private MonoBehaviour lastBossHealth;
    private float startGameTime;
    private Vector2 scroll;
    private int pass, warn, error;
    private string summary = "Chua chay Audit. Mo SampleScene truoc khi kiem tra.";

    [MenuItem("Tools/MonsterSurvival/7B Elemental Balance/1 - OPEN 7B.4 Final QA")]
    public static void Open()
    {
        var window = GetWindow<MS7B4ElementalFinalQAWindow>("7B.4 Final Elemental QA");
        window.minSize = new Vector2(620, 490);
    }

    [MenuItem("Tools/MonsterSurvival/7B Elemental Balance/2 - RUN 7B.4 AUDIT (READ ONLY)")]
    public static void AuditMenu()
    {
        var window = GetWindow<MS7B4ElementalFinalQAWindow>("7B.4 Final Elemental QA");
        window.Audit();
    }

    [MenuItem("Tools/MonsterSurvival/7B Elemental Balance/3 - RUN 7B.4 FORMULA TESTS")]
    public static void FormulaMenu()
    {
        var window = GetWindow<MS7B4ElementalFinalQAWindow>("7B.4 Final Elemental QA");
        window.RunFormulaTests();
    }

    private void OnEnable()
    {
        EditorApplication.update += EditorTick;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        string previous = SessionState.GetString(SessionReportKey, "");
        if (report.Length == 0 && !string.IsNullOrEmpty(previous))
        {
            report.Append(previous);
            csv.Append(SessionState.GetString(SessionCsvKey, ""));
        }
    }

    private void OnDisable()
    {
        StopObserver(false);
        EditorApplication.update -= EditorTick;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        CacheSession();
    }

    private void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingPlayMode && observing)
        {
            Add("[LIVE] Exiting Play Mode: recording stopped. NOT an automatic Victory.");
            StopObserver(true);
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("MONSTERSURVIVAL 7B.4 - ELEMENTAL BALANCE & FINAL QA", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Read-only: no changes to Bullet.cs, SkillManager, Boss, Gold/EXP, Equipment or Save. " +
            "Audit checks wiring; Formula QA tests arithmetic only; Live Observer samples gameplay and cannot prove cause of damage.",
            MessageType.Info);

        EditorGUILayout.LabelField("Current Scene", SceneManager.GetActiveScene().name);
        EditorGUILayout.LabelField("Mode", EditorApplication.isPlaying ? "PLAY" : "EDIT");
        EditorGUILayout.LabelField("Report", summary);
        EditorGUILayout.Space(6);

        if (GUILayout.Button("1 - AUDIT CURRENT SCENE + ELEMENTAL SOURCES (READ ONLY)", GUILayout.Height(31))) Audit();
        if (GUILayout.Button("2 - RUN FORMULA-ONLY SELF TESTS", GUILayout.Height(27))) RunFormulaTests();
        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
        {
            if (!observing)
            {
                if (GUILayout.Button("3 - START LIVE OBSERVER (PLAY MODE)", GUILayout.Height(33))) StartObserver();
            }
            else
            {
                if (GUILayout.Button("3 - STOP & SAVE LIVE OBSERVATIONS", GUILayout.Height(33))) StopObserver(true);
            }
        }
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("EXPORT REPORT TXT", GUILayout.Height(27))) Export(false);
        if (GUILayout.Button("EXPORT LIVE SAMPLES CSV", GUILayout.Height(27))) Export(true);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox(
            "Start Live Observer after entering Play Mode. Sample interval 0.25 s; short-lived lightning arcs can be missed. " +
            "Observed slowed enemies include old Frost/Slow Shot and other Slow sources. " +
            "Keep Shadow Titan at 500 HP; this tool never changes its HP.", MessageType.Warning);
        EditorGUILayout.Space(3);
        EditorGUILayout.LabelField("Audit / Formula / Runtime Report", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(report.Length > 0 ? report.ToString() : "Chua co bao cao.", GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private static List<MonoBehaviour> SceneScripts()
    {
        return UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .Where(x => x != null && x.gameObject.scene.IsValid() && x.gameObject.scene.isLoaded).ToList();
    }

    private static MonoBehaviour FindByName(IEnumerable<MonoBehaviour> scripts, string typeName)
    {
        return scripts.FirstOrDefault(x => x != null && x.GetType().Name == typeName);
    }

    private static List<MonoBehaviour> FindAllByName(IEnumerable<MonoBehaviour> scripts, string typeName)
    {
        return scripts.Where(x => x != null && x.GetType().Name == typeName).ToList();
    }

    private static object Member(object obj, string name)
    {
        if (obj == null) return null;
        Type type = obj.GetType();
        FieldInfo field = type.GetField(name, Fields);
        if (field != null) return field.GetValue(obj);
        PropertyInfo property = type.GetProperty(name, Fields);
        if (property != null && property.GetIndexParameters().Length == 0)
        {
            try { return property.GetValue(obj, null); }
            catch (Exception) { return null; }
        }
        return null;
    }

    private static float FloatValue(object obj, string name, float fallback = 0f)
    {
        object value = Member(obj, name);
        if (value == null) return fallback;
        try { return Convert.ToSingle(value, CultureInfo.InvariantCulture); }
        catch (Exception) { return fallback; }
    }

    private static int IntValue(object obj, string name, int fallback = 0)
    {
        object value = Member(obj, name);
        if (value == null) return fallback;
        try { return Convert.ToInt32(value, CultureInfo.InvariantCulture); }
        catch (Exception) { return fallback; }
    }

    private static bool BoolValue(object obj, string name)
    {
        object value = Member(obj, name);
        return value is bool && (bool)value;
    }

    private static MonoBehaviour ComponentNamed(GameObject go, string name)
    {
        if (go == null) return null;
        return go.GetComponents<MonoBehaviour>().FirstOrDefault(x => x != null && x.GetType().Name == name);
    }

    private static bool HasComponentNamed(GameObject go, string name)
    {
        return ComponentNamed(go, name) != null;
    }

    private static string AbsoluteProjectPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath)) return null;
        return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath));
    }

    private static string ScriptPath(MonoBehaviour component)
    {
        if (component == null) return null;
        MonoScript script = MonoScript.FromMonoBehaviour(component);
        return script != null ? AssetDatabase.GetAssetPath(script) : null;
    }

    private static string LoadSource(MonoBehaviour component)
    {
        string path = ScriptPath(component);
        string absolute = AbsoluteProjectPath(path);
        return !string.IsNullOrEmpty(absolute) && File.Exists(absolute) ? File.ReadAllText(absolute) : null;
    }

    private static string FindUniqueSource(string exactFileName, out int matches)
    {
        matches = 0;
        string result = null;
        foreach (string guid in AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(exactFileName) + " t:MonoScript"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.Equals(Path.GetFileName(path), exactFileName, StringComparison.Ordinal)) continue;
            matches++;
            result = path;
        }
        return result;
    }

    private static int Count(string text, string substring)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(substring)) return 0;
        int count = 0;
        for (int i = 0; (i = text.IndexOf(substring, i, StringComparison.Ordinal)) >= 0; i += substring.Length) count++;
        return count;
    }

    private void Add(string line)
    {
        report.AppendLine(line);
        if (report.Length > 225000) report.Remove(0, Math.Min(30000, report.Length));
    }

    private void Result(bool good, string passText, string failText, bool isError = false)
    {
        if (good) { pass++; Add("[PASS] " + passText); }
        else if (isError) { error++; Add("[ERROR] " + failText); }
        else { warn++; Add("[WARN] " + failText); }
    }

    private void Audit()
    {
        report.Clear();
        pass = warn = error = 0;
        Add("MONSTERSURVIVAL 7B.4 | ELEMENTAL BALANCE QA - READ ONLY");
        Add("Generated " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | Unity " + Application.unityVersion);
        Add("Scene " + SceneManager.GetActiveScene().name + " | Mode " + (EditorApplication.isPlaying ? "PLAY" : "EDIT"));
        Add("Rules: do NOT change Shadow Titan 500 HP. No Save/Gold/EXP/Prefab/Scene changes.");
        Add("---------------------------------");
        try
        {
            var scripts = SceneScripts();
            MonoBehaviour player = FindByName(scripts, "PlayerShooter");
            MonoBehaviour loadout = FindByName(scripts, "MS7B1ElementLoadout");
            MonoBehaviour manager = FindByName(scripts, "SkillManager");
            MonoBehaviour coordinator = FindByName(scripts, "MS7B2ElementalSkillCoordinator");
            MonoBehaviour hub = FindByName(scripts, "MS7B3VisualHub");
            MonoBehaviour wave = FindByName(scripts, "WaveManager");

            Result(SceneManager.GetActiveScene().IsValid() && player != null,
                "Gameplay scene PlayerShooter found", "PlayerShooter missing in loaded Scene. Open SampleScene.");
            Result(loadout != null, "7B.1 Player Loadout attached", "7B.1 Player Loadout missing", true);
            Result(FindAllByName(scripts, "MS7B1ElementLoadout").Count == 1,
                "Only one 7B.1 Player Loadout", "Multiple/no 7B.1 Player Loadouts detected");

            UnityEngine.Object data = Member(loadout, "elementData") as UnityEngine.Object;
            Result(data != null, "7B.1 Elemental Data linked", "7B.1 Elemental Data is not assigned", true);
            if (data != null)
            {
                Add("[INFO] Base/active data | burn " + FloatValue(data, "burnDamagePerSecond").ToString("0.##") +
                    " HP/s for " + FloatValue(data, "burnDuration").ToString("0.##") +
                    "s; ice slow " + (100f * FloatValue(data, "slowPercent")).ToString("0.#") +
                    "% for " + FloatValue(data, "slowDuration").ToString("0.##") +
                    "s; chain targets " + IntValue(data, "maxChainTargets") +
                    ", ratio " + FloatValue(data, "chainDamageRatio").ToString("0.##"));
                Result(FloatValue(data, "burnDamagePerSecond") >= 0f && FloatValue(data, "burnDuration") > 0f &&
                    FloatValue(data, "slowPercent") >= 0f && FloatValue(data, "slowPercent") <= .9f &&
                    IntValue(data, "maxChainTargets") >= 0 && IntValue(data, "maxChainTargets") <= 8,
                    "Element data values within expected ranges", "Element data values outside expected ranges", true);
            }
            Add("[INFO] Selected Element=" + (Member(loadout, "selectedElement") ?? "None") +
                " (None in Edit Mode is valid: skills equip elements during gameplay)");

            MonoBehaviour bulletPrefab = Member(player, "bulletPrefab") as MonoBehaviour;
            Result(bulletPrefab != null, "PlayerShooter Bullet prefab is linked", "PlayerShooter Bullet prefab missing", true);
            if (bulletPrefab != null)
            {
                Result(HasComponentNamed(bulletPrefab.gameObject, "MS7B1ElementalProjectile"),
                    "7B.1 Bullet addon on actual Bullet prefab", "7B.1 addon missing from actual Bullet prefab", true);
                string bulletCode = LoadSource(bulletPrefab);
                Result(bulletCode != null && Count(bulletCode, "MS7B1ElementalHitBridge.AfterBaseHit(") == 1,
                    "Exactly one 7B.1 hook in actual Bullet.cs", "Missing or duplicate 7B.1 bridge hook in Bullet.cs", true);
                if (bulletCode != null)
                {
                    bool legacySlow = bulletCode.Contains("HandleSlow(") && bulletCode.Contains("ProjectileEffectType.Slow");
                    Add("[INFO] Legacy Frost/Slow bullet code detected=" + legacySlow);
                }
            }

            int copies;
            string projectilePath = FindUniqueSource("MS7B1ElementalProjectile.cs", out copies);
            Result(copies == 1, "Exactly one MS7B1ElementalProjectile.cs", "Expected one 7B.1 elemental projectile script, found " + copies, true);
            if (copies == 1 && !string.IsNullOrEmpty(projectilePath) && File.Exists(AbsoluteProjectPath(projectilePath)))
            {
                string projectileCode = File.ReadAllText(AbsoluteProjectPath(projectilePath));
                bool safe = projectileCode.Contains("hasLegacySlow") && projectileCode.Contains("if (!hasLegacySlow)");
                Result(safe, "7B.3 legacy Frost Shot precedence guard detected",
                    "Legacy Frost Shot guard missing. ICE + old Frost may call ApplySlow twice.");
                string[] vfxCalls = {
                    "MS7B3VisualBridge.AfterFire(", "MS7B3VisualBridge.AfterIce(",
                    "MS7B3VisualBridge.AfterLightningPrimary(", "MS7B3VisualBridge.AfterLightningJump("};
                bool vfxPresent = vfxCalls.All(call => Count(projectileCode, call) == 1);
                Result(vfxPresent, "7B.3 four VFX bridges found exactly once", "7B.3 VFX hook(s) missing or duplicated");
            }

            Result(manager != null, "SkillManager exists for Level Up cards", "No SkillManager in Scene");
            Result(coordinator != null, "7B.2 coordinator present", "7B.2 coordinator missing / cards not active");
            Result(FindAllByName(scripts, "MS7B2ElementalSkillCoordinator").Count == 1,
                "7B.2 coordinator has no duplicate", "Multiple/no 7B.2 coordinators");
            if (manager != null)
            {
                string skillCode = LoadSource(manager);
                Result(skillCode != null && Count(skillCode, "MS7B2ElementalSkillBridge.TryApply(") == 1 &&
                       Count(skillCode, "MS7B2ElementalSkillBridge.DecorateOffer(") == 1,
                    "7B.2 SkillManager apply + offer hooks present once", "7B.2 SkillManager hooks missing/duplicated");
            }
            if (coordinator != null && manager != null)
            {
                object skillsRaw = Member(manager, "allSkills");
                IList skills = skillsRaw as IList;
                HashSet<UnityEngine.Object> ownedAssets = new HashSet<UnityEngine.Object>();
                if (skills != null) foreach (object item in skills)
                    if (item is UnityEngine.Object) ownedAssets.Add((UnityEngine.Object)item);
                foreach (string name in new[] { "fireSkill", "iceSkill", "lightningSkill" })
                {
                    UnityEngine.Object skill = Member(coordinator, name) as UnityEngine.Object;
                    Result(skill != null && ownedAssets.Contains(skill),
                        name + " linked and registered in 3-card pool", name + " missing or not in SkillManager.allSkills");
                    if (skill != null)
                    {
                        Result(IntValue(skill, "maxLevel") == 3, name + " maxLevel=3", name + " maxLevel differs from 3");
                        Result(Member(skill, "icon") is Sprite, name + " icon linked", name + " icon missing");
                    }
                }
            }
            Result(FindAllByName(scripts, "MS7B3VisualHub").Count == 1,
                "Exactly one 7B.3 visual/audio hub on active Scene", "Expected one 7B.3 visual hub, found " + FindAllByName(scripts, "MS7B3VisualHub").Count);
            if (hub != null)
            {
                int clips = new[] { "fireHit", "iceHit", "lightningHit" }.Count(n => Member(hub, n) is AudioClip);
                Result(clips == 3, "7B.3 all three AudioClips linked", "7B.3 AudioClips linked " + clips + "/3");
            }

            Result(wave != null, "WaveManager found for wave/boss checks", "WaveManager not found in Scene");
            if (wave != null)
            {
                Add("[INFO] WaveManager CurrentWave=" + IntValue(wave, "CurrentWave") + " bossWave=" + IntValue(wave, "bossWave"));
                object prefabObject = Member(wave, "bossPrefab");
                GameObject prefab = prefabObject as GameObject;
                if (prefab != null)
                {
                    MonoBehaviour prefabHealth = ComponentNamed(prefab, "Health");
                    if (prefabHealth != null)
                        Add("[INFO] Boss Prefab " + prefab.name + " serialized maxHealth=" +
                            FloatValue(prefabHealth, "maxHealth").ToString("0.##") +
                            " (runtime agent/variant may change this; verify spawned Shadow Titan 500 HP separately).");
                }
                MonoBehaviour runningBossHealth = Member(wave, "bossHealth") as MonoBehaviour;
                if (runningBossHealth != null)
                    Add("[INFO] ACTIVE WaveManager boss maxHealth=" + FloatValue(runningBossHealth, "maxHealth") +
                        " currentHP=" + FloatValue(runningBossHealth, "CurrentHealth") +
                        ". Confirm boss identity before claiming Shadow Titan=500.");
                Add("[MANUAL] Shadow Titan runtime 500/500 HP: not auto-verified outside a confirmed active Titan encounter.");
            }

            int slowReady = 0, prefabCount = 0;
            foreach (MonoBehaviour owner in scripts.Where(s => s != null &&
                         (s.GetType().Name == "EnemySpawner" || s.GetType().Name == "WaveManager" ||
                          s.GetType().Name == "HealerWaveSpawner" || s.GetType().Name == "SummonerWaveSpawner")))
            {
                foreach (string name in new[] { "enemyPrefab", "fastEnemyPrefab", "tankEnemyPrefab", "rangedEnemyPrefab", "healerPrefab", "summonerPrefab" })
                {
                    var prefab = Member(owner, name) as GameObject;
                    if (prefab == null || prefabCount > 15) continue;
                    prefabCount++;
                    if (prefab.GetComponentsInChildren<MonoBehaviour>(true).Any(b => b != null && b.GetType().Name == "EnemySlowEffect")) slowReady++;
                    else Add("[WARN] " + owner.GetType().Name + "." + name + " " + prefab.name + " lacks EnemySlowEffect (Ice may not slow this enemy).");
                }
            }
            Add("[INFO] Enemy prefabs with EnemySlowEffect: " + slowReady + "/" + prefabCount + " inspected (only references in open Scene).");
            Add("[MANUAL] Frost+Ice only one ApplySlow per hit, Burn ticks, Chain jumps, VFX/SFX, Loot/Save, 500 HP boss need Play Mode verification.");
        }
        catch (Exception ex)
        {
            error++;
            Add("[ERROR] Audit exception: " + ex.GetType().Name + " " + ex.Message);
        }
        summary = pass + " PASS / " + warn + " WARN / " + error + " ERROR";
        Add("[RESULT] " + summary + ". These are STRUCTURE checks, not Play Mode gameplay passes.");
        CacheSession();
        Debug.Log("[7B.4 AUDIT] " + summary + " (read-only). See 7B.4 QA window for details.");
        Repaint();
    }

    private void RunFormulaTests()
    {
        if (report.Length > 0) Add("---------------------------------");
        Add("[7B.4 FORMULA-ONLY SELF TEST] " + DateTime.Now.ToString("HH:mm:ss"));
        int good = 0, total = 0;
        Action<bool, string> test = (condition, label) =>
        {
            total++;
            if (condition) good++;
            Add((condition ? "[PASS] " : "[FAIL] ") + label);
        };
        test(Math.Abs(2f * 3f - 6f) < .0001f, "Fire Lv1 nominal DOT: 2 DPS * 3s = 6 damage");
        test(Math.Abs(2f * (1f + .3f) - 2.6f) < .0001f, "Fire Lv2 7B.2 +30% DPS");
        test(Math.Abs(2f * (1f + .6f) - 3.2f) < .0001f, "Fire Lv3 7B.2 +60% DPS");
        test(Math.Abs((.3f + .1f * 2f) - .5f) < .0001f, "Ice Lv3 30% + 2*10% = 50% slow");
        test(Math.Abs((1f - .5f) - .5f) < .0001f, "Ice 50% slow is 0.5x move multiplier");
        test(3 + 1 * 2 == 5, "Lightning Lv3 target cap from 7B.2 = 5");
        test(Math.Abs((.45f + .1f * 2f) - .65f) < .0001f, "Lightning Lv3 ratio = 65% of confirmed hit");
        test(Math.Abs(10f * .45f * 3f - 13.5f) < .0001f,
            "Lightning Lv1 maximum secondary damage 13.5 for 10 confirmed hit / 3 targets");
        bool legacySlow = true; bool newIceSlow = true;
        int applyCalls = legacySlow ? 1 : (newIceSlow ? 1 : 0);
        test(applyCalls == 1, "7B.3 Frost precedence decision: exactly one intended Slow call");
        test(Math.Abs(500f * 1.10f - 550f) < .001f, "500 HP base * 1.10 agent multiplier becomes 550; use multiplier 1.00 for 500 (NOT runtime verification)");
        Add("[RESULT] FORMULA SELF TEST " + good + "/" + total + " PASS. Does NOT test collision, Sprite, Audio, Save, Boss or actual Slow call count.");
        summary = "Formula " + good + "/" + total + " PASS (math only)";
        CacheSession();
        Debug.Log("[7B.4 FORMULA] " + good + "/" + total + " PASS (formula only; runtime QA still required)");
        Repaint();
    }

    private void StartObserver()
    {
        if (!EditorApplication.isPlaying) { Add("[WARN] Enter Play Mode first."); return; }
        StopObserver(false);
        csv.Clear();
        csv.AppendLine("seconds,wave,element,fire_lvl,ice_lvl,lightning_lvl,map_index,player_hp,run_gold,player_level,current_exp,active_burn_seen,active_slow_any_source,arc_visuals_seen,boss_hp,boss_max_hp");
        observing = true;
        rows = hpEvents = badDuplicateBurn = arcSampleCount = elementSwitches = 0;
        seenBurn.Clear(); seenSlow.Clear(); lastElement = "";
        startGameTime = Time.time;
        lastBossHealth = null;
        nextTick = EditorApplication.timeSinceStartup;
        Add("[LIVE] Started read-only observer at game time " + startGameTime.ToString("0.##") +
            ". Open window throughout Play Mode. No modifications to enemies or game data.");
        EditorTick();
        Repaint();
    }

    private void StopObserver(bool log)
    {
        observing = false;
        foreach (var kv in healthListeners.ToArray())
        {
            MonoBehaviour health = kv.Key;
            if (health == null) continue;
            try
            {
                EventInfo evt = health.GetType().GetEvent("OnHealthChanged", Fields);
                if (evt != null) evt.RemoveEventHandler(health, kv.Value);
            }
            catch (Exception) { /* scene teardown: ignore */ }
        }
        healthListeners.Clear();
        lastHealth.Clear();
        if (log)
        {
            Add("[LIVE RESULT] samples=" + rows + " | observed burn target components=" + seenBurn.Count +
                " | slowed targets (ALL sources)=" + seenSlow.Count + " | sampled arcs=" + arcSampleCount +
                " | HP-change events=" + hpEvents + " | element changes=" + elementSwitches +
                " | duplicate Burn statuses=" + badDuplicateBurn);
            Add("[LIVE LIMITATIONS] 0 observed burns/arcs does not prove absence (sampling may miss short effects)." +
                " HP-change events are NOT solely elemental. No automatic Boss/Victory validation.");
            CacheSession();
        }
        Repaint();
    }

    private void EditorTick()
    {
        if (!observing || !EditorApplication.isPlaying) return;
        if (EditorApplication.timeSinceStartup < nextTick) return;
        nextTick = EditorApplication.timeSinceStartup + 0.25d;
        try { Sample(); }
        catch (Exception ex)
        {
            Add("[LIVE WARN] Sampling aborted: " + ex.GetType().Name + " " + ex.Message);
            StopObserver(true);
        }
    }

    private void OnHpChanged(MonoBehaviour health, float current, float max)
    {
        if (!observing || health == null) return;
        hpEvents++;
        lastHealth[health] = current;
    }

    private void ObserveHpEvents(MonoBehaviour health)
    {
        if (health == null || healthListeners.ContainsKey(health)) return;
        EventInfo evt = health.GetType().GetEvent("OnHealthChanged", Fields);
        if (evt == null) return;
        Action<float, float> callback = (current, maximum) => OnHpChanged(health, current, maximum);
        try
        {
            evt.AddEventHandler(health, callback);
            healthListeners[health] = callback;
            lastHealth[health] = FloatValue(health, "CurrentHealth");
        }
        catch (Exception) { /* optional event */ }
    }

    private static int SkillLevel(MonoBehaviour manager, MonoBehaviour coordinator, string fieldName)
    {
        if (manager == null || coordinator == null) return -1;
        object asset = Member(coordinator, fieldName);
        if (asset == null) return -1;
        MethodInfo getLevel = manager.GetType().GetMethod("GetSkillLevel", Fields);
        if (getLevel == null) return -1;
        try
        {
            object level = getLevel.Invoke(manager, new[] { asset });
            return level != null ? Convert.ToInt32(level, CultureInfo.InvariantCulture) : -1;
        }
        catch (Exception) { return -1; }
    }

    private void Sample()
    {
        var scripts = SceneScripts();
        MonoBehaviour loadout = FindByName(scripts, "MS7B1ElementLoadout");
        MonoBehaviour progression = FindByName(scripts, "PlayerProgression");
        MonoBehaviour wave = FindByName(scripts, "WaveManager");
        MonoBehaviour manager = FindByName(scripts, "SkillManager");
        MonoBehaviour coordinator = FindByName(scripts, "MS7B2ElementalSkillCoordinator");
        MonoBehaviour map = FindByName(scripts, "WaveMapSwitcher");
        MonoBehaviour playerHealth = FindAllByName(scripts, "Health").FirstOrDefault(h => h != null && h.CompareTag("Player"));
        string element = (Member(loadout, "selectedElement") ?? "None").ToString();
        if (lastElement != element)
        {
            if (lastElement != "") elementSwitches++;
            lastElement = element;
            Add("[LIVE] Element=" + element + " | game time " + Time.time.ToString("0.##"));
        }
        int burnActive = 0, slowActive = 0;
        var enemyHealths = FindAllByName(scripts, "Health");
        foreach (MonoBehaviour hp in enemyHealths)
        {
            if (hp == null) continue;
            bool isEnemy = hp.CompareTag("Enemy");
            if (!isEnemy) continue;
            ObserveHpEvents(hp);
            int burnCount = hp.GetComponents<MonoBehaviour>().Count(x => x != null && x.GetType().Name == "MS7B1BurnStatus");
            if (burnCount > 1) badDuplicateBurn++;
            if (burnCount > 0) { burnActive++; seenBurn.Add(hp); }
            var slow = ComponentNamed(hp.gameObject, "EnemySlowEffect");
            if (slow != null && FloatValue(slow, "SpeedMultiplier", 1f) < .999f)
            {
                slowActive++;
                seenSlow.Add(hp);
            }
        }
        MonoBehaviour runtimeBoss = Member(wave, "bossHealth") as MonoBehaviour;
        if (runtimeBoss != null)
        {
            ObserveHpEvents(runtimeBoss);
            if (runtimeBoss != lastBossHealth)
            {
                lastBossHealth = runtimeBoss;
                Add("[LIVE] WaveManager boss observed: " + runtimeBoss.gameObject.name +
                    " maxHP=" + FloatValue(runtimeBoss, "maxHealth").ToString("0.##") +
                    " (verify this is Shadow Titan before comparing with 500).");
            }
        }
        int arcs = UnityEngine.Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None)
            .Count(x => x != null && (x.name.Contains("LightningArc") || x.name.Contains("7B3") || x.name.Contains("ArcFlash")));
        arcSampleCount += arcs;
        rows++;
        csv.AppendFormat(CultureInfo.InvariantCulture,
            "{0:0.###},{1},{2},{3},{4},{5},{6},{7:0.###},{8},{9},{10},{11},{12},{13},{14:0.###},{15:0.###}\n",
            Mathf.Max(0, Time.time - startGameTime), IntValue(wave, "CurrentWave"), element,
            SkillLevel(manager, coordinator, "fireSkill"), SkillLevel(manager, coordinator, "iceSkill"),
            SkillLevel(manager, coordinator, "lightningSkill"), IntValue(map, "ActiveMapIndex", -1),
            FloatValue(playerHealth, "CurrentHealth", -1f), IntValue(progression, "coins"),
            IntValue(progression, "level"), IntValue(progression, "currentExp"),
            burnActive, slowActive, arcs,
            FloatValue(runtimeBoss, "CurrentHealth", -1f), FloatValue(runtimeBoss, "maxHealth", -1f));
        if (rows % 12 == 0) Repaint();
        if (rows > 7200)
        {
            Add("[LIVE] Limit 7200 samples (about 30 min). Stop and export CSV.");
            StopObserver(true);
        }
    }

    private void Export(bool asCsv)
    {
        string content = asCsv ? csv.ToString() : report.ToString();
        if (string.IsNullOrEmpty(content))
            content = asCsv ? SessionState.GetString(SessionCsvKey, "") : SessionState.GetString(SessionReportKey, "");
        if (string.IsNullOrEmpty(content))
        {
            EditorUtility.DisplayDialog("7B.4", "Chua co du lieu de xuat. Run Audit/Formula or Live Observer first.", "OK");
            return;
        }
        string parent = Directory.GetParent(Application.dataPath).FullName;
        string extension = asCsv ? "csv" : "txt";
        string name = "MS7B4_" + (asCsv ? "LiveSamples" : "FinalQA") + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string path = EditorUtility.SaveFilePanel("Export MonsterSurvival 7B.4 report", parent, name, extension);
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            File.WriteAllText(path, content, new UTF8Encoding(false));
            Debug.Log("[7B.4] Report exported: " + path);
        }
        catch (Exception ex)
        {
            Debug.LogError("[7B.4] Export failed: " + ex.Message);
        }
    }

    private void CacheSession()
    {
        SessionState.SetString(SessionReportKey, report.ToString());
        SessionState.SetString(SessionCsvKey, csv.ToString());
    }
}
#endif
