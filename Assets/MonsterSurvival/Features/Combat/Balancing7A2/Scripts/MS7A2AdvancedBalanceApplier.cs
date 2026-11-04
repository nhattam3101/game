using System;
using System.Collections.Generic;
using UnityEngine;

// Add to WaveManager alongside 7A.1. Preview-only by default.
// Does not subscribe to Health.OnDied, pay rewards, or modify boss/minion AI.
[DisallowMultipleComponent]
public sealed class MS7A2AdvancedBalanceApplier : MonoBehaviour
{
    [Header("References")]
    public WaveManager waveManager;
    public HealerWaveSpawner healerSpawner;
    public SummonerWaveSpawner summonerSpawner;
    public MS7A2AdvancedBalanceProfile profile;

    [Header("Safety")]
    [Tooltip("OFF = diagnostics only. ON = apply to directors and newly observed special enemies.")]
    public bool applyAtRuntime = false;
    public bool logWaveChanges = true;
    [Tooltip("Scan interval; the component does not scan all enemies every frame.")]
    [Min(0.2f)] public float scanInterval = 0.75f;

    public int LastAppliedWave { get; private set; }
    public int LastTunedEnemies { get; private set; }

    private int observedWave = -1;
    private float nextScanTime;
    private bool previousApplyMode;
    private DirectorSnapshot directorSnapshot;
    private readonly List<EnemySnapshot> enemySnapshots = new List<EnemySnapshot>();

    private sealed class DirectorSnapshot
    {
        public HealerWaveSpawner healer;
        public SummonerWaveSpawner summoner;
        public float hInterval, sInterval;
        public int hActive, hWave, sActive, sWave;
        public float ahInterval, asInterval;
        public int ahActive, ahWave, asActive, asWave;
    }

    private sealed class EnemySnapshot
    {
        public GameObject go;
        public HealerEnemySupport healer;
        public SummonerEnemyAI summoner;
        public RangedEnemyController ranged;
        public EnemyController melee;
        public float hAmount, hInterval, sInterval, rInterval, rDamage, meleeDamage;
        public int minions, maxMinions, meleeExp, meleeGold, rangedExp, rangedGold;
        public float ahAmount, ahInterval, asInterval, arInterval, arDamage, aMeleeDamage;
        public int aMinions, aMaxMinions, aMeleeExp, aMeleeGold, aRangedExp, aRangedGold;
    }

    private void Awake()
    {
        if (waveManager == null) waveManager = GetComponent<WaveManager>();
        if (healerSpawner == null) healerSpawner = GetComponent<HealerWaveSpawner>();
        if (summonerSpawner == null) summonerSpawner = GetComponent<SummonerWaveSpawner>();
    }

    private void OnEnable() { observedWave = -1; nextScanTime = 0f; LastAppliedWave = 0; previousApplyMode = false; }

    private void Update()
    {
        // Respond safely to Inspector toggles while already in Play Mode.
        if (previousApplyMode != applyAtRuntime)
        {
            if (previousApplyMode && !applyAtRuntime) RestoreAll();
            previousApplyMode = applyAtRuntime;
            observedWave = -1;
            nextScanTime = 0f;
        }
        if (waveManager == null || profile == null || waveManager.gameManager == null ||
            waveManager.gameManager.IsGameOver) return;

        int wave = waveManager.CurrentWave;
        if (wave < 1) return;
        MS7A2AdvancedBalanceProfile.WaveRow row;
        bool valid = profile.TryGet(wave, out row) && MS7A2AdvancedBalanceProfile.Valid(row);
        if (wave != observedWave)
        {
            observedWave = wave;
            if (logWaveChanges)
                Debug.Log("[7A.2] Wave " + wave + (valid ? (applyAtRuntime ? " ACTIVE" : " PREVIEW") : " INVALID / UNCHANGED"), this);
            if (valid && applyAtRuntime) ApplyDirectors(row, wave);
        }
        if (!applyAtRuntime || !valid || Time.timeScale <= 0f) return;
        if (Time.unscaledTime < nextScanTime) return;
        nextScanTime = Time.unscaledTime + Mathf.Max(0.2f, scanInterval);
        TuneNewEnemies(row);
    }

