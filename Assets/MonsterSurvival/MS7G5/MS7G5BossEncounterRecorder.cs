using System;
using System.Text;
using UnityEngine;

// 7G.5. READ-ONLY observer on the existing WaveManager. NO gameplay or Save writes.
[DisallowMultipleComponent]
[DefaultExecutionOrder(15000)]
public sealed class MS7G5BossEncounterRecorder : MonoBehaviour
{
    [Header("READ ONLY - leaves Boss, Health, Waves and Profile untouched")]
    public bool enableObservation = true;

    public sealed class Capture
    {
        public string bossType;
        public string lastObject;
        public int encounters;
        public float runtimeMaxHealth;
        public float runtimeCurrentHealth;
        public bool phaseTwo;
        public bool sawPause;
        public bool skillEnabled;
        public bool vfxEnabled;
        public bool balanceEnabled;
        public bool balanceApplied;
        public string preset = "Unknown";
        public int warnings, impacts, dodges, hpHits, shieldBlocks, wallBlocks;
        public int cancels, oldAiInterrupts, voidBurstDeferred;
        public int vfxWarnings, vfxImpacts, outlineSyncs, missingGeometry;
        public int balanceApplies, balanceRestores, balanceSafetySkips;
        public bool existingBossAiDetected;
        public float warningSeconds, cooldown, damage;
        public bool aliveAtLastSample;
        public bool hasSample;
        internal EntityId lastInstance;
        internal Component source;

        public string ToReport()
        {
            var b = new StringBuilder();
            b.AppendLine("BOSS_TYPE=" + bossType);
            b.AppendLine("RUNTIME_SEEN=" + hasSample + " ENCOUNTERS=" + encounters + " LAST_OBJECT=" + lastObject);
            b.AppendLine("HP_RUNTIME=" + runtimeMaxHealth.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                         " HP_CURRENT_LAST=" + runtimeCurrentHealth.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
            b.AppendLine("PHASE2_SEEN=" + phaseTwo + " PAUSE_SEEN=" + sawPause + " ALIVE_AT_LAST_SAMPLE=" + aliveAtLastSample);
            b.AppendLine("SKILL_ENABLED=" + skillEnabled + " OLD_AI_PRESENT=" + existingBossAiDetected);
            b.AppendLine("WARNINGS=" + warnings + " IMPACTS=" + impacts + " DODGES=" + dodges + " HP_REDUCED_HITS=" + hpHits);
            b.AppendLine("SHIELD_BLOCKED=" + shieldBlocks + " WALL_BLOCKED=" + wallBlocks + " CANCELS=" + cancels +
                         " OLD_AI_INTERRUPTS=" + oldAiInterrupts + " VOID_BURST_DEFERRED=" + voidBurstDeferred);
            b.AppendLine("VFX_ENABLED=" + vfxEnabled + " VFX_WARNING=" + vfxWarnings + " VFX_IMPACT=" + vfxImpacts +
                         " VFX_SYNC=" + outlineSyncs + " VFX_GEOMETRY_MISSING_FRAMES=" + missingGeometry);
            b.AppendLine("BALANCE_ENABLED=" + balanceEnabled + " BALANCE_APPLIED=" + balanceApplied +
                         " PRESET=" + preset + " APPLIES=" + balanceApplies + " RESTORES=" + balanceRestores +
                         " SAFETY_SKIPS=" + balanceSafetySkips);
            b.AppendLine("WARN_SEC=" + warningSeconds + " DMG=" + damage + " COOLDOWN_SEC=" + cooldown);
            if (encounters > 1) b.AppendLine("[PENDING] Multiple spawns of same Boss: counters shown for latest encounter only.");
            return b.ToString();
        }
    }

    public readonly Capture Titan = new Capture { bossType = "Titan" };
    public readonly Capture Grave = new Capture { bossType = "Grave" };
    public bool SawAnyPause { get; private set; }
    public int ObservedFrames { get; private set; }
    private float nextLookup;
    private MS7G1TitanRiftLanes titanLive;
    private MS7G2GraveSoulReap graveLive;

    private void Update()
    {
        if (!enableObservation || !Application.isPlaying) return;
        ObservedFrames++;
        if (Time.timeScale <= 0f) SawAnyPause = true;
        // Find bosses periodically, then snapshot every frame so counters survive Victory/destruction.
        if (Time.unscaledTime >= nextLookup)
        {
            nextLookup = Time.unscaledTime + 0.25f;
            if (titanLive == null) titanLive = FindFirstObjectByType<MS7G1TitanRiftLanes>();
            if (graveLive == null) graveLive = FindFirstObjectByType<MS7G2GraveSoulReap>();
        }
        if (titanLive != null) CaptureTitan(titanLive);
        if (graveLive != null) CaptureGrave(graveLive);
    }

