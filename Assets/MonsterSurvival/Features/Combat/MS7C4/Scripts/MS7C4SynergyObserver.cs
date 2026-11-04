using System;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 7C.4 read-only observer. Never grants skills, changes stats, publishes combo
/// state, modifies a UI button, writes Save, or changes gameplay objects.
/// Intended to diagnose WHY a combo is locked or not active in Play Mode.
/// Requires the existing 7C.3.3 system; place on the existing unified UI object.
/// </summary>
[DisallowMultipleComponent]
public sealed class MS7C4SynergyObserver : MonoBehaviour
{
    [Header("Existing scene components - read-only")]
    [SerializeField] private SkillManager skillManager;
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private MS7C3UnifiedSynergyUI unifiedUI;
    [SerializeField] private MS7C33LevelUpGuide levelUpGuide;

    [Header("Diagnostics - no persistent HUD")]
    [SerializeField] private bool logChangesInPlayMode = true;
    [SerializeField, Range(0.25f, 3f)] private float sampleEverySeconds = 0.5f;

    private float nextSample;
    private string previousSignature;
    private bool hadBaseline;
    private int samples;
    private int transitionEvents;

    private static readonly string[] CombatIds = {
        "", "", "",
        "7c1.wildfire_conduit", "7c1.glacial_rebound", "7c1.thunder_precision"
    };
    private static readonly string[] SourceFields = {
        "", "", "", "wildfireCombatSource", "glacialCombatSource", "thunderCombatSource"
    };
    private static readonly string[] MemberFields = {
        "", "", "", "wildfireActiveMember", "glacialActiveMember", "thunderActiveMember"
    };

    public void EditorAssign(SkillManager manager, PlayerStats stats,
        MS7C3UnifiedSynergyUI unified, MS7C33LevelUpGuide guide)
    {
        skillManager = manager;
        playerStats = stats;
        unifiedUI = unified;
        levelUpGuide = guide;
    }

    public bool EditorReferencesValid
    {
        get { return skillManager != null && playerStats != null &&
            unifiedUI != null && levelUpGuide != null; }
    }

