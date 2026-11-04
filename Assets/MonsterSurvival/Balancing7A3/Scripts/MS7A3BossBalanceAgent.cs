using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// Attach to the ROOT of the actual boss PREFAB (same object as Health).
// The very early order is important: Health.Awake sets CurrentHealth=maxHealth.
// No edits are made to prefab-authored HP, AI, animation, loot or Save data.
[DefaultExecutionOrder(-9000)]
[RequireComponent(typeof(Health))]
[DisallowMultipleComponent]
public sealed class MS7A3BossBalanceAgent : MonoBehaviour
{
    [Serializable]
    public sealed class OptionalDamageBinding
    {
        [Tooltip("Component on THIS boss prefab; not a project asset or a Player script.")]
        public Component component;
        [Tooltip("EXACT public float field name from the attack script. Audit first.")]
        public string publicFloatFieldName;
        [Range(0.5f, 2f)] public float multiplier = 1f;
    }

    [Header("Boss identity / balance")]
    public MS7A3BossBalanceProfile profile;
    [Tooltip("OFF by default. Only a spawned boss with this ON applies live tuning.")]
    public bool applyAtRuntime = false;

    [Header("Optional damage fields - for Warden attacks not verified in source")]
    [Tooltip("Must be set explicitly by developer AFTER inspecting boss attack script. Skips invalid entries safely.")]
    public List<OptionalDamageBinding> additionalDamageFields = new List<OptionalDamageBinding>();

    [Header("Standalone reward - HIGH CARE")]
    [Tooltip("OFF by default. Enable ONLY after verifying no OTHER component already grants Gold/EXP on this Boss death. Existing EnemyController rewards are always tuned in-place, never duplicated.")]
    public bool grantStandaloneIfNoEnemyController = false;
    public bool logBalance = true;

    public bool DidApply { get; private set; }
    public bool GaveStandaloneReward { get; private set; }
    public float BattleSeconds { get { return startedAt > 0f ? Mathf.Max(0f, Time.time - startedAt) : 0f; } }

    private Health health;
    private EnemyController rewardSource;
    private float startedAt = -1f;
    private bool died;
    private float damageTaken;
    private float lastHealth;

    private void Awake()
    {
        health = GetComponent<Health>();
        rewardSource = GetComponent<EnemyController>();
        if (!applyAtRuntime) return;
        if (profile == null)
        {
            Debug.LogWarning("[7A.3] Profile missing; leaving boss unchanged.", this);
            return;
        }
        string reason;
        if (!profile.Validate(out reason))
        {
            Debug.LogError("[7A.3] " + reason + "; boss left unchanged.", this);
            return;
        }
        if (health == null)
        {
            Debug.LogError("[7A.3] Health missing on boss root.", this);
            return;
        }

        // Prevent applying the wrong profile to the wrong boss prefab.
        bool isWarden = GetComponent<GraveWardenBossAI>() != null;
        bool isTitan = GetComponent<BossSpecialAttack>() != null || GetComponent<BossVoidBurstAttack>() != null;
        if ((profile.boss == MS7A3BossBalanceProfile.BossKind.GraveWarden && !isWarden) ||
            (profile.boss == MS7A3BossBalanceProfile.BossKind.ShadowTitan && !isTitan))
        {
            Debug.LogError("[7A.3] Profile/boss AI mismatch. Verify boss prefab, no changes applied.", this);
            return;
        }

        // This runs before Health.Awake, so CurrentHealth starts at the tuned maximum.
        health.maxHealth = Mathf.Max(1f, health.maxHealth * profile.maxHpMultiplier);

        if (profile.boss == MS7A3BossBalanceProfile.BossKind.ShadowTitan)
        {
            BossSpecialAttack shock = GetComponent<BossSpecialAttack>();
            if (shock != null)
            {
                shock.shockwaveDamage = Mathf.Max(0f, shock.shockwaveDamage * profile.shockwaveDamageMultiplier);
                shock.cooldown = Mathf.Max(0.1f, shock.cooldown * profile.shockwaveCooldownMultiplier);
                shock.warningDuration = Mathf.Max(0.2f, shock.warningDuration * profile.shockwaveWarningMultiplier);
            }
            BossVoidBurstAttack burst = GetComponent<BossVoidBurstAttack>();
            if (burst != null)
            {
                burst.damagePerZone = Mathf.Max(0f, burst.damagePerZone * profile.voidBurstDamageMultiplier);
                burst.castInterval = Mathf.Max(0.1f, burst.castInterval * profile.voidBurstCooldownMultiplier);
                burst.warningDuration = Mathf.Max(0.2f, burst.warningDuration * profile.voidBurstWarningMultiplier);
                // Do not change charge stop / zone VFX lifetime / phase-2 animation logic.
            }
        }
        else
        {
            GraveWardenBossAI warden = GetComponent<GraveWardenBossAI>();
            if (warden != null)
            {
                float c = profile.wardenCooldownMultiplier;
                warden.summonCooldown = Mathf.Max(0.1f, warden.summonCooldown * c);
                warden.tombWallCooldown = Mathf.Max(0.1f, warden.tombWallCooldown * c);
                warden.spiritOrbitCooldown = Mathf.Max(0.1f, warden.spiritOrbitCooldown * c);
                warden.reanimateCooldown = Mathf.Max(0.1f, warden.reanimateCooldown * c);
                warden.minimumTimeBetweenCasts = Mathf.Max(0.5f, warden.minimumTimeBetweenCasts * profile.wardenGapMultiplier);
                warden.moveSpeed = Mathf.Max(0f, warden.moveSpeed * profile.wardenMovementMultiplier);
            }
        }

        // Existing EnemyController already pays once on Health.OnDied. We ONLY modify its numbers.
        if (rewardSource != null)
        {
            rewardSource.coinReward = ScaleReward(rewardSource.coinReward, profile.goldMultiplier);
            rewardSource.expReward = ScaleReward(rewardSource.expReward, profile.expMultiplier);
        }
        ApplyExplicitDamageBindings();
        DidApply = true;
        if (logBalance)
        {
            Debug.Log("[7A.3] ACTIVE " + profile.boss + " HP=" + health.maxHealth.ToString("0.#") +
                      " existing reward source=" + (rewardSource != null) +
                      " standalone=" + (grantStandaloneIfNoEnemyController && rewardSource == null), this);
        }
    }