    private static void CaptureIdentity(Capture state, Component source, Health health)
    {
        EntityId id = source.GetEntityId();
        if (state.lastInstance != id)
        {
            state.lastInstance = id;
            state.source = source;
            state.encounters++;
            // Counters intentionally represent the latest instance, never falsely merge separate boss runs.
            state.warnings = state.impacts = state.dodges = state.hpHits = 0;
            state.cancels = state.oldAiInterrupts = state.voidBurstDeferred = 0;
            state.vfxWarnings = state.vfxImpacts = state.outlineSyncs = state.missingGeometry = 0;
        }
        state.hasSample = true;
        state.lastObject = source.gameObject.name;
        if (health != null)
        {
            state.runtimeMaxHealth = health.maxHealth;
            state.runtimeCurrentHealth = health.CurrentHealth;
            state.aliveAtLastSample = health.CurrentHealth > 0f;
        }
        if (Time.timeScale <= 0f) state.sawPause = true;
    }

    private static void CaptureCommon(Capture state, Component boss)
    {
        var vfx = boss.GetComponent<MS7G3BossSkillVFX>();
        if (vfx != null)
        {
            state.vfxEnabled = vfx.enableEnhancedVFX;
            state.vfxWarnings = vfx.WarningFxCount;
            state.vfxImpacts = vfx.ImpactFxCount;
            state.outlineSyncs = vfx.SyncedOutlineCount;
            state.missingGeometry = vfx.MissingGeometryFrames;
            state.sawPause |= vfx.PauseObserved;
        }
        var balance = boss.GetComponent<MS7G4BossBalance>();
        if (balance != null)
        {
            state.balanceEnabled = balance.enableBossBalance;
            state.balanceApplied = balance.IsApplied;
            state.preset = balance.preset.ToString();
            state.balanceApplies = balance.ApplyCount;
            state.balanceRestores = balance.RestoreCount;
            state.balanceSafetySkips = balance.SafetySkips;
            state.sawPause |= balance.SawPause;
            state.phaseTwo |= balance.SawPhaseTwo;
        }
    }

    private void CaptureTitan(MS7G1TitanRiftLanes source)
    {
        CaptureIdentity(Titan, source, source.GetComponent<Health>());
        Titan.skillEnabled = source.enableRiftLanes;
        Titan.existingBossAiDetected = source.GetComponent<BossPhase2Controller>() != null &&
                                       source.GetComponent<BossSpecialAttack>() != null &&
                                       source.GetComponent<BossVoidBurstAttack>() != null;
        Titan.phaseTwo |= source.Phase2Observed;
        Titan.sawPause |= source.PauseObserved;
        Titan.warnings = source.WarningCount;
        Titan.impacts = source.ImpactCount;
        Titan.dodges = source.DodgeCount;
        Titan.hpHits = source.HitCount;
        Titan.cancels = source.CancelCount;
        Titan.voidBurstDeferred = source.VoidBurstDeferCount;
        Titan.warningSeconds = source.warningSeconds;
        Titan.damage = source.damage;
        Titan.cooldown = source.cooldownSeconds;
        CaptureCommon(Titan, source);
    }

    private void CaptureGrave(MS7G2GraveSoulReap source)
    {
        CaptureIdentity(Grave, source, source.GetComponent<Health>());
        Grave.skillEnabled = source.enableSoulReap;
        var ai = source.GetComponent<GraveWardenBossAI>();
        Grave.existingBossAiDetected = ai != null && source.OldCastFlagsAvailable;
        Grave.phaseTwo |= ai != null && ai.IsPhaseTwo;
        Grave.warnings = source.WarningsStarted;
        Grave.impacts = source.Impacts;
        Grave.dodges = source.PlayerDodged;
        Grave.hpHits = source.HpReduced;
        Grave.shieldBlocks = source.ShieldBlocked;
        Grave.wallBlocks = source.WallBlocked;
        Grave.oldAiInterrupts = source.InterruptedByOldAI;
        Grave.cancels = source.CancelledOther;
        Grave.warningSeconds = source.warningDuration;
        Grave.damage = source.damage;
        Grave.cooldown = source.cooldown;
        CaptureCommon(Grave, source);
    }
}
