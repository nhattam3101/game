using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// MonsterSurvival 7D.2: runtime-only equipment set stats applied to PlayerStats.
/// NO Save writes and NO edits to other gameplay scripts.
/// - Requires explicit confirmation that 7D.1 maps the REAL three equipment slots.
/// - Adds only the difference between the new and previously applied bonuses.
/// - Rolls back its own contributions on unequip, invalid source, or disable.
/// - Does not modify permanent upgrades or the base combat system.
/// IMPORTANT: uses ADDITIVE multiplier points, matching PlayerStats' existing + skill bonuses.
/// </summary>
[DisallowMultipleComponent]
public sealed class MS7D2SetBonusApplier : MonoBehaviour
{
    [Header("Existing 7D.1 components - on the SAME Player")]
    public PlayerStats playerStats;
    public MS7D1EquippedItemReader equipmentReader;
    public MS7D1EquipmentSetRegistry setRegistry;

    [Header("Safety: enable both AFTER you validate real Equipment slots")]
    [Tooltip("Tick ONLY AFTER 7D.1 Audit showed real IDs when EQUIPPING and UNEQUIPPING items.")]
    public bool mappingVerifiedByUser = false;
    [Tooltip("OFF by default. ON applies actual bonuses in Play Mode only.")]
    public bool enableActualBonuses = false;

    [Header("Existing float fields in PlayerStats - case-sensitive")]
    public string damageMultiplierField = "damageMultiplier";
    public string attackSpeedMultiplierField = "attackSpeedMultiplier";

    [Header("Compatibility rules and upper bounds")]
    [Tooltip("ON: one item ID may not count toward two different set rules.")]
    public bool prohibitPiecesSharedBetweenSets = true;
    [Range(0f, 2f)] public float maxTotalDamageBonus = 0.6f;
    [Range(0f, 2f)] public float maxTotalAttackSpeedBonus = 0.6f;
    [Min(0.1f)] public float pollInterval = 0.25f;
    public bool logChanges = true;

    private float nextPoll;
    private float appliedDamage;
    private float appliedAttack;
    private PlayerStats appliedTarget;
    private FieldInfo appliedDamageField;
    private FieldInfo appliedAttackField;
    private string lastDiagnostic = "Not checked yet";
    private string previousError = "";
    private static MS7D2SetBonusApplier activeOwner;