    private static int ScaleReward(int baseline, float multiplier)
    {
        if (baseline <= 0) return 0;
        return Mathf.Clamp(Mathf.RoundToInt(baseline * multiplier), 0, 1000000);
    }

    private void ApplyExplicitDamageBindings()
    {
        if (additionalDamageFields == null) return;
        HashSet<string> processed = new HashSet<string>();
        foreach (OptionalDamageBinding b in additionalDamageFields)
        {
            string reason;
            if (!IsValidBinding(b, out reason))
            {
                Debug.LogWarning("[7A.3] Skipping optional damage field: " + reason, this);
                continue;
            }
            string key = b.component.GetEntityId() + ":" + b.publicFloatFieldName;
            if (!processed.Add(key))
            {
                Debug.LogWarning("[7A.3] Duplicate optional damage field skipped: " + b.publicFloatFieldName, this);
                continue;
            }
            FieldInfo field = b.component.GetType().GetField(b.publicFloatFieldName, BindingFlags.Public | BindingFlags.Instance);
            float baseline = (float)field.GetValue(b.component);
            field.SetValue(b.component, Mathf.Max(0f, baseline * b.multiplier));
        }
    }

    public bool IsValidBinding(OptionalDamageBinding b, out string reason)
    {
        if (b == null || b.component == null || string.IsNullOrEmpty(b.publicFloatFieldName))
        { reason = "missing component/field"; return false; }
        if (b.component.gameObject != gameObject)
        { reason = "component must be on SAME boss root"; return false; }
        if (float.IsNaN(b.multiplier) || float.IsInfinity(b.multiplier) || b.multiplier < 0.5f || b.multiplier > 2f)
        { reason = "invalid multiplier"; return false; }
        if (b.component is Health || b.component is EnemyController ||
            b.component is MS7A3BossBalanceAgent || b.component is BossSpecialAttack ||
            b.component is BossVoidBurstAttack)
        { reason = "reserved component; already tuned or managed elsewhere"; return false; }
        // Optional fields are specifically for damage; never accidentally tune cooldown / life / animation timings.
        if (b.publicFloatFieldName.IndexOf("damage", StringComparison.OrdinalIgnoreCase) < 0)
        { reason = "field name must contain 'damage'"; return false; }
        FieldInfo field = b.component.GetType().GetField(b.publicFloatFieldName, BindingFlags.Public | BindingFlags.Instance);
        if (field == null || field.FieldType != typeof(float) || field.IsInitOnly || field.IsStatic)
        { reason = "field not found or not a writable public float"; return false; }
        float current = (float)field.GetValue(b.component);
        if (float.IsNaN(current) || float.IsInfinity(current))
        { reason = "invalid baseline"; return false; }
        reason = "OK";
        return true;
    }

    private void OnEnable()
    {
        if (health == null) health = GetComponent<Health>();
        if (health == null) return;
        health.OnDied += OnBossDied;
        health.OnHealthChanged += OnBossHealthChanged;
    }

    private void Start()
    {
        startedAt = Time.time;
        if (health != null) lastHealth = health.CurrentHealth;
    }

    private void OnBossHealthChanged(float current, float maximum)
    {
        if (!DidApply) return;
        if (lastHealth > current) damageTaken += lastHealth - current;
        lastHealth = current;
    }

    private void OnBossDied()
    {
        if (died) return;
        died = true;
        if (!DidApply) return;
        if (grantStandaloneIfNoEnemyController && rewardSource == null && !GaveStandaloneReward)
        {
            // EXPLICIT OPT IN. Never executes on a boss which has EnemyController reward source.
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            PlayerProgression progression = player != null ? player.GetComponent<PlayerProgression>() : null;
            if (progression != null && profile != null)
            {
                GaveStandaloneReward = true;
                if (profile.standaloneExp > 0) progression.AddExp(profile.standaloneExp);
                if (profile.standaloneGold > 0) progression.AddCoins(profile.standaloneGold);
            }
            else Debug.LogWarning("[7A.3] Standalone reward skipped: PlayerProgression not found.", this);
        }
        if (logBalance)
            Debug.Log("[7A.3] " + profile.boss + " defeated | fight=" + BattleSeconds.ToString("0.0") +
                      "s | recorded boss damage=" + damageTaken.ToString("0.0") +
                      " | standalone reward=" + GaveStandaloneReward, this);
    }

    private void OnDisable()
    {
        if (health == null) return;
        health.OnDied -= OnBossDied;
        health.OnHealthChanged -= OnBossHealthChanged;
        // Do NOT reset maxHealth mid-combat; that would desync CurrentHealth.
        // To compare baseline, turn off ApplyAtRuntime on the prefab and restart Play Mode.
    }
}
