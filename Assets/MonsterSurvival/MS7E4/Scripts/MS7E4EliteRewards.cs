using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 7E.4: Grants EXTRA EXP/Gold on an existing Elite's Health.OnDied.
/// Base rewards remain entirely owned by EnemyController.Die().
/// This component does not modify EnemyController rewards, PlayerStats, Gold bank, or Save/Profile.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyController), typeof(Health))]
[RequireComponent(typeof(MS7E1EliteAffix))]
public sealed class MS7E4EliteRewards : MonoBehaviour
{
    [Header("Safe opt-in: OFF until QA confirms mapping")]
    [SerializeField] private bool enableExtraRewards = false;

    [Header("Additional reward as fraction of original Elite base reward")]
    [Range(0f, 2f)] [SerializeField] private float bulwarkExtraExp = 0.70f;
    [Range(0f, 2f)] [SerializeField] private float bulwarkExtraGold = 0.50f;
    [Range(0f, 2f)] [SerializeField] private float swiftExtraExp = 0.55f;
    [Range(0f, 2f)] [SerializeField] private float swiftExtraGold = 0.50f;
    [Range(0f, 2f)] [SerializeField] private float regeneratorExtraExp = 0.65f;
    [Range(0f, 2f)] [SerializeField] private float regeneratorExtraGold = 0.50f;

    [Header("Hard per-kill caps; only extra reward, never base reward")]
    [Min(0)] [SerializeField] private int maxExtraExpPerKill = 12;
    [Min(0)] [SerializeField] private int maxExtraGoldPerKill = 4;

    [Header("Optional diagnostics")]
    [SerializeField] private bool printSuccessfulPayouts = false;

    private EnemyController enemy;
    private Health health;
    private MS7E1EliteAffix affix;
    private bool subscribed;
    private bool deathHandled;

    public bool ExtraRewardsEnabled { get { return enableExtraRewards; } }
    public bool DeathHandled { get { return deathHandled; } }

    public static int DeathEvents { get; private set; }
    public static int GrantedDeaths { get; private set; }
    public static int SkippedDisabled { get; private set; }
    public static int SkippedNoPlayer { get; private set; }
    public static int SkippedNotInitialized { get; private set; }
    public static int ExtraExpGranted { get; private set; }
    public static int ExtraGoldGranted { get; private set; }
    public static int BulwarkPayouts { get; private set; }
    public static int SwiftPayouts { get; private set; }
    public static int RegeneratorPayouts { get; private set; }

    private const int TraceLimit = 80;
    private static readonly List<string> Trace = new List<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPlaySession()
    {
        DeathEvents = 0;
        GrantedDeaths = 0;
        SkippedDisabled = 0;
        SkippedNoPlayer = 0;
        SkippedNotInitialized = 0;
        ExtraExpGranted = 0;
        ExtraGoldGranted = 0;
        BulwarkPayouts = 0;
        SwiftPayouts = 0;
        RegeneratorPayouts = 0;
        Trace.Clear();
    }

    public static string RecentEvents()
    {
        return Trace.Count > 0 ? string.Join("\n", Trace.ToArray()) : "(No Elite reward events observed yet.)";
    }

    /// <summary>Pure, read-only reward math shared with QA. Custom prefab factors are respected.</summary>
    public static void ComputeExtraForBalance(int baseExp, int baseGold,
        float expFactor, float goldFactor, int capExp, int capGold,
        out int extraExp, out int extraGold)
    {
        extraExp = ComputeBoundedExtra(baseExp, expFactor, capExp);
        extraGold = ComputeBoundedExtra(baseGold, goldFactor, capGold);
    }

    public void PreviewBonus(MS7E1EliteAffix.AffixKind kind, out int extraExp, out int extraGold)
    {
        EnemyController source = enemy != null ? enemy : GetComponent<EnemyController>();
        int baseExp = source != null ? Mathf.Max(0, source.expReward) : 0;
        int baseGold = source != null ? Mathf.Max(0, source.coinReward) : 0;
        float expFactor = 0f;
        float goldFactor = 0f;
        if (kind == MS7E1EliteAffix.AffixKind.Bulwark)
        {
            expFactor = bulwarkExtraExp;
            goldFactor = bulwarkExtraGold;
        }
        else if (kind == MS7E1EliteAffix.AffixKind.Swift)
        {
            expFactor = swiftExtraExp;
            goldFactor = swiftExtraGold;
        }
        else if (kind == MS7E1EliteAffix.AffixKind.Regenerator)
        {
            expFactor = regeneratorExtraExp;
            goldFactor = regeneratorExtraGold;
        }
        ComputeExtraForBalance(baseExp, baseGold, expFactor, goldFactor,
            maxExtraExpPerKill, maxExtraGoldPerKill, out extraExp, out extraGold);
    }