    public float AppliedDamageBonus { get { return appliedDamage; } }
    public float AppliedAttackSpeedBonus { get { return appliedAttack; } }
    public string LastDiagnostic { get { return lastDiagnostic; } }
    public bool CurrentlyApplying { get { return appliedDamage > 0.00001f || appliedAttack > 0.00001f; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() { activeOwner = null; }

    private void Awake() { FindOwnReferences(); }

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        if (activeOwner != null && activeOwner != this)
        {
            Debug.LogError("[7D.2] Duplicate active Set Bonus Applier detected. This component will disable itself.", this);
            enabled = false;
            return;
        }
        activeOwner = this;
    }

    private void FindOwnReferences()
    {
        if (playerStats == null) playerStats = GetComponent<PlayerStats>();
        if (equipmentReader == null) equipmentReader = GetComponent<MS7D1EquippedItemReader>();
        if (setRegistry == null) setRegistry = GetComponent<MS7D1EquipmentSetRegistry>();
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        if (Time.unscaledTime < nextPoll) return;
        nextPoll = Time.unscaledTime + Mathf.Max(0.1f, pollInterval);
        TickNow();
    }

    /// <summary>Safe to call from Play Mode diagnostics; may adjust stats ONLY if both explicit gates are ON.</summary>
    public void TickNow()
    {
        if (!Application.isPlaying) return;
        FindOwnReferences();
        if (activeOwner != this) return;
        if (!mappingVerifiedByUser || !enableActualBonuses)
        {
            RevertAppliedBonuses();
            lastDiagnostic = "PREVIEW ONLY: mappingVerifiedByUser and enableActualBonuses must BOTH be ON";
            previousError = "";
            return;
        }
        float wantDamage, wantAttack;
        string reason;
        if (!TryEvaluate(out wantDamage, out wantAttack, out reason))
        {
            RevertAppliedBonuses();
            lastDiagnostic = "NOT APPLIED: " + reason;
            if (reason != previousError)
            {
                Debug.LogWarning("[7D.2] " + lastDiagnostic, this);
                previousError = reason;
            }
            return;
        }
        previousError = "";
        FieldInfo dmgField = FindFloatField(playerStats.GetType(), damageMultiplierField);
        FieldInfo aspdField = FindFloatField(playerStats.GetType(), attackSpeedMultiplierField);
        if (dmgField == null || aspdField == null || dmgField == aspdField)
        {
            RevertAppliedBonuses();
            lastDiagnostic = "NOT APPLIED: invalid PlayerStats fields; expected two distinct writable float fields";
            return;
        }
        if (appliedTarget != null && (appliedTarget != playerStats || appliedDamageField != dmgField || appliedAttackField != aspdField))
            RevertAppliedBonuses();
        // Don't rewrite unchanged values: other skills/permanent upgrades are free to update their own stats.
        if (Mathf.Abs(wantDamage - appliedDamage) < 0.000001f &&
            Mathf.Abs(wantAttack - appliedAttack) < 0.000001f)
        {
            lastDiagnostic = "LIVE bonus already stable (no repeated addition)";
            return;
        }
        try
        {
            float existingDamage = (float)dmgField.GetValue(playerStats);
            float existingAttack = (float)aspdField.GetValue(playerStats);
            float nextDamage = ReplaceContribution(existingDamage, appliedDamage, wantDamage);
            float nextAttack = ReplaceContribution(existingAttack, appliedAttack, wantAttack);
            if (!FinitePositive(nextDamage) || !FinitePositive(nextAttack))
            {
                lastDiagnostic = "NOT APPLIED: computed multiplier invalid; check external stat writers";
                Debug.LogError("[7D.2] " + lastDiagnostic, this);
                return;
            }
            // Every precondition passed. Roll back the first write if the second write fails.
            // This prevents an untracked, partial stat bonus in the exceptional case.
            dmgField.SetValue(playerStats, nextDamage);
            try
            {
                aspdField.SetValue(playerStats, nextAttack);
            }
            catch
            {
                dmgField.SetValue(playerStats, existingDamage);
                throw;
            }
            appliedTarget = playerStats;
            appliedDamageField = dmgField;
            appliedAttackField = aspdField;
            appliedDamage = wantDamage;
            appliedAttack = wantAttack;
            lastDiagnostic = "APPLIED damage +" + wantDamage.ToString("0.###") +
                             "; attack speed +" + wantAttack.ToString("0.###") + " multiplier points";
            if (logChanges) Debug.Log("[7D.2] " + lastDiagnostic, this);
        }
        catch (Exception e)
        {
            lastDiagnostic = "NOT APPLIED: " + e.GetType().Name + ": " + e.Message;
            Debug.LogError("[7D.2] " + lastDiagnostic, this);
        }
    }

    /// <summary>Read-only computation of totals from 7D.1's three EQUIPPED slots.</summary>
    public bool TryEvaluate(out float damage, out float attack, out string diagnostic)
    {
        damage = 0f;
        attack = 0f;
        diagnostic = "";
        FindOwnReferences();
        if (playerStats == null || equipmentReader == null || setRegistry == null)
        {
            diagnostic = "PlayerStats, reader or registry missing";
            return false;
        }
        if (playerStats.gameObject != gameObject || equipmentReader.gameObject != gameObject ||
            setRegistry.gameObject != gameObject || setRegistry.reader != equipmentReader)
        {
            diagnostic = "Expected 7D.1 reader, registry and PlayerStats on this same Player";
            return false;
        }
        if (equipmentReader.equipmentSource == null ||
            !equipmentReader.equipmentSource.gameObject.scene.IsValid() ||
            !equipmentReader.equipmentSource.gameObject.scene.isLoaded)
        {
            diagnostic = "Equipment source must be an existing scene component";
            return false;
        }
        if (FindFloatField(playerStats.GetType(), damageMultiplierField) == null ||
            FindFloatField(playerStats.GetType(), attackSpeedMultiplierField) == null ||
            damageMultiplierField == attackSpeedMultiplierField)
        {
            diagnostic = "PlayerStats float fields not found; do not patch PlayerStats blindly";
            return false;
        }
        if (setRegistry.sets == null)
        {
            diagnostic = "Set rule list missing";
            return false;
        }
        int enabled = 0;
        var seenSets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenPiecesAcrossSets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (MS7D1EquipmentSetRegistry.Rule rule in setRegistry.sets)
        {
            if (rule == null || !rule.enabledRule) continue;
            enabled++;
            if (string.IsNullOrWhiteSpace(rule.setId) || !seenSets.Add(rule.setId.Trim()))
            {
                diagnostic = "Empty/duplicate set ID";
                return false;
            }
            var pieces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (rule.pieceIds != null)
                foreach (string piece in rule.pieceIds)
                {
                    if (string.IsNullOrWhiteSpace(piece)) continue;
                    if (!pieces.Add(piece.Trim()))
                    {
                        diagnostic = "Duplicate item ID inside set: " + rule.setId;
                        return false;
                    }
                    if (prohibitPiecesSharedBetweenSets && !seenPiecesAcrossSets.Add(piece.Trim()))
                    {
                        diagnostic = "One item ID used by multiple sets: " + piece.Trim();
                        return false;
                    }
                }
            if (pieces.Count < 2 || pieces.Count > 3)
            {
                diagnostic = "Set must contain 2 or 3 DISTINCT equipment item IDs: " + rule.setId;
                return false;
            }
            if (!ValidBonus(rule.damageBonusAtTwo) || !ValidBonus(rule.attackSpeedBonusAtThree))
            {
                diagnostic = "Invalid bonus number on set " + rule.setId;
                return false;
            }
        }
        if (enabled == 0) { diagnostic = "No enabled valid set rules"; return false; }
        List<MS7D1EquipmentSetRegistry.Status> statuses;
        string adapter;
        if (!setRegistry.TryPreview(out statuses, out adapter))
        {
            diagnostic = "7D.1 reader failed: " + adapter;
            return false;
        }
        if (statuses == null || statuses.Count != enabled)
        {
            diagnostic = "7D.1 skipped a set; inspect duplicate/invalid rules";
            return false;
        }
        foreach (MS7D1EquipmentSetRegistry.Status s in statuses)
        {
            if (s.twoPieceReady) damage += s.previewTwoPieceDamage;
            if (s.threePieceReady) attack += s.previewThreePieceAttackSpeed;
        }
        if (!ValidBonus(damage) || !ValidBonus(attack) ||
            damage > maxTotalDamageBonus + 0.00001f ||
            attack > maxTotalAttackSpeedBonus + 0.00001f)
        {
            diagnostic = "Total set bonuses exceed safety bounds; check rules";
            damage = attack = 0f;
            return false;
        }
        diagnostic = "VALID: " + enabled + " set rules; " + adapter +
                     "; wanted damage +" + damage.ToString("0.###") +
                     " attack +" + attack.ToString("0.###");
        return true;
    }

    private static bool ValidBonus(float value) { return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f; }
    private static bool FinitePositive(float value) { return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f; }

    private static FieldInfo FindFloatField(Type type, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public |
                                   BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (Type t = type; t != null; t = t.BaseType)
        {
            FieldInfo f = t.GetField(name, flags);
            if (f != null) return f.FieldType == typeof(float) && !f.IsInitOnly && !f.IsStatic ? f : null;
        }
        return null;
    }

    public static float ReplaceContribution(float current, float previousOwnContribution, float nextOwnContribution)
    {
        return current - previousOwnContribution + nextOwnContribution;
    }

    private void RevertAppliedBonuses()
    {
        if (appliedTarget == null || appliedDamageField == null || appliedAttackField == null)
        {
            appliedTarget = null; appliedDamageField = null; appliedAttackField = null;
            appliedDamage = appliedAttack = 0f;
            return;
        }
        try
        {
            float d = (float)appliedDamageField.GetValue(appliedTarget);
            float a = (float)appliedAttackField.GetValue(appliedTarget);
            float nextD = d - appliedDamage;
            float nextA = a - appliedAttack;
            if (FinitePositive(nextD) && FinitePositive(nextA))
            {
                appliedDamageField.SetValue(appliedTarget, nextD);
                appliedAttackField.SetValue(appliedTarget, nextA);
                if (logChanges && (appliedDamage > 0f || appliedAttack > 0f))
                    Debug.Log("[7D.2] Removed ONLY own set bonuses: damage " + appliedDamage + ", attack " + appliedAttack, this);
            }
            else Debug.LogError("[7D.2] Cannot safely reverse contributions. External stat writer may have reset PlayerStats. Stopping further edits.", this);
        }
        catch (Exception e) { Debug.LogError("[7D.2] Rollback failed: " + e.Message, this); }
        appliedTarget = null; appliedDamageField = null; appliedAttackField = null;
        appliedDamage = appliedAttack = 0f;
    }

    private void OnDisable()
    {
        if (Application.isPlaying) RevertAppliedBonuses();
        if (activeOwner == this) activeOwner = null;
    }

    private void OnDestroy()
    {
        if (Application.isPlaying) RevertAppliedBonuses();
        if (activeOwner == this) activeOwner = null;
    }
}