    private void ApplyDirectors(MS7A2AdvancedBalanceProfile.WaveRow r, int wave)
    {
        if (healerSpawner == null && summonerSpawner == null) return;
        // Restore last wave's director overrides, before taking fresh baselines.
        RestoreDirectors();
        DirectorSnapshot d = new DirectorSnapshot();
        d.healer = healerSpawner; d.summoner = summonerSpawner;
        if (d.healer != null)
        {
            d.hInterval = d.healer.spawnInterval; d.hActive = d.healer.maxActiveHealers;
            d.hWave = d.healer.maxSpawnsPerWave;
            d.ahInterval = r.healerSpawnInterval; d.ahActive = r.maxActiveHealers; d.ahWave = r.healerSpawnsPerWave;
            d.healer.spawnInterval = r.healerSpawnInterval;
            d.healer.maxActiveHealers = r.maxActiveHealers;
            d.healer.maxSpawnsPerWave = r.healerSpawnsPerWave;
        }
        if (d.summoner != null)
        {
            d.sInterval = d.summoner.spawnInterval; d.sActive = d.summoner.maxActiveSummoners;
            d.sWave = d.summoner.maxSpawnsPerWave;
            d.asInterval = r.summonerSpawnInterval; d.asActive = r.maxActiveSummoners;
            d.asWave = r.summonerSpawnsPerWave;
            d.summoner.spawnInterval = r.summonerSpawnInterval;
            d.summoner.maxActiveSummoners = r.maxActiveSummoners;
            d.summoner.maxSpawnsPerWave = r.summonerSpawnsPerWave;
        }
        directorSnapshot = d;
        LastAppliedWave = wave;
    }

    private void TuneNewEnemies(MS7A2AdvancedBalanceProfile.WaveRow r)
    {
        // Only special enemy types or explicitly marked Elite; no normal, Tank or bosses.
        // Inactive/enemy prefab assets are excluded by FindObjectsByType.
        Health[] enemies = FindObjectsByType<Health>(FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            Health hp = enemies[i];
            if (hp == null || !hp.gameObject.activeInHierarchy || !hp.CompareTag("Enemy") || hp.CurrentHealth <= 0f) continue;
            GameObject go = hp.gameObject;
            if (go.GetComponent<BossSpecialAttack>() != null || go.GetComponent<GraveWardenBossAI>() != null) continue;
            if (go.GetComponent<SummonedMinion>() != null) continue;
            if (IsTracked(go)) continue;

            HealerEnemySupport healer = go.GetComponent<HealerEnemySupport>();
            SummonerEnemyAI summoner = go.GetComponent<SummonerEnemyAI>();
            RangedEnemyController ranged = go.GetComponent<RangedEnemyController>();
            bool elite = go.GetComponent<MS7A2EliteMarker>() != null;
            if (healer == null && summoner == null && ranged == null && !elite) continue;
            EnemyController melee = go.GetComponent<EnemyController>();
            EnemySnapshot s = new EnemySnapshot { go = go, healer = healer, summoner = summoner,
                ranged = ranged, melee = melee };
            if (healer != null)
            {
                s.hAmount = healer.healAmount; s.hInterval = healer.healInterval;
                s.ahAmount = r.healerHealAmount; s.ahInterval = r.healerHealInterval;
                healer.healAmount = s.ahAmount; healer.healInterval = s.ahInterval;
            }
            if (summoner != null)
            {
                s.sInterval = summoner.summonInterval;
                s.minions = summoner.minionsPerCast; s.maxMinions = summoner.maxActiveMinions;
                s.asInterval = r.summonInterval; s.aMinions = r.minionsPerCast; s.aMaxMinions = r.maxActiveMinions;
                summoner.summonInterval = s.asInterval;
                summoner.minionsPerCast = s.aMinions; summoner.maxActiveMinions = s.aMaxMinions;
            }
            if (ranged != null)
            {
                s.rInterval = ranged.attackInterval; s.rDamage = ranged.projectileDamage;
                s.rangedGold = ranged.coinReward; s.rangedExp = ranged.expReward;
                s.arInterval = r.rangedAttackInterval; s.arDamage = r.rangedProjectileDamage;
                s.aRangedGold = Scale(s.rangedGold, r.specialGoldMultiplier);
                s.aRangedExp = Scale(s.rangedExp, r.specialExpMultiplier);
                ranged.attackInterval = s.arInterval; ranged.projectileDamage = s.arDamage;
                ranged.coinReward = s.aRangedGold; ranged.expReward = s.aRangedExp;
            }
            if (melee != null && ranged == null)
            {
                s.meleeGold = melee.coinReward; s.meleeExp = melee.expReward;
                s.aMeleeGold = Scale(s.meleeGold, r.specialGoldMultiplier);
                s.aMeleeExp = Scale(s.meleeExp, r.specialExpMultiplier);
                melee.coinReward = s.aMeleeGold; melee.expReward = s.aMeleeExp;
                if (elite)
                {
                    s.meleeDamage = melee.damage;
                    s.aMeleeDamage = s.meleeDamage * r.eliteDamageMultiplier;
                    melee.damage = s.aMeleeDamage;
                }
            }
            enemySnapshots.Add(s);
            LastTunedEnemies++;
        }
        // Keep list short as enemies die; removing destroyed entries doesn't change any gameplay.
        enemySnapshots.RemoveAll(s => s == null || s.go == null);
    }

