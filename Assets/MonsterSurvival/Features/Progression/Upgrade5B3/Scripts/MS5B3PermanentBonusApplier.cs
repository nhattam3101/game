using System.Collections.Generic;
using UnityEngine;

// 5B.3: Apply permanent bonuses AFTER 5A.3's [DefaultExecutionOrder(-100)]
// Inventory/Equipment restoration, but BEFORE normal Player logic starts.
// Never writes profiles; never calls SkillManager or Equipment's Equip methods.
[DisallowMultipleComponent]
[DefaultExecutionOrder(-75)]
public sealed class MS5B3PermanentBonusApplier : MonoBehaviour
{
    [Header("Existing components on the SAME Player")]
    public PlayerStats playerStats;
    public Health playerHealth;

    [Header("Diagnostics (read-only at runtime)")]
    [SerializeField] private int lockedProfileSlot;
    [SerializeField] private bool profileReadSuccessfully;
    [SerializeField] private bool applied;
    [SerializeField] private string status = "Not started";
    [SerializeField] private MS5B3BonusValues bonuses;

    private bool started;
    private bool initialVitalityGranted;

    // Do not allow two 5B.3 components on different GameObjects to modify the same Player.
    private static readonly Dictionary<PlayerStats, MS5B3PermanentBonusApplier> Owners =
        new Dictionary<PlayerStats, MS5B3PermanentBonusApplier>();

    public int LockedProfileSlot { get { return lockedProfileSlot; } }
    public bool ProfileReadSuccessfully { get { return profileReadSuccessfully; } }
    public bool IsApplied { get { return applied; } }
    public string Status { get { return status; } }
    public MS5B3BonusValues AppliedBonuses { get { return applied ? bonuses : default(MS5B3BonusValues); } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearOwnersBetweenPlaySessions() { Owners.Clear(); }

    private void Awake()
    {
        Resolve();
    }

    private void Start()
    {
        if (started) return;
        started = true;
        Resolve();
        lockedProfileSlot = MS5A2GoldBank.SelectedSlot; // lock for the whole run
        if (!playerStats || !playerHealth || playerStats.gameObject != playerHealth.gameObject ||
            playerStats.gameObject != gameObject)
        {
            status = "BLOCKED: attach to Player with PlayerStats and Health on same GameObject";
            Debug.LogError("[5B.3] " + status, this);
            return;
        }
        MS5B1PermanentUpgrades.Snapshot profile;
        string readMessage;
        if (!MS5B1PermanentUpgrades.TryGetSnapshot(lockedProfileSlot, out profile, out readMessage))
        {
            status = "BLOCKED: invalid save: " + readMessage;
            Debug.LogError("[5B.3] " + status, this);
            return;
        }
        string mathError;
        if (!MS5B3BonusMath.TryFromSnapshot(profile, out bonuses, out mathError))
        {
            status = "BLOCKED: " + mathError;
            Debug.LogError("[5B.3] " + status, this);
            return;
        }
        profileReadSuccessfully = true;
        ApplyIfNeeded();
        if (applied)
        {
            status = "APPLIED profile " + lockedProfileSlot + " (" + readMessage + ") | " +
                     "HP +" + bonuses.maxHealth + ", Damage +" + bonuses.damage +
                     ", ASPD +" + bonuses.attackSpeed + ", Move +" + bonuses.moveSpeed;
            Debug.Log("[5B.3] " + status, this);
        }
    }

    private void Resolve()
    {
        if (!playerStats) playerStats = GetComponent<PlayerStats>();
        if (!playerHealth) playerHealth = GetComponent<Health>();
    }

    private void OnEnable()
    {
        // When re-enabled later, reuse the SAME locked snapshot, never query a different profile.
        // This also avoids a repeat starting-HP heal.
        if (Application.isPlaying && started && profileReadSuccessfully && !applied)
            ApplyIfNeeded();
    }

    private void ApplyIfNeeded()
    {
        if (applied || !profileReadSuccessfully || !playerStats || !playerHealth) return;
        MS5B3PermanentBonusApplier owner;
        if (Owners.TryGetValue(playerStats, out owner) && owner != null && owner != this && owner.IsApplied)
        {
            status = "BLOCKED: another 5B.3 applier already owns this Player";
            Debug.LogError("[5B.3] " + status, this);
            return;
        }
        Owners[playerStats] = this;
        playerStats.damageMultiplier += bonuses.damage;
        playerStats.attackSpeedMultiplier += bonuses.attackSpeed;
        playerStats.moveSpeedMultiplier += bonuses.moveSpeed;
        if (bonuses.maxHealth > 0f)
        {
            playerHealth.maxHealth += bonuses.maxHealth;
            if (!initialVitalityGranted)
            {
                // At the beginning of a run, grant starting HP up to the increased cap.
                // On later component re-enable, do NOT grant free healing again.
                playerHealth.Heal(bonuses.maxHealth);
            }
            else
            {
                playerHealth.Heal(0.000001f); // refresh listeners only
            }
        }
        initialVitalityGranted = true;
        applied = true;
    }

    private void OnDisable()
    {
        RemoveOnlyOwnBonus();
    }

    private void OnDestroy()
    {
        RemoveOnlyOwnBonus();
    }

    private void RemoveOnlyOwnBonus()
    {
        if (!applied) return;
        if (playerStats)
        {
            playerStats.damageMultiplier -= bonuses.damage;
            playerStats.attackSpeedMultiplier -= bonuses.attackSpeed;
            playerStats.moveSpeedMultiplier -= bonuses.moveSpeed;
            MS5B3PermanentBonusApplier currentOwner;
            if (Owners.TryGetValue(playerStats, out currentOwner) && currentOwner == this)
                Owners.Remove(playerStats);
        }
        if (playerHealth && bonuses.maxHealth > 0f)
        {
            playerHealth.maxHealth = Mathf.Max(1f, playerHealth.maxHealth - bonuses.maxHealth);
            // Health.Heal clamps CurrentHealth when maxHealth decreases and updates HUD.
            playerHealth.Heal(0.000001f);
        }
        applied = false;
    }
}
