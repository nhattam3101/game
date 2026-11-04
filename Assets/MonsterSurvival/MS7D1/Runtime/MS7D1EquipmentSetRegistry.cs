using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 7D.1 foundation: evaluates owned SET PIECES from the three equipped slots.
/// PREVIEW ONLY: no PlayerStats modifications, buffs, SetActive calls, Save or PlayerPrefs.
/// 7D.2 will attach actual effects AFTER validating the existing Equipment API.
/// </summary>
[DisallowMultipleComponent]
public sealed class MS7D1EquipmentSetRegistry : MonoBehaviour
{
    [Serializable]
    public sealed class Rule
    {
        public bool enabledRule;
        public string setId = "";
        public string displayName = "";
        [Tooltip("Match EXACT equipment item IDs, never bag entries. Two or three unique IDs per set.")]
        public string[] pieceIds = new string[3];
        [Header("PREVIEW numbers only - NOT applied to PlayerStats in 7D.1")]
        [Range(0f, 1f)] public float damageBonusAtTwo = 0.08f;
        [Range(0f, 1f)] public float attackSpeedBonusAtThree = 0.12f;
    }

    public sealed class Status
    {
        public string id;
        public string name;
        public int matched;
        public int total;
        public bool twoPieceReady;
        public bool threePieceReady;
        public bool complete;
        public float previewTwoPieceDamage;
        public float previewThreePieceAttackSpeed;
        public override string ToString()
        {
            return name + " | " + matched + "/" + total +
                " | 2-piece=" + (twoPieceReady ? "READY" : "LOCKED") +
                " | 3-piece=" + (threePieceReady ? "READY" : "LOCKED") +
                " | full=" + (complete ? "READY" : "LOCKED") +
                " | PREVIEW ONLY";
        }
    }

    [Header("Live 3-slot adapter, on the same Player")]
    public MS7D1EquippedItemReader reader;
    [Header("Set rules: add verified real item IDs in Inspector")]
    public List<Rule> sets = new List<Rule>();
    [Header("Read-only preview monitoring")]
    [Min(.1f)] public float refreshSeconds = .35f;
    public bool logTierChanges = true;

    private float nextRefresh;
    private readonly Dictionary<string, int> previousTier = new Dictionary<string, int>();

    private void Awake()
    {
        if (reader == null) reader = GetComponent<MS7D1EquippedItemReader>();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + Mathf.Max(.1f, refreshSeconds);
        if (!logTierChanges) return;
        List<Status> statuses;
        string diagnostic;
        if (!TryPreview(out statuses, out diagnostic)) return;
        foreach (Status s in statuses) {
            int tier = s.threePieceReady ? 3 : (s.twoPieceReady ? 2 : 0);
            int oldTier;
            if (!previousTier.TryGetValue(s.id, out oldTier)) { previousTier[s.id] = tier; continue; }
            if (tier != oldTier) {
                previousTier[s.id] = tier;
                Debug.Log("[7D.1] SET PREVIEW (no stat applied): " + s.name + " => tier " + tier, this);
            }
        }
    }

    public bool TryPreview(out List<Status> results, out string diagnostic)
    {
        results = new List<Status>();
        diagnostic = "";
        if (reader == null) { diagnostic = "Missing MS7D1EquippedItemReader"; return false; }
        string[] equipped;
        if (!reader.TryRead(out equipped, out diagnostic)) return false;
        if (sets == null) return true;
        var registered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Rule rule in sets) {
            if (rule == null || !rule.enabledRule) continue;
            if (string.IsNullOrWhiteSpace(rule.setId) || !registered.Add(rule.setId.Trim())) {
                diagnostic += " | Duplicate/empty setId skipped";
                continue;
            }
            int total = CountValidPieces(rule.pieceIds);
            if (total < 2 || total > 3) {
                diagnostic += " | Invalid set definition skipped: " + rule.setId;
                continue;
            }
            int matched = CountMatched(rule.pieceIds, equipped);
            results.Add(new Status {
                id = rule.setId,
                name = string.IsNullOrWhiteSpace(rule.displayName) ? rule.setId : rule.displayName,
                matched = matched,
                total = total,
                twoPieceReady = matched >= 2,
                threePieceReady = total == 3 && matched >= 3,
                complete = matched >= total,
                previewTwoPieceDamage = rule.damageBonusAtTwo,
                previewThreePieceAttackSpeed = rule.attackSpeedBonusAtThree
            });
        }
        return true;
    }

    private static int CountValidPieces(string[] ids)
    {
        if (ids == null) return 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string id in ids)
            if (!string.IsNullOrWhiteSpace(id)) seen.Add(id.Trim());
        return seen.Count;
    }

    /// <summary>Pure formula: each actual equipped slot is used at most once.</summary>
    public static int CountMatched(string[] required, string[] equipped)
    {
        if (required == null || equipped == null) return 0;
        var seenRequired = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenEquipped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string item in equipped)
            if (!string.IsNullOrWhiteSpace(item)) seenEquipped.Add(item.Trim());
        int count = 0;
        foreach (string item in required) {
            if (string.IsNullOrWhiteSpace(item)) continue;
            string id = item.Trim();
            if (!seenRequired.Add(id)) continue;
            if (seenEquipped.Contains(id)) count++;
        }
        return count;
    }
}