    private static int ComputeBoundedExtra(int baseline, float factor, int cap)
    {
        if (baseline <= 0 || cap <= 0 || factor <= 0f) return 0;
        // Avoid overflow for exotic reward values; the final bonus is always bounded.
        double proposed = Math.Ceiling((double)baseline * (double)factor);
        return (int)Math.Min(Math.Max(0d, proposed), (double)cap);
    }

    private void Awake()
    {
        enemy = GetComponent<EnemyController>();
        health = GetComponent<Health>();
        affix = GetComponent<MS7E1EliteAffix>();
    }

    private void OnEnable()
    {
        if (!subscribed && health != null)
        {
            health.OnDied += HandleDeath;
            subscribed = true;
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void Unsubscribe()
    {
        if (!subscribed || health == null) return;
        health.OnDied -= HandleDeath;
        subscribed = false;
    }

    private void HandleDeath()
    {
        if (deathHandled) return;
        deathHandled = true; // exactly-once protection per Elite instance
        DeathEvents++;

        if (!enableExtraRewards)
        {
            SkippedDisabled++;
            AddTrace(name + " | DISABLED (base rewards still granted by EnemyController)");
            return;
        }
        if (enemy == null || affix == null || !affix.Initialized ||
            affix.ActiveAffix == MS7E1EliteAffix.AffixKind.Random)
        {
            SkippedNotInitialized++;
            AddTrace(name + " | SKIP: Affix not initialized");
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        PlayerProgression progression = player != null ? player.GetComponent<PlayerProgression>() : null;
        if (progression == null)
        {
            SkippedNoPlayer++;
            AddTrace(name + " | SKIP: PlayerProgression missing");
            return;
        }

        float expFactor = 0f;
        float goldFactor = 0f;
        switch (affix.ActiveAffix)
        {
            case MS7E1EliteAffix.AffixKind.Bulwark:
                expFactor = bulwarkExtraExp;
                goldFactor = bulwarkExtraGold;
                break;
            case MS7E1EliteAffix.AffixKind.Swift:
                expFactor = swiftExtraExp;
                goldFactor = swiftExtraGold;
                break;
            case MS7E1EliteAffix.AffixKind.Regenerator:
                expFactor = regeneratorExtraExp;
                goldFactor = regeneratorExtraGold;
                break;
            default:
                SkippedNotInitialized++;
                AddTrace(name + " | SKIP: Unknown Affix");
                return;
        }

        int baseExp = Mathf.Max(0, enemy.expReward);
        int baseGold = Mathf.Max(0, enemy.coinReward);
        int extraExp = ComputeBoundedExtra(baseExp, expFactor, maxExtraExpPerKill);
        int extraGold = ComputeBoundedExtra(baseGold, goldFactor, maxExtraGoldPerKill);

        // Existing EnemyController will still independently grant its ORIGINAL reward on death.
        if (extraExp > 0) progression.AddExp(extraExp);
        if (extraGold > 0) progression.AddCoins(extraGold);

        GrantedDeaths++;
        ExtraExpGranted += extraExp;
        ExtraGoldGranted += extraGold;
        if (affix.ActiveAffix == MS7E1EliteAffix.AffixKind.Bulwark) BulwarkPayouts++;
        else if (affix.ActiveAffix == MS7E1EliteAffix.AffixKind.Swift) SwiftPayouts++;
        else if (affix.ActiveAffix == MS7E1EliteAffix.AffixKind.Regenerator) RegeneratorPayouts++;
        AddTrace(name + " | " + affix.ActiveAffix + " | base=" + baseExp + "EXP/" +
            baseGold + "Gold | EXTRA=" + extraExp + "EXP/" + extraGold + "Gold");
        if (printSuccessfulPayouts)
            Debug.Log("[7E.4] Elite extra reward: +" + extraExp + " EXP +" + extraGold + " Gold. " + name, this);
    }

    private static void AddTrace(string message)
    {
        if (Trace.Count >= TraceLimit) Trace.RemoveAt(0);
        Trace.Add(Time.time.ToString("F1") + "s | " + message);
    }

    private void OnValidate()
    {
        maxExtraExpPerKill = Mathf.Max(0, maxExtraExpPerKill);
        maxExtraGoldPerKill = Mathf.Max(0, maxExtraGoldPerKill);
    }
}
