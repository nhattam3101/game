using System;
using UnityEngine;

// 7C.1: read-only unlock detection for NEW synergies.
// The three older synergies (Frozen Burst / Phantom Chain / Rapid Blast)
// are deliberately not recreated, overridden or changed here.
[DisallowMultipleComponent]
public sealed class MS7C1SynergyCore : MonoBehaviour
{
    [Header("Existing systems (assigned by 7C.1 Installer)")]
    public SkillManager skillManager;
    public MS7B2ElementalSkillCoordinator elementalCards;
    public MS7B1ElementLoadout loadout;

    [Header("Monitoring (no gameplay buffs in 7C.1)")]
    [Min(0.1f)] public float refreshSeconds = 0.25f;
    public bool logNewUnlocks = true;

    [Header("Detected only, never serialized to Save")]
    [SerializeField] private bool wildfireConduit;
    [SerializeField] private bool glacialRebound;
    [SerializeField] private bool thunderPrecision;
    private float nextRefresh;

    // Stable, NEW registry identifiers. These are NOT the old 2C IDs.
    public const string WildfireId = "7c1.wildfire_conduit";
    public const string GlacialId = "7c1.glacial_rebound";
    public const string ThunderId = "7c1.thunder_precision";

    public bool WildfireConduitUnlocked { get { return wildfireConduit; } }
    public bool GlacialReboundUnlocked { get { return glacialRebound; } }
    public bool ThunderPrecisionUnlocked { get { return thunderPrecision; } }
    public int UnlockedCount { get { return (wildfireConduit ? 1 : 0) + (glacialRebound ? 1 : 0) + (thunderPrecision ? 1 : 0); } }

    // Event is for UI and future 7C.2 listeners; never adds buffs or rewards.
    public event Action<string> OnNewSynergyUnlocked;

    private void Awake()
    {
        DiscoverReferences();
        RefreshNow(false);
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + Mathf.Max(0.1f, refreshSeconds);
        RefreshNow(true);
    }

    public void DiscoverReferences()
    {
        if (skillManager == null) skillManager = FindFirstObjectByType<SkillManager>();
        if (elementalCards == null && skillManager != null)
            elementalCards = skillManager.GetComponent<MS7B2ElementalSkillCoordinator>();
        if (loadout == null) loadout = GetComponent<MS7B1ElementLoadout>();
    }

    public int GetLevel(SkillData skill)
    {
        return skillManager != null && skill != null ? skillManager.GetSkillLevel(skill) : 0;
    }

    // Some old skills may exist as multiple SkillData assets, so check all owned entries.
    public bool OwnsLegacySkill(SkillType type)
    {
        if (skillManager == null || skillManager.allSkills == null) return false;
        foreach (SkillData skill in skillManager.allSkills)
            if (skill != null && skill.skillType == type && skillManager.GetSkillLevel(skill) > 0)
                return true;
        return false;
    }

    // Pure helpers shared with the 7C.1 Editor formula-only tests.
    public static bool HasWildfire(int fireLevel, int lightningLevel) { return fireLevel > 0 && lightningLevel > 0; }
    public static bool HasGlacial(int iceLevel, bool ownsRicochet) { return iceLevel > 0 && ownsRicochet; }
    public static bool HasThunder(int lightningLevel, bool ownsCritical) { return lightningLevel > 0 && ownsCritical; }

    public void RefreshNow(bool emitEvents)
    {
        DiscoverReferences();
        if (skillManager == null || elementalCards == null) return;

        bool wildfire = HasWildfire(GetLevel(elementalCards.fireSkill), GetLevel(elementalCards.lightningSkill));
        bool glacial = HasGlacial(GetLevel(elementalCards.iceSkill), OwnsLegacySkill(SkillType.RicochetShot));
        bool thunder = HasThunder(GetLevel(elementalCards.lightningSkill), OwnsLegacySkill(SkillType.CriticalHit));

        SetUnlocked(ref wildfireConduit, wildfire, WildfireId, emitEvents);
        SetUnlocked(ref glacialRebound, glacial, GlacialId, emitEvents);
        SetUnlocked(ref thunderPrecision, thunder, ThunderId, emitEvents);
    }

    private void SetUnlocked(ref bool field, bool value, string id, bool emitEvents)
    {
        bool was = field;
        field = value;
        if (!was && value && emitEvents)
        {
            if (logNewUnlocks) Debug.Log("[7C.1] NEW synergy detected: " + id + " (no combat effect until 7C.2)", this);
            if (OnNewSynergyUnlocked != null) OnNewSynergyUnlocked(id);
        }
    }

    // Current element is NOT used to decide ownership/unlock. It is informative only.
    public bool IsPreferredElementEquipped(string id)
    {
        if (loadout == null) return false;
        if (id == WildfireId || id == ThunderId) return loadout.selectedElement == MS7B1ElementType.Lightning;
        if (id == GlacialId) return loadout.selectedElement == MS7B1ElementType.Ice;
        return false;
    }
}
