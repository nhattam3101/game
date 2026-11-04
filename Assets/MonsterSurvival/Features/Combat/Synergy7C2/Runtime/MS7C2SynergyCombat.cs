using UnityEngine;

// 7C.2: Combat bonuses for NEW combos only.
// Does not create SkillData, replace legacy Synergy, alter Save, or change direct bullet damage.
[DisallowMultipleComponent]
public sealed class MS7C2SynergyCombat : MonoBehaviour
{
    [Header("7C.1 prerequisite, on the Player")]
    public MS7C1SynergyCore core;

    [Header("Wildfire Conduit: Fire + Lightning OWNED, Lightning EQUIPPED")]
    [Range(0f, 1f)] public float wildfireBurnChance = .35f;
    [Range(.1f, 2f)] public float wildfireBurnDpsMultiplier = 1f;

    [Header("Glacial Rebound: Ice + Ricochet OWNED, Ice + Ricochet EQUIPPED")]
    [Range(0f, .30f)] public float glacialExtraSlow = .10f;
    [Range(.3f, .9f)] public float maxGlacialSlow = .75f;

    [Header("Thunder Precision: Lightning + Critical Hit OWNED, Critical BULLET")]
    [Range(1f, 2f)] public float thunderCriticalChainMultiplier = 1.35f;

    [Header("Debug / QA (run-only counters)")]
    public bool logTriggers;
    [SerializeField] private int wildfireProcs;
    [SerializeField] private int glacialHitBonuses;
    [SerializeField] private int thunderCriticalChains;
    private static MS7C2SynergyCombat active;

    public int WildfireProcs { get { return wildfireProcs; } }
    public int GlacialHitBonuses { get { return glacialHitBonuses; } }
    public int ThunderCriticalChains { get { return thunderCriticalChains; } }

    private void Awake()
    {
        if (core == null) core = GetComponent<MS7C1SynergyCore>();
        if (active != null && active != this)
        {
            Debug.LogError("[7C.2] Duplicate combat controller. Disable/remove the extra component.", this);
            enabled = false;
            return;
        }
        active = this;
    }

    public static MS7C2SynergyCombat Find()
    {
        if (active == null) active = FindFirstObjectByType<MS7C2SynergyCombat>();
        if (active == null || !active.isActiveAndEnabled || active.core == null) return null;
        return active;
    }

    public bool ActiveWildfire()
    {
        if (core == null) return false;
        core.RefreshNow(false);
        return core.WildfireConduitUnlocked;
    }
    public bool ActiveGlacial(Bullet bullet)
    {
        if (core == null || bullet == null) return false;
        core.RefreshNow(false);
        return core.GlacialReboundUnlocked && bullet.projectileEffect != null &&
               bullet.projectileEffect.effectType == ProjectileEffectType.Ricochet;
    }
    public bool ActiveThunder(Bullet bullet)
    {
        if (core == null || bullet == null) return false;
        core.RefreshNow(false);
        if (!core.ThunderPrecisionUnlocked) return false;
        MS7C2ProjectileState state = bullet.GetComponent<MS7C2ProjectileState>();
        return state != null && state.IsCritical;
    }

    public static float FormulaGlacialSlow(float baseSlow, float bonus, float maxSlow)
    {
        return Mathf.Clamp(Mathf.Max(0f, baseSlow) + Mathf.Max(0f, bonus), 0f,
            Mathf.Clamp(maxSlow, 0f, .9f));
    }
    public static float FormulaThunderDamage(float chainDamage, float multiplier)
    {
        return Mathf.Max(0f, chainDamage) * Mathf.Clamp(multiplier, 1f, 2f);
    }
    public static float FormulaWildfireDps(float baseDps, float bonusPerLevel,
        int fireLevel, float multiplier)
    {
        return Mathf.Max(0f, baseDps) *
            (1f + Mathf.Max(0, fireLevel - 1) * Mathf.Max(0f, bonusPerLevel)) *
            Mathf.Clamp(multiplier, .1f, 2f);
    }

    internal void CountWildfire(Health enemy)
    {
        wildfireProcs++;
        if (logTriggers) Debug.Log("[7C.2] Wildfire Burn on " + enemy.name +
                                   " | total=" + wildfireProcs, enemy);
    }
    internal void CountGlacial()
    {
        glacialHitBonuses++;
        if (logTriggers) Debug.Log("[7C.2] Glacial Rebound: 1 enhanced Slow call | total=" + glacialHitBonuses);
    }
    internal void CountThunder()
    {
        thunderCriticalChains++;
        if (logTriggers) Debug.Log("[7C.2] Thunder Precision: critical chain boosted | total=" + thunderCriticalChains);
    }

    public void ResetCounters()
    {
        wildfireProcs = glacialHitBonuses = thunderCriticalChains = 0;
    }
    private void OnDestroy() { if (active == this) active = null; }
}
