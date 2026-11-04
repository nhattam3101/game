#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// 7D.4 — READ ONLY final equipment set integration QA.
// Does not install/remove components, edit Scene assets, change stats, modify Save,
// change 7D.2 gates, create loot, or manipulate Inventory.
public sealed class MS7D4FinalEquipmentQA : EditorWindow
{
    private const string Menu = "Tools/MonsterSurvival/7D Equipment Sets/";
    private const float Epsilon = 0.0008f;
    private static readonly string[] ManualItems = {
        "01 | Inventory / Equipment opens; SETS is readable; no overlapping USE/EQUIP/UNEQUIP controls",
        "02 | Set shows correct REAL IDs and 0/3 -> 1/3 -> 2/3 -> 3/3",
        "03 | Removing 1 item: 3/3 -> 2/3 removes ONLY own ASPD set bonus",
        "04 | Removing another: 2/3 -> 1/3 removes ONLY own Damage set bonus",
        "05 | Re-equipping: bonuses return once; no accumulation after waiting / reopening UI",
        "06 | Swap weapon/armor/accessory for different ID: set tier and stats update correctly",
        "07 | Original 3D.2/3D.3 gear bonuses remain correct after unequip and re-equip",
        "08 | Level-up 7C skills still modify stats correctly while a set is equipped",
        "09 | Permanent 5B upgrades remain correct, without overwriting or double counting",
        "10 | Victory, Game Over, Pause, Retry remain correct; no unwanted save on defeat",
        "11 | Profile 01/02/03 data remains isolated; reload equipment restores exactly once",
        "12 | 1920x1080, 1280x720, 1024x768, small Game View: SETS UI readable",
        "13 | Boss follow HP unchanged; Shadow Titan base balance remains at intended 500 HP",
        "14 | Save/Gold/Inventory 20 slots and 3 equipped slots unchanged by this QA"
    };

    private sealed class SetProgress
    {
        public string id;
        public string title;
        public int total;
        public int mask;
        public int transitions;
        public int maxObserved;
        public int lastCount = -1;
        public bool sawTwoApplied;
        public bool sawThreeApplied;
        public bool sawDropThreeToTwo;
        public bool sawDropTwoToOne;
        public bool pendingDropThreeToTwo;
        public bool pendingDropTwoToOne;
    }