    private static int Scale(int original, float mult)
    {
        return Mathf.Max(0, Mathf.RoundToInt(Mathf.Clamp((float)original * mult, 0f, 100000f)));
    }

    private bool IsTracked(GameObject go)
    {
        for (int i = 0; i < enemySnapshots.Count; i++)
            if (enemySnapshots[i] != null && enemySnapshots[i].go == go) return true;
        return false;
    }

    private void RestoreDirectors()
    {
        DirectorSnapshot d = directorSnapshot;
        if (d == null) return;
        if (d.healer != null)
        {
            if (Mathf.Approximately(d.healer.spawnInterval, d.ahInterval)) d.healer.spawnInterval = d.hInterval;
            if (d.healer.maxActiveHealers == d.ahActive) d.healer.maxActiveHealers = d.hActive;
            if (d.healer.maxSpawnsPerWave == d.ahWave) d.healer.maxSpawnsPerWave = d.hWave;
        }
        if (d.summoner != null)
        {
            if (Mathf.Approximately(d.summoner.spawnInterval, d.asInterval)) d.summoner.spawnInterval = d.sInterval;
            if (d.summoner.maxActiveSummoners == d.asActive) d.summoner.maxActiveSummoners = d.sActive;
            if (d.summoner.maxSpawnsPerWave == d.asWave) d.summoner.maxSpawnsPerWave = d.sWave;
        }
        directorSnapshot = null;
    }

    private void OnDisable() { RestoreAll(); }
    private void OnDestroy() { RestoreAll(); }

    private void RestoreAll()
    {
        RestoreDirectors();
        for (int i = 0; i < enemySnapshots.Count; i++)
        {
            EnemySnapshot s = enemySnapshots[i];
            if (s == null || s.go == null) continue;
            if (s.healer != null)
            {
                if (Mathf.Approximately(s.healer.healAmount, s.ahAmount)) s.healer.healAmount = s.hAmount;
                if (Mathf.Approximately(s.healer.healInterval, s.ahInterval)) s.healer.healInterval = s.hInterval;
            }
            if (s.summoner != null)
            {
                if (Mathf.Approximately(s.summoner.summonInterval, s.asInterval)) s.summoner.summonInterval = s.sInterval;
                if (s.summoner.minionsPerCast == s.aMinions) s.summoner.minionsPerCast = s.minions;
                if (s.summoner.maxActiveMinions == s.aMaxMinions) s.summoner.maxActiveMinions = s.maxMinions;
            }
            if (s.ranged != null)
            {
                if (Mathf.Approximately(s.ranged.attackInterval, s.arInterval)) s.ranged.attackInterval = s.rInterval;
                if (Mathf.Approximately(s.ranged.projectileDamage, s.arDamage)) s.ranged.projectileDamage = s.rDamage;
                if (s.ranged.coinReward == s.aRangedGold) s.ranged.coinReward = s.rangedGold;
                if (s.ranged.expReward == s.aRangedExp) s.ranged.expReward = s.rangedExp;
            }
            if (s.melee != null && s.ranged == null)
            {
                if (s.melee.coinReward == s.aMeleeGold) s.melee.coinReward = s.meleeGold;
                if (s.melee.expReward == s.aMeleeExp) s.melee.expReward = s.meleeExp;
                if (Mathf.Approximately(s.melee.damage, s.aMeleeDamage) && s.aMeleeDamage > 0f)
                    s.melee.damage = s.meleeDamage;
            }
        }
        enemySnapshots.Clear();
        LastTunedEnemies = 0;
        observedWave = -1;
    }
}