    private void OnEnable()
    {
        previousSignature = null;
        hadBaseline = false;
        nextSample = 0f;
        samples = transitionEvents = 0;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextSample) return;
        nextSample = Time.unscaledTime + sampleEverySeconds;
        if (skillManager == null || playerStats == null) return;
        samples++;
        string signature = Signature();
        if (!hadBaseline)
        {
            hadBaseline = true;
            previousSignature = signature;
            if (logChangesInPlayMode)
                Debug.Log("[7C.4] Baseline captured (not a gameplay PASS). " +
                    "Use Tools > MonsterSurvival > 7C.4 > 3 - Export Snapshot during Play Mode.", this);
            return;
        }
        if (signature == previousSignature) return;
        string prior = previousSignature;
        previousSignature = signature;
        transitionEvents++;
        if (logChangesInPlayMode)
            Debug.Log("[7C.4] COMBO/CHOICE CHANGE " + transitionEvents +
                " | from=" + prior + " | to=" + signature + "\n" + BuildReport(), this);
    }

    private string Signature()
    {
        var builder = new StringBuilder(160);
        for (int i = 0; i < MS7C33SynergyRules.Combos.Length; i++)
        {
            var combo = MS7C33SynergyRules.Combos[i];
            builder.Append(OwnedLevel(combo.First)).Append('/')
                .Append(OwnedLevel(combo.Second)).Append(':');
            builder.Append(IsCoreConfirmed(i) ? 'Y' : 'N');
            if (i < 3) builder.Append(LegacyActive(i) ? 'A' : 'I');
            else
            {
                bool currentActive;
                string source;
                bool statusKnown = TryReadNewActive(i, out currentActive, out source);
                builder.Append(!statusKnown ? '?' : currentActive ? 'R' : 'I');
                builder.Append(MS7C42SynergyStatus.Read(skillManager, playerStats, i).procs);
            }
            builder.Append(';');
        }
        bool selecting = IsSkillPanelOpen();
        builder.Append(selecting ? "SELECT" : "PLAY");
        if (selecting)
        {
            for (int slot = 0; slot < 3; slot++)
            {
                SkillData offered = MS7C33SynergyRules.GetOffered(skillManager, slot);
                builder.Append('|').Append(offered != null ? offered.GetEntityId().ToString() : "-");
            }
        }
        return builder.ToString();
    }

    private bool IsSkillPanelOpen()
    {
        return skillManager != null && skillManager.skillPanel != null &&
            skillManager.skillPanel.activeInHierarchy;
    }

    private int OwnedLevel(string name)
    {
        return MS7C33SynergyRules.OwnedLevel(skillManager, name);
    }

    private bool IsCoreConfirmed(int i)
    {
        if (i < 3) return MS7C33SynergyRules.IsActualLegacyUnlocked(playerStats, i);
        return MS7C42SynergyStatus.Read(skillManager, playerStats, i).coreUnlocked;
    }

    // "Ready" is a qualifying loadout, not proof of a proc. Only 7C.2's
    // runtime proc counters prove the hook recorded a combat event.
    private bool TryReadNewActive(int index, out bool isReady, out string sourceName)
    {
        MS7C42SynergyStatus.Snapshot snap = MS7C42SynergyStatus.Read(skillManager, playerStats, index);
        isReady = snap.ready;
        sourceName = snap.note + "; procs=" + snap.procs;
        return snap.combatFound;
    }

    private bool LegacyActive(int i)
    {
        if (playerStats == null || !IsCoreConfirmed(i)) return false;
        if (i == 0)
            return playerStats.activeProjectileEffect != null &&
                   playerStats.activeProjectileEffect.effectType == ProjectileEffectType.Explosion;
        if (i == 1) return playerStats.IsPhantomChainActive;
        return playerStats.IsRapidBlastActive;
    }

    public string BuildReport()
    {
        var report = new StringBuilder(3800);
        report.AppendLine("MONSTERSURVIVAL 7C.4 - RUNTIME SYNERGY SNAPSHOT (READ ONLY)");
        report.Append("Unity ").Append(Application.unityVersion).Append(" | Scene ")
            .Append(SceneManager.GetActiveScene().name)
            .Append(" | Play Mode ").Append(Application.isPlaying)
            .Append(" | Time ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
            .AppendLine();
        report.AppendLine("No gameplay modifications. An observed level pair is not proof of combat effects.");
        report.Append("Samples: ").Append(samples).Append(" | Changes: ")
            .Append(transitionEvents).Append(" | Level-up panel: ")
            .Append(IsSkillPanelOpen()).AppendLine();
        report.Append("References: SkillManager=").Append(skillManager != null)
            .Append(" PlayerStats=").Append(playerStats != null)
            .Append(" UnifiedUI=").Append(unifiedUI != null)
            .Append(" ChoiceGuide=").Append(levelUpGuide != null).AppendLine();
        if (skillManager == null || playerStats == null)
        {
            report.AppendLine("ERROR: Gameplay references missing. Run Installer in SampleScene.");
            return report.ToString();
        }
        for (int i = 0; i < MS7C33SynergyRules.Combos.Length; i++)
        {
            var combo = MS7C33SynergyRules.Combos[i];
            int a = OwnedLevel(combo.First), b = OwnedLevel(combo.Second);
            bool core = IsCoreConfirmed(i);
            string result;
            if (i < 3)
            {
                if (core) result = LegacyActive(i) ? "ACTIVE (legacy)" : "UNLOCKED / NOT ACTIVE (legacy)";
                else if (a > 0 && b > 0) result = "CORE ? - LEVEL PAIR OWNED, PlayerStats unlock FALSE";
                else result = "LOCKED";
            }
            else if (a == 0 || b == 0) result = "LOCKED (missing required skill)";
            else
            {
                bool active;
                string source;
                bool known = TryReadNewActive(i, out active, out source);
                MS7C42SynergyStatus.Snapshot snap = MS7C42SynergyStatus.Read(skillManager, playerStats, i);
                if (!snap.coreFound) result = "7C1 ? - core MISSING [" + snap.note + "]";
                else if (!snap.coreUnlocked) result = "CORE ? - pair owned but 7C.1 unlock FALSE";
                else if (!known) result = "7C2 ? - core UNLOCKED; combat component NOT CONNECTED [" + source + "]";
                else result = (active ? "READY (not a confirmed proc)" : "UNLOCKED / not armed") +
                    " | RECORDED PROCS=" + snap.procs + " [" + source + "]";
            }
            report.Append(i + 1).Append(". ").Append(combo.Title).Append(" | ")
                .Append(combo.First).Append(" Lv").Append(a).Append(" + ")
                .Append(combo.Second).Append(" Lv").Append(b)
                .Append(" | ").Append(result).AppendLine();
        }
        if (IsSkillPanelOpen())
        {
            report.AppendLine("--- LEVEL UP: ACTUAL THREE OFFERS ---");
            for (int slot = 0; slot < 3; slot++)
            {
                SkillData skill = MS7C33SynergyRules.GetOffered(skillManager, slot);
                report.Append("Choice ").Append(slot + 1).Append(": ");
                if (skill == null) { report.AppendLine("NOT AVAILABLE"); continue; }
                report.Append(skill.skillName).Append(" (type ")
                    .Append(skill.skillType).Append(")");
                for (int i = 0; i < MS7C33SynergyRules.Combos.Length; i++)
                {
                    bool complete;
                    if (MS7C33SynergyRules.OfferedHelps(skillManager, skill, i, out complete))
                        report.Append(" -> ").Append(MS7C33SynergyRules.Combos[i].Title)
                            .Append(complete ? " [COMPLETE]" : " [+1]");
                }
                report.AppendLine();
            }
        }
        report.AppendLine("Interpretation: CORE ? = unlock core pending; 7C2 ? = combat component absent/unlinked; READY is equipment armed, and only PROCS confirms a hook event. No combat impact is asserted by this report.");
        return report.ToString();
    }
}