    private readonly Dictionary<string, SetProgress> progress =
        new Dictionary<string, SetProgress>(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> events = new List<string>();
    private readonly bool[] manual = new bool[ManualItems.Length];
    private string auditText = "Not audited yet";
    private string lastSnapshot = "No Play Mode capture yet";
    private Vector2 scroll;
    private bool capturing;
    private int samples;
    private int stableCorrect;
    private int stableMismatch;
    private int readFailures;
    private int appliedTransitionCount;
    private string lastStableSignature = "";
    private int stableGearSamples;
    private float previousAppliedDamage;
    private float previousAppliedAttack;
    private double nextPoll;
    private bool previousSamplePresent;
    private string previousSignature = "";
    private string lastDiagnostic = "";
    private int loggedEvents;
    private string captureScene = "";
    private DateTime started;
    private DateTime stopped;

    [MenuItem(Menu + "21 - Open 7D.4 Final Equipment QA")]
    public static void Open()
    {
        MS7D4FinalEquipmentQA w = GetWindow<MS7D4FinalEquipmentQA>("7D.4 Final Equipment QA");
        w.minSize = new Vector2(650, 475);
        w.Show();
    }

    [MenuItem(Menu + "22 - Audit 7D.4 Equipment Integration (READ ONLY)")]
    public static void MenuAudit()
    {
        MS7D4FinalEquipmentQA w = GetWindow<MS7D4FinalEquipmentQA>("7D.4 Final Equipment QA");
        w.Audit();
        Debug.Log(w.auditText);
    }

    [MenuItem(Menu + "23 - Export 7D.4 Report as TXT")]
    public static void MenuExport()
    {
        MS7D4FinalEquipmentQA w = GetWindow<MS7D4FinalEquipmentQA>("7D.4 Final Equipment QA");
        w.Export();
    }

    [MenuItem(Menu + "24 - Run 7D.4 Pure Formula Tests")]
    public static void MenuFormulas()
    {
        string result = PureTests();
        Debug.Log(result);
    }

    private void OnEnable()
    {
        EditorApplication.update -= EditorTick;
        EditorApplication.update += EditorTick;
    }

    private void OnDisable()
    {
        capturing = false;
        EditorApplication.update -= EditorTick;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("7D.4 — FINAL EQUIPMENT SET QA + BALANCE", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Read-only dashboard. No Scene/Prefab/Save writes. It does NOT tick 7D.2 bonus gates, equip items, " +
            "change PlayerStats, or certify gameplay that you did not test.", MessageType.Info);
        EditorGUILayout.LabelField("Scene: " + SceneManager.GetActiveScene().name +
                                   "  |  Play Mode: " + EditorApplication.isPlaying +
                                   "  |  Capture: " + (capturing ? "RUNNING" : "STOPPED"));
        EditorGUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("1 - AUDIT SCENE / RULES / REFS (READ ONLY)", GUILayout.Height(32))) Audit();
        if (GUILayout.Button("2 - PURE FORMULA TESTS", GUILayout.Height(32))) Debug.Log(PureTests());
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying || capturing);
        if (GUILayout.Button("3 - START LIVE CAPTURE", GUILayout.Height(34))) StartCapture();
        EditorGUI.EndDisabledGroup();
        EditorGUI.BeginDisabledGroup(!capturing);
        if (GUILayout.Button("4 - STOP CAPTURE", GUILayout.Height(34))) StopCapture();
        EditorGUI.EndDisabledGroup();
        if (GUILayout.Button("5 - EXPORT TXT REPORT", GUILayout.Height(34))) Export();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField("Samples: " + samples + "  |  stable matches: " + stableCorrect +
                                   "  |  stable mismatches: " + stableMismatch +
                                   "  |  read failures: " + readFailures +
                                   "  |  bonus transitions: " + appliedTransitionCount);
        EditorGUILayout.HelpBox("If you are at 2/3 with bonus OFF, that is a PREVIEW success, NOT a final release PASS. " +
            "Use real gear to test 0/1/2/3 pieces, then enable existing 7D.2 safety gates ONLY after verifying the mapping.", MessageType.Warning);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("STRUCTURAL AUDIT", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(auditText, GUILayout.MinHeight(120));
        EditorGUILayout.Space(7);
        EditorGUILayout.LabelField("LIVE CAPTURE SNAPSHOT", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(lastSnapshot, GUILayout.MinHeight(95));
        EditorGUILayout.Space(7);
        EditorGUILayout.LabelField("MANUAL QA (check only AFTER you really test)", EditorStyles.boldLabel);
        for (int i = 0; i < ManualItems.Length; i++) manual[i] = EditorGUILayout.ToggleLeft(ManualItems[i], manual[i]);
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("RECENT EVENTS (max 180 retained)", EditorStyles.boldLabel);
        for (int i = Math.Max(0, events.Count - 20); i < events.Count; i++)
            EditorGUILayout.LabelField(events[i], EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.EndScrollView();
    }

    private static bool SceneComponent(Component c)
    {
        return c != null && !EditorUtility.IsPersistent(c) && c.gameObject.scene.IsValid() &&
               c.gameObject.scene.isLoaded && c.gameObject.scene == SceneManager.GetActiveScene();
    }

    private static List<T> SceneObjects<T>() where T : Component
    {
        var result = new List<T>();
        foreach (T c in Resources.FindObjectsOfTypeAll<T>()) if (SceneComponent(c)) result.Add(c);
        return result;
    }

    private static T One<T>() where T : Component
    {
        List<T> list = SceneObjects<T>();
        return list.Count == 1 ? list[0] : null;
    }

    private void Audit()
    {
        int pass = 0, warn = 0, error = 0;
        var report = new StringBuilder();
        report.AppendLine("MONSTERSURVIVAL 7D.4 STRUCTURAL AUDIT (READ ONLY)");
        report.AppendLine("Unity " + Application.unityVersion + " | " + SceneManager.GetActiveScene().name +
                          " | PlayMode=" + EditorApplication.isPlaying);
        PlayerStats stats = InspectOne<PlayerStats>("PlayerStats", report, ref pass, ref error);
        MS3DPlayerEquipment gear = InspectOne<MS3DPlayerEquipment>("3D.1 PlayerEquipment", report, ref pass, ref error);
        MS7D1EquippedItemReader reader = InspectOne<MS7D1EquippedItemReader>("7D.1 Reader", report, ref pass, ref error);
        MS7D1EquipmentSetRegistry registry = InspectOne<MS7D1EquipmentSetRegistry>("7D.1 Registry", report, ref pass, ref error);
        MS7D2SetBonusApplier applier = InspectOne<MS7D2SetBonusApplier>("7D.2 Applier", report, ref pass, ref error);
        MS7D3EquipmentSetsUI ui = InspectOne<MS7D3EquipmentSetsUI>("7D.3 SETS UI", report, ref pass, ref error);
        if (stats != null && gear != null && reader != null && registry != null && applier != null && ui != null)
        {
            Check(gear.gameObject == stats.gameObject && reader.gameObject == stats.gameObject &&
                  registry.gameObject == stats.gameObject && applier.gameObject == stats.gameObject,
                  "One Player owns Equipment/Reader/Registry/Applier/Stats", report, ref pass, ref error);
            Check(reader.equipmentSource == gear && registry.reader == reader &&
                  applier.playerStats == stats && applier.equipmentReader == reader &&
                  applier.setRegistry == registry, "Equipment -> Reader -> Registry -> Applier links", report, ref pass, ref error);
            Check(ui.playerEquipment == gear && ui.equippedReader == reader &&
                  ui.setRegistry == registry && ui.bonusApplier == applier,
                  "7D.3 SETS UI references", report, ref pass, ref error);
            Check(reader.slot1Path == "weapon" && reader.slot2Path == "armor" &&
                  reader.slot3Path == "accessory" && reader.itemIdPath == "itemId",
                  "Exact 3D.1 slot paths + MS3AItemData.itemId", report, ref pass, ref error);
            string[] equipped; string diag;
            bool readOk = reader.TryRead(out equipped, out diag);
            Check(readOk && equipped != null && equipped.Length == 3,
                  "Reader can read 3 slots", report, ref pass, ref error);
            report.AppendLine("[INFO] Reader: " + diag);
            if (readOk && equipped != null && equipped.Length == 3)
            {
                Check(ActualId(gear, MS3DEquipmentType.Weapon) == Safe(equipped[0]) &&
                      ActualId(gear, MS3DEquipmentType.Armor) == Safe(equipped[1]) &&
                      ActualId(gear, MS3DEquipmentType.Accessory) == Safe(equipped[2]),
                      "Reader IDs exactly equal 3D.1 GetEquipped IDs", report, ref pass, ref error);
            }
            List<MS7D1EquipmentSetRegistry.Status> statuses;
            string preview;
            bool previewOk = registry.TryPreview(out statuses, out preview);
            Check(previewOk, "Registry TryPreview readable", report, ref pass, ref error);
            if (previewOk)
            {
                report.AppendLine("[INFO] Active set rules=" + statuses.Count + " | configured raw count=" +
                                  (registry.sets != null ? registry.sets.Count : 0));
                if (statuses.Count == 0)
                {
                    report.AppendLine("[WARN] No enabled VALID set rules. Create rules first.");
                    warn++;
                }
                foreach (MS7D1EquipmentSetRegistry.Status status in statuses)
                    report.AppendLine("[SET] " + status.ToString());
                if (registry.sets != null)
                {
                    var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var sharedPieces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (MS7D1EquipmentSetRegistry.Rule rule in registry.sets)
                    {
                        if (rule == null || !rule.enabledRule) continue;
                        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        int repeats = 0; int shared = 0;
                        if (rule.pieceIds != null)
                        {
                            foreach (string piece in rule.pieceIds)
                            {
                                if (string.IsNullOrWhiteSpace(piece)) continue;
                                string trimmed = piece.Trim();
                                if (!ids.Add(trimmed)) repeats++;
                                else if (!sharedPieces.Add(trimmed)) shared++;
                            }
                        }
                        bool idValid = !string.IsNullOrWhiteSpace(rule.setId) && names.Add(rule.setId.Trim());
                        bool bonusValid = !float.IsNaN(rule.damageBonusAtTwo) && !float.IsInfinity(rule.damageBonusAtTwo) &&
                                          !float.IsNaN(rule.attackSpeedBonusAtThree) && !float.IsInfinity(rule.attackSpeedBonusAtThree) &&
                                          rule.damageBonusAtTwo >= 0f && rule.attackSpeedBonusAtThree >= 0f;
                        bool validRule = idValid && ids.Count >= 2 && ids.Count <= 3 && repeats == 0 && bonusValid &&
                                         (!applier.prohibitPiecesSharedBetweenSets || shared == 0);
                        if (validRule) { pass++; report.AppendLine("[PASS] SET RULE " + rule.setId + " : " + ids.Count + " unique IDs"); }
                        else { error++; report.AppendLine("[ERROR] Invalid rule " + rule.setId + ": IDs=" + ids.Count +
                            " duplicates=" + repeats + " shared=" + shared + " bonusValid=" + bonusValid); }
                        report.AppendLine("[BALANCE] " + rule.displayName + " | 2-piece DMG +" +
                            rule.damageBonusAtTwo.ToString("0.###") + " | 3-piece ASPD +" +
                            rule.attackSpeedBonusAtThree.ToString("0.###") +
                            " (additive multiplier points; NOT guaranteed final output %)");
                        if (rule.damageBonusAtTwo > applier.maxTotalDamageBonus ||
                            rule.attackSpeedBonusAtThree > applier.maxTotalAttackSpeedBonus)
                        {
                            warn++;
                            report.AppendLine("[WARN] One rule exceeds Applier total safety caps; adjust rules/caps after balancing.");
                        }
                    }
                }
            }
            float wantD, wantA; string evaluate;
            bool valid = applier.TryEvaluate(out wantD, out wantA, out evaluate);
            report.AppendLine("[INFO] Evaluation=" + valid + " | " + evaluate);
            report.AppendLine("[INFO] Gate mappingVerified=" + applier.mappingVerifiedByUser +
                              "; actualBonusesEnabled=" + applier.enableActualBonuses +
                              "; current own contribution DMG=" + applier.AppliedDamageBonus.ToString("0.####") +
                              " ASPD=" + applier.AppliedAttackSpeedBonus.ToString("0.####"));
            if (!valid) { warn++; report.AppendLine("[WARN] Bonus rules not evaluable: " + evaluate); }
            else
            {
                pass++;
                report.AppendLine("[PASS] Bonus rules evaluate: wanted DMG +" + wantD.ToString("0.####") +
                                  "; ASPD +" + wantA.ToString("0.####"));
            }
            float rawDamage, rawAttack;
            bool rawOK = TryStatsFields(applier, out rawDamage, out rawAttack);
            if (!rawOK) { warn++; report.AppendLine("[WARN] PlayerStats raw multiplier fields unavailable"); }
            else report.AppendLine("[INFO] Stats raw: DMG=" + rawDamage.ToString("0.####") +
                                   ", ASPD=" + rawAttack.ToString("0.####") +
                                   " (includes other systems; these are NOT set-only stats)");
            if (!EditorApplication.isPlaying)
            {
                warn++; report.AppendLine("[WARN] Edit Mode cannot verify runtime set application / unequip reversal");
            }
        }
        report.AppendLine("RESULT PASS=" + pass + " WARN=" + warn + " ERROR=" + error);
        auditText = report.ToString();
        Repaint();
    }

    private static T InspectOne<T>(string title, StringBuilder sb, ref int pass, ref int error) where T : Component
    {
        List<T> all = SceneObjects<T>();
        bool ok = all.Count == 1;
        Check(ok, title + " exactly one (found " + all.Count + ")", sb, ref pass, ref error);
        return ok ? all[0] : null;
    }

    private static void Check(bool ok, string message, StringBuilder sb, ref int pass, ref int error)
    {
        if (ok) { pass++; sb.AppendLine("[PASS] " + message); }
        else { error++; sb.AppendLine("[ERROR] " + message); }
    }

    private static string ActualId(MS3DPlayerEquipment gear, MS3DEquipmentType slot)
    {
        MS3AItemData item = gear.GetEquipped(slot);
        return item == null ? "" : Safe(item.itemId);
    }

    private static string Safe(string s) { return string.IsNullOrWhiteSpace(s) ? "" : s.Trim(); }

    private static bool TryStatsFields(MS7D2SetBonusApplier applier, out float damage, out float attack)
    {
        damage = attack = 0f;
        if (applier == null || applier.playerStats == null) return false;
        FieldInfo d = FindField(applier.playerStats.GetType(), applier.damageMultiplierField);
        FieldInfo a = FindField(applier.playerStats.GetType(), applier.attackSpeedMultiplierField);
        if (d == null || a == null || d == a) return false;
        try
        {
            damage = (float)d.GetValue(applier.playerStats);
            attack = (float)a.GetValue(applier.playerStats);
            return !(float.IsNaN(damage) || float.IsNaN(attack));
        }
        catch { return false; }
    }

    private static FieldInfo FindField(Type t, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public |
                                   BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (Type p = t; p != null; p = p.BaseType)
        {
            FieldInfo f = p.GetField(name, flags);
            if (f != null) return f.FieldType == typeof(float) ? f : null;
        }
        return null;
    }

    private void StartCapture()
    {
        if (!EditorApplication.isPlaying) { Debug.LogWarning("[7D.4] Start Capture requires Play Mode."); return; }
        capturing = true;
        samples = stableCorrect = stableMismatch = readFailures = appliedTransitionCount = 0;
        stableGearSamples = 0;
        lastStableSignature = "";
        loggedEvents = 0;
        previousSamplePresent = false;
        previousSignature = "";
        previousAppliedDamage = previousAppliedAttack = 0f;
        events.Clear(); progress.Clear();
        nextPoll = 0;
        captureScene = SceneManager.GetActiveScene().name;
        started = DateTime.Now;
        stopped = DateTime.MinValue;
        lastDiagnostic = "Capture started; equip/unequip manually in Play Mode.";
        AddEvent("START LIVE CAPTURE — no game state modifications");
        Sample();
    }

    private void StopCapture()
    {
        if (!capturing) return;
        capturing = false;
        stopped = DateTime.Now;
        AddEvent("STOP LIVE CAPTURE, samples=" + samples);
        BuildSnapshot();
    }

    private void EditorTick()
    {
        if (!capturing) return;
        if (!EditorApplication.isPlaying)
        {
            StopCapture();
            return;
        }
        if (EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + .4d;
        Sample();
    }

    private void Sample()
    {
        if (!EditorApplication.isPlaying) return;
        MS3DPlayerEquipment gear = One<MS3DPlayerEquipment>();
        MS7D1EquippedItemReader reader = One<MS7D1EquippedItemReader>();
        MS7D1EquipmentSetRegistry registry = One<MS7D1EquipmentSetRegistry>();
        MS7D2SetBonusApplier applier = One<MS7D2SetBonusApplier>();
        if (gear == null || reader == null || registry == null || applier == null)
        {
            readFailures++;
            lastDiagnostic = "Missing/duplicate Equipment, Reader, Registry or Applier in active Scene";
            if (readFailures == 1) AddEvent("READ FAILURE: " + lastDiagnostic);
            return;
        }
        string[] ids; string adapter = "";
        List<MS7D1EquipmentSetRegistry.Status> statuses; string preview = "";
        if (!reader.TryRead(out ids, out adapter) || !registry.TryPreview(out statuses, out preview) ||
            ids == null || ids.Length != 3)
        {
            readFailures++;
            lastDiagnostic = "Reader/Registry failed: " + adapter + " | " + preview;
            if (readFailures <= 3) AddEvent("READ FAILURE: " + lastDiagnostic);
            return;
        }
        samples++;
        float wantedD, wantedA; string evaluate;
        bool evaluable = applier.TryEvaluate(out wantedD, out wantedA, out evaluate);
        float ownD = applier.AppliedDamageBonus;
        float ownA = applier.AppliedAttackSpeedBonus;
        float rawD, rawA;
        bool rawOk = TryStatsFields(applier, out rawD, out rawA);
        bool gateOn = applier.mappingVerifiedByUser && applier.enableActualBonuses && applier.isActiveAndEnabled;
        float expectedD = gateOn && evaluable ? wantedD : 0f;
        float expectedA = gateOn && evaluable ? wantedA : 0f;
        string sig = Safe(ids[0]) + "/" + Safe(ids[1]) + "/" + Safe(ids[2]) +
                     " | gates=" + gateOn + " | valid=" + evaluable;
        if (sig == lastStableSignature) stableGearSamples++;
        else { stableGearSamples = 1; lastStableSignature = sig; }
        // Ignore brief transients immediately after equip / gate transitions;
        // 7D.2 ticks at its own configurable poll interval.
        int stableThreshold = Mathf.Max(4, Mathf.CeilToInt((applier.pollInterval + 0.4f) / 0.4f) + 1);
        if (stableGearSamples >= stableThreshold && evaluable && rawOk)
        {
            if (Mathf.Abs(expectedD - ownD) < Epsilon && Mathf.Abs(expectedA - ownA) < Epsilon)
                stableCorrect++;
            else
            {
                stableMismatch++;
                if (stableMismatch <= 8) AddEvent("STABLE MISMATCH expected DMG=" + expectedD.ToString("0.####") +
                    ", ASPD=" + expectedA.ToString("0.####") + " | own DMG=" + ownD.ToString("0.####") +
                    ", ASPD=" + ownA.ToString("0.####") + " | diagnostic=" + applier.LastDiagnostic);
            }
        }
        foreach (MS7D1EquipmentSetRegistry.Status status in statuses)
        {
            SetProgress p;
            if (!progress.TryGetValue(status.id, out p))
            {
                p = new SetProgress();
                p.id = status.id; p.title = status.name; p.total = status.total;
                progress.Add(status.id, p);
            }
            int count = Mathf.Clamp(status.matched, 0, 3);
            p.mask |= 1 << count;
            p.maxObserved = Mathf.Max(p.maxObserved, count);
            if (p.lastCount >= 0 && p.lastCount != count)
            {
                p.transitions++;
                AddEvent("SET " + status.name + " : " + p.lastCount + "/" + status.total + " -> " +
                         count + "/" + status.total);
                if (p.lastCount == 3 && count == 2 && gateOn) p.pendingDropThreeToTwo = true;
                if (p.lastCount == 2 && count == 1 && gateOn) p.pendingDropTwoToOne = true;
            }
            p.lastCount = count;
            bool stable = stableGearSamples >= stableThreshold && evaluable && rawOk &&
                          Mathf.Abs(expectedD - ownD) < Epsilon &&
                          Mathf.Abs(expectedA - ownA) < Epsilon;
            if (stable && count == 2 && gateOn && ownD > Epsilon && ownA <= Epsilon) p.sawTwoApplied = true;
            if (stable && count == 3 && gateOn && ownD > Epsilon && ownA > Epsilon) p.sawThreeApplied = true;
            if (stable && p.pendingDropThreeToTwo && count == 2 && gateOn &&
                ownA <= Epsilon && ownD > Epsilon) { p.sawDropThreeToTwo = true; p.pendingDropThreeToTwo = false; }
            if (stable && p.pendingDropTwoToOne && count == 1 && gateOn &&
                ownD <= Epsilon && ownA <= Epsilon) { p.sawDropTwoToOne = true; p.pendingDropTwoToOne = false; }
        }
        if (previousSamplePresent && (Mathf.Abs(ownD - previousAppliedDamage) > Epsilon ||
                                     Mathf.Abs(ownA - previousAppliedAttack) > Epsilon))
        {
            appliedTransitionCount++;
            AddEvent("OWN BONUS changed: DMG " + previousAppliedDamage.ToString("0.####") +
                " -> " + ownD.ToString("0.####") + "; ASPD " + previousAppliedAttack.ToString("0.####") +
                " -> " + ownA.ToString("0.####"));
        }
        if (sig != previousSignature)
        {
            AddEvent("EQUIPMENT " + sig + " | wanted "+ wantedD.ToString("0.####") +
                     "/" + wantedA.ToString("0.####") + " | applied " + ownD.ToString("0.####") +
                     "/" + ownA.ToString("0.####"));
            previousSignature = sig;
        }
        previousAppliedDamage = ownD;
        previousAppliedAttack = ownA;
        previousSamplePresent = true;
        lastDiagnostic = "IDs=" + Safe(ids[0]) + "," + Safe(ids[1]) + "," + Safe(ids[2]) +
                         " | wanted DMG/ASPD=" + wantedD.ToString("0.####") + "/" + wantedA.ToString("0.####") +
                         " | own=" + ownD.ToString("0.####") + "/" + ownA.ToString("0.####") +
                         " | raw=" + (rawOk ? rawD.ToString("0.####") + "/" + rawA.ToString("0.####") : "unavailable") +
                         " | " + applier.LastDiagnostic;
        if (samples % 4 == 0) BuildSnapshot();
        Repaint();
    }

    private void AddEvent(string message)
    {
        if (events.Count >= 180) events.RemoveAt(0);
        events.Add(DateTime.Now.ToString("HH:mm:ss") + " | " + message);
        loggedEvents++;
    }

    private void BuildSnapshot()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Scene=" + captureScene + " | samples=" + samples + " | events=" + loggedEvents);
        sb.AppendLine("Stable comparison: matched=" + stableCorrect + " mismatched=" + stableMismatch +
                      " (temporary equip transitions ignored; NOT a damage output test)");
        sb.AppendLine("Read failures=" + readFailures + " | own bonus transitions=" + appliedTransitionCount);
        foreach (KeyValuePair<string, SetProgress> kv in progress)
        {
            SetProgress p = kv.Value;
            sb.AppendLine(p.title + " | observed stages: " + ObservedStages(p.mask) +
                " | max=" + p.maxObserved + "/" + p.total + " | transitions=" + p.transitions +
                " | 2-piece applied=" + p.sawTwoApplied + " | 3-piece applied=" + p.sawThreeApplied +
                " | 3->2 reversed=" + p.sawDropThreeToTwo + " | 2->1 reversed=" + p.sawDropTwoToOne);
        }
        sb.AppendLine("Current: " + lastDiagnostic);
        lastSnapshot = sb.ToString();
    }

    private static string ObservedStages(int mask)
    {
        var sb = new StringBuilder();
        for (int i = 0; i <= 3; i++) if ((mask & (1 << i)) != 0)
            sb.Append(i).Append("/3 ");
        return sb.Length == 0 ? "none" : sb.ToString().Trim();
    }

    private static string PureTests()
    {
        int pass = 0, total = 0;
        var sb = new StringBuilder("MONSTERSURVIVAL 7D.4 PURE FORMULA TESTS (NO SCENE WRITES)\n");
        Action<bool, string> check = delegate(bool ok, string description)
        {
            total++;
            if (ok) pass++;
            sb.AppendLine((ok ? "[PASS] " : "[FAIL] ") + description);
        };
        string[] required = { "reaper_edge_3d3", "soulguard_plate_3d3", "warden_oath_3d4" };
        check(MS7D1EquipmentSetRegistry.CountMatched(required, new string[] { "", "", "" }) == 0, "Empty=0 pieces");
        check(MS7D1EquipmentSetRegistry.CountMatched(required, new string[] { required[0], "", "" }) == 1, "Weapon only=1 piece");
        check(MS7D1EquipmentSetRegistry.CountMatched(required, new string[] { required[0], required[1], "" }) == 2, "Weapon+Armor=2 pieces");
        check(MS7D1EquipmentSetRegistry.CountMatched(required, required) == 3, "All 3 = 3 pieces");
        check(MS7D1EquipmentSetRegistry.CountMatched(required, new string[] { "HOLLOW_BOW_3D3", required[1], required[2] }) == 2, "Different weapon not counted");
        check(MS7D1EquipmentSetRegistry.CountMatched(required, new string[] { required[0], required[0], required[2] }) == 2, "Duplicated item ID counts once");
        check(MS7D1EquipmentSetRegistry.CountMatched(required, new string[] { "REAPER_EDGE_3D3", required[1], required[2] }) == 3, "ID comparison ignores case");
        float withBonus = MS7D2SetBonusApplier.ReplaceContribution(1.20f, 0f, .08f);
        check(Mathf.Abs(withBonus - 1.28f) < .00001f, "Apply +0.08 to existing 1.20");
        float noReapply = MS7D2SetBonusApplier.ReplaceContribution(withBonus, .08f, .08f);
        check(Mathf.Abs(noReapply - 1.28f) < .00001f, "Repeated same contribution is idempotent");
        check(Mathf.Abs(MS7D2SetBonusApplier.ReplaceContribution(1.28f, .08f, 0f) - 1.20f) < .00001f,
            "Remove own damage preserves external base 1.20");
        check(Mathf.Abs(MS7D2SetBonusApplier.ReplaceContribution(1.52f, .12f, 0f) - 1.40f) < .00001f,
            "Remove own attack preserves external base 1.40");
        check(Mathf.Abs(MS7D2SetBonusApplier.ReplaceContribution(1.31f, .08f, .08f) - 1.31f) < .00001f,
            "Other system increased base while own contribution remained stable");
        sb.AppendLine("RESULT: " + pass + "/" + total + " PASS | Pure conditions only; does NOT prove runtime");
        return sb.ToString();
    }

    private void Export()
    {
        if (string.IsNullOrEmpty(auditText) || auditText == "Not audited yet") Audit();
        BuildSnapshot();
        int userChecked = 0;
        for (int i = 0; i < manual.Length; i++) if (manual[i]) userChecked++;
        bool fullStages = false;
        bool bonusObserved = false;
        bool reversalObserved = false;
        foreach (SetProgress p in progress.Values)
        {
            if (p.total != 3) continue;
            if ((p.mask & 0x0F) == 0x0F) fullStages = true;
            if (p.sawTwoApplied && p.sawThreeApplied) bonusObserved = true;
            if (p.sawDropThreeToTwo && p.sawDropTwoToOne) reversalObserved = true;
        }
        bool structural = !auditText.Contains("[ERROR]") && !auditText.Contains("[WARN]");
        // Never claim gameplay success merely from a read-only snapshot.
        bool evidence = structural && samples > 0 && fullStages && bonusObserved &&
                        reversalObserved && stableCorrect >= 3 && stableMismatch == 0 && readFailures == 0;
        bool gate = evidence && userChecked == manual.Length;
        var sb = new StringBuilder();
        sb.AppendLine("MONSTERSURVIVAL 7D.4 - FINAL EQUIPMENT SET QA / READ ONLY");
        sb.AppendLine("Generated=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
                      " | Unity=" + Application.unityVersion + " | Scene=" + SceneManager.GetActiveScene().name);
        sb.AppendLine("No Scene/Prefab/Save/PlayerStats data modified by this QA tool.");
        sb.AppendLine("This report cannot prove untested gameplay; manual checkboxes reflect user confirmation only.");
        sb.AppendLine("=== STRUCTURAL AUDIT ===\n" + auditText);
        sb.AppendLine("=== PLAY MODE CAPTURE ===");
        sb.AppendLine("Started=" + (started == DateTime.MinValue ? "not started" : started.ToString("yyyy-MM-dd HH:mm:ss")) +
                      " | stopped=" + (stopped == DateTime.MinValue ? "not stopped" : stopped.ToString("yyyy-MM-dd HH:mm:ss")));
        sb.AppendLine(lastSnapshot);
        sb.AppendLine("=== OBSERVATION FLAGS ===");
        sb.AppendLine("ALL_FOUR_STAGES_ONE_3PIECE_SET=" + fullStages);
        sb.AppendLine("TWO_AND_THREE_PIECE_BONUS_OBSERVED=" + bonusObserved);
        sb.AppendLine("3_TO_2_AND_2_TO_1_REVERSALS_OBSERVED=" + reversalObserved);
        sb.AppendLine("STABLE_CONTRIBUTION_MATCHES=" + stableCorrect + " | MISMATCHES=" + stableMismatch);
        sb.AppendLine("=== MANUAL CONFIRMATIONS ===");
        for (int i = 0; i < ManualItems.Length; i++)
            sb.AppendLine((manual[i] ? "[USER CONFIRMED] " : "[NOT CONFIRMED] ") + ManualItems[i]);
        sb.AppendLine("MANUAL=" + userChecked + "/" + manual.Length);
        sb.AppendLine("=== RELEASE GATE ===");
        sb.AppendLine("STRUCTURE_AND_LIVE_EVIDENCE=" + (evidence ? "PASS" : "PENDING"));
        sb.AppendLine("MANUAL_VERIFICATION=" + (userChecked == manual.Length ? "CONFIRMED" : "PENDING"));
        sb.AppendLine("RESULT=" + (gate ? "READY FOR USER REVIEW (not an automatic certification)" :
                                   "PENDING - DO NOT MARK 7D FINAL QA COMPLETE"));
        sb.AppendLine("=== EVENT TRACE ===");
        foreach (string line in events) sb.AppendLine(line);
        string dest = EditorUtility.SaveFilePanel("Export 7D.4 QA report", "",
            "MS7D4_EquipmentFinalQA_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt", "txt");
        if (string.IsNullOrEmpty(dest)) return;
        try
        {
            File.WriteAllText(dest, sb.ToString(), new UTF8Encoding(false));
            Debug.Log("[7D.4] Wrote read-only QA report to " + dest + " | " + (gate ? "READY FOR REVIEW" : "PENDING"));
            EditorUtility.RevealInFinder(dest);
        }
        catch (Exception e)
        {
            Debug.LogError("[7D.4] Export failed: " + e.Message);
        }
    }
}
#endif
