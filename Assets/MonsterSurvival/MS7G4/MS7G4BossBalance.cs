using System;
using System.Text;
using UnityEngine;

// 7G.4 - Additive balance preset for NEW 7G.1 / 7G.2 skills only.
// No direct edits to Boss Health, AI, original attacks, WaveManager, VFX or Save.
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class MS7G4BossBalance : MonoBehaviour
{
    public enum BalancePreset { Gentle = 0, Balanced = 1, Challenge = 2 }
    public enum BossType { Unknown = 0, ShadowTitan = 1, GraveWarden = 2 }

    [Header("Release safety - OFF after installation")]
    public bool enableBossBalance = false;
    public BalancePreset preset = BalancePreset.Balanced;

    [Header("Diagnostics (no gameplay writes)")]
    public bool logChanges = false;

    public bool IsApplied { get { return applied; } }
    public BossType ResolvedBoss { get { return bossType; } }
    public int ApplyCount { get; private set; }
    public int RestoreCount { get; private set; }
    public int SafetySkips { get; private set; }
    public bool SawPause { get; private set; }
    public bool SawPhaseTwo { get; private set; }
    public string LastStatus { get; private set; }

    private MS7G1TitanRiftLanes titan;
    private MS7G2GraveSoulReap grave;
    private BossPhase2Controller titanPhase;
    private GraveWardenBossAI graveAI;
    private Health bossHealth;
    private BossType bossType;
    private bool applied;
    private BalancePreset appliedPreset;
    private bool hasSnapshot;
    private TitanValues titanOriginal;
    private GraveValues graveOriginal;
    private float nextSafetyReport;

    private struct TitanValues
    {
        public float warning, damage, cooldown, firstDelay, halfWidth, sideAngle, nearRadius;
    }
    private struct GraveValues
    {
        public float warning, damage, cooldown, firstDelay, inner, outer, halfAngle;
    }

    private void Awake()
    {
        titan = GetComponent<MS7G1TitanRiftLanes>();
        grave = GetComponent<MS7G2GraveSoulReap>();
        bossHealth = GetComponent<Health>();
        titanPhase = GetComponent<BossPhase2Controller>();
        graveAI = GetComponent<GraveWardenBossAI>();
        bossType = titan != null && grave == null && titanPhase != null ? BossType.ShadowTitan :
                   grave != null && titan == null && graveAI != null ? BossType.GraveWarden : BossType.Unknown;
        LastStatus = "WAITING";
    }

    private void Start() { Reconcile(); }

    private void Update()
    {
        if (Time.timeScale <= 0f)
        {
            SawPause = true;
            if (!enableBossBalance && applied) RestoreOriginal();
            return;
        }
        if (titanPhase != null && titanPhase.IsPhase2) SawPhaseTwo = true;
        if (graveAI != null && graveAI.IsPhaseTwo) SawPhaseTwo = true;
        Reconcile();
    }

    private bool PhaseTwoNow()
    {
        return bossType == BossType.ShadowTitan ? titanPhase != null && titanPhase.IsPhase2 :
               bossType == BossType.GraveWarden && graveAI != null && graveAI.IsPhaseTwo;
    }

    private void Reconcile()
    {
        if (!enableBossBalance)
        {
            if (applied) RestoreOriginal();
            else LastStatus = "OFF";
            return;
        }
        if (bossType == BossType.Unknown || bossHealth == null)
        {
            LastStatus = "SKIPPED: unsupported or ambiguous boss source";
            ReportSkip();
            return;
        }
        if (bossHealth.CurrentHealth <= 0f)
        {
            if (applied) RestoreOriginal();
            LastStatus = "SKIPPED: boss defeated";
            return;
        }
        if (applied)
        {
            if (preset == appliedPreset) return;
            // Never change telegraph dimensions during Phase 2. Stage next preset for next spawn.
            LastStatus = "PENDING: preset changed; takes effect on next boss spawn";
            return;
        }
        // Scene/Inspector hot enabling in Phase 2 risks changing a live warning geometry.
        if (PhaseTwoNow())
        {
            LastStatus = "PENDING: enabled after Phase 2 began; restart boss encounter";
            ReportSkip();
            return;
        }
        if (bossType == BossType.ShadowTitan)
        {
            titanOriginal = ReadTitan();
            hasSnapshot = true;
            ApplyTitan(preset);
        }
        else
        {
            graveOriginal = ReadGrave();
            hasSnapshot = true;
            ApplyGrave(preset);
        }
        applied = true;
        appliedPreset = preset;
        ApplyCount++;
        LastStatus = "APPLIED: " + bossType + " / " + preset;
        if (logChanges) Debug.Log("[7G.4] " + LastStatus, this);
    }

    private void ReportSkip()
    {
        if (Time.time < nextSafetyReport) return;
        nextSafetyReport = Time.time + 5f;
        SafetySkips++;
    }

    private TitanValues ReadTitan()
    {
        return new TitanValues
        {
            warning = titan.warningSeconds, damage = titan.damage,
            cooldown = titan.cooldownSeconds, firstDelay = titan.firstCastDelayAfterPhase2,
            halfWidth = titan.laneHalfWidth, sideAngle = titan.sideAngleDegrees,
            nearRadius = titan.safeNearBossRadius
        };
    }
    private GraveValues ReadGrave()
    {
        return new GraveValues
        {
            warning = grave.warningDuration, damage = grave.damage,
            cooldown = grave.cooldown, firstDelay = grave.firstCastDelayAfterPhase2,
            inner = grave.innerRadius, outer = grave.outerRadius,
            halfAngle = grave.halfArcAngleDegrees
        };
    }

    private void ApplyTitan(BalancePreset p)
    {
        // Warning never shorter than existing runtime minimum (0.8s).
        // Conservative recommendations: long telegraphs and capped damage.
        switch (p)
        {
            case BalancePreset.Gentle:
                titan.warningSeconds = 2.4f; titan.damage = 6f;
                titan.cooldownSeconds = 22f; titan.firstCastDelayAfterPhase2 = 7f;
                titan.laneHalfWidth = 0.22f; titan.sideAngleDegrees = 34f;
                titan.safeNearBossRadius = 1.1f; break;
            case BalancePreset.Challenge:
                titan.warningSeconds = 1.8f; titan.damage = 10f;
                titan.cooldownSeconds = 15f; titan.firstCastDelayAfterPhase2 = 6f;
                titan.laneHalfWidth = 0.29f; titan.sideAngleDegrees = 28f;
                titan.safeNearBossRadius = 0.85f; break;
            default:
                titan.warningSeconds = 2.1f; titan.damage = 8f;
                titan.cooldownSeconds = 18f; titan.firstCastDelayAfterPhase2 = 6f;
                titan.laneHalfWidth = 0.25f; titan.sideAngleDegrees = 32f;
                titan.safeNearBossRadius = 1.0f; break;
        }
    }

    private void ApplyGrave(BalancePreset p)
    {
        switch (p)
        {
            case BalancePreset.Gentle:
                grave.warningDuration = 2.3f; grave.damage = 7f;
                grave.cooldown = 24f; grave.firstCastDelayAfterPhase2 = 7f;
                grave.innerRadius = 1.8f; grave.outerRadius = 4.0f;
                grave.halfArcAngleDegrees = 38f; break;
            case BalancePreset.Challenge:
                grave.warningDuration = 1.65f; grave.damage = 11f;
                grave.cooldown = 17f; grave.firstCastDelayAfterPhase2 = 6f;
                grave.innerRadius = 1.4f; grave.outerRadius = 4.3f;
                grave.halfArcAngleDegrees = 49f; break;
            default:
                grave.warningDuration = 1.95f; grave.damage = 9f;
                grave.cooldown = 20f; grave.firstCastDelayAfterPhase2 = 6f;
                grave.innerRadius = 1.6f; grave.outerRadius = 4.15f;
                grave.halfArcAngleDegrees = 43f; break;
        }
    }

    public void RestoreOriginal()
    {
        if (!applied || !hasSnapshot) return;
        if (bossType == BossType.ShadowTitan && titan != null)
        {
            titan.warningSeconds = titanOriginal.warning;
            titan.damage = titanOriginal.damage;
            titan.cooldownSeconds = titanOriginal.cooldown;
            titan.firstCastDelayAfterPhase2 = titanOriginal.firstDelay;
            titan.laneHalfWidth = titanOriginal.halfWidth;
            titan.sideAngleDegrees = titanOriginal.sideAngle;
            titan.safeNearBossRadius = titanOriginal.nearRadius;
        }
        else if (bossType == BossType.GraveWarden && grave != null)
        {
            grave.warningDuration = graveOriginal.warning;
            grave.damage = graveOriginal.damage;
            grave.cooldown = graveOriginal.cooldown;
            grave.firstCastDelayAfterPhase2 = graveOriginal.firstDelay;
            grave.innerRadius = graveOriginal.inner;
            grave.outerRadius = graveOriginal.outer;
            grave.halfArcAngleDegrees = graveOriginal.halfAngle;
        }
        applied = false;
        hasSnapshot = false;
        RestoreCount++;
        LastStatus = "RESTORED: baseline values";
        if (logChanges) Debug.Log("[7G.4] " + LastStatus, this);
    }

    private void OnDisable() { RestoreOriginal(); }
    private void OnDestroy() { RestoreOriginal(); }

    public string BuildReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("[7G.4] Boss=" + name + " Type=" + bossType +
                      " OptIn=" + enableBossBalance + " Preset=" + preset +
                      " Applied=" + applied + " Status=" + LastStatus);
        sb.AppendLine("ApplyCount=" + ApplyCount + " RestoreCount=" + RestoreCount +
                      " SafetySkips=" + SafetySkips + " PauseSeen=" + SawPause +
                      " Phase2Seen=" + SawPhaseTwo);
        if (bossHealth != null) sb.AppendLine("Boss Health.maxHealth=" + bossHealth.maxHealth +
                                                " CurrentHealth=" + bossHealth.CurrentHealth);
        if (titan != null)
        {
            sb.AppendLine("Rift: Warn=" + titan.warningSeconds + " Damage=" + titan.damage +
                          " Cooldown=" + titan.cooldownSeconds + " SideAngle=" + titan.sideAngleDegrees +
                          " Width=" + titan.laneHalfWidth + " NearSafe=" + titan.safeNearBossRadius);
            sb.AppendLine("Rift Runtime: Warnings=" + titan.WarningCount + " Impacts=" + titan.ImpactCount +
                          " Hits=" + titan.HitCount + " Dodges=" + titan.DodgeCount);
        }
        if (grave != null)
        {
            sb.AppendLine("Reap: Warn=" + grave.warningDuration + " Damage=" + grave.damage +
                          " Cooldown=" + grave.cooldown + " Inner=" + grave.innerRadius +
                          " Outer=" + grave.outerRadius + " HalfAngle=" + grave.halfArcAngleDegrees);
            sb.AppendLine("Reap Runtime: Warnings=" + grave.WarningsStarted + " Impacts=" + grave.Impacts +
                          " HPReduced=" + grave.HpReduced + " Dodges=" + grave.PlayerDodged +
                          " ShieldBlocked=" + grave.ShieldBlocked + " InterruptedOldAI=" + grave.InterruptedByOldAI);
        }
        sb.AppendLine("READ ONLY QA OUTPUT: counters are not proof of visual readability, all boss attacks, or Save/Profile.");
        return sb.ToString();
    }
}
