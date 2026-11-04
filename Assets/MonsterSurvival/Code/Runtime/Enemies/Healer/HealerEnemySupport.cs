using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Add to a copy of NormalEnemy. EnemyController, Health, HitEffects remain unchanged.
[RequireComponent(typeof(Health))]
public class HealerEnemySupport : MonoBehaviour
{
    [Header("Healing")]
    [Min(0.1f)] public float healInterval = 2f;
    [Min(0.1f)] public float healRadius = 3f;
    [Min(0f)] public float healAmount = 5f;
    [Min(1)] public int maxTargetsPerPulse = 3;
    public LayerMask allyLayers = ~0;
    public bool allowHealingBoss = false;

    [Header("Visual (Optional)")]
    public Animator healerAnimator;
    public string healTrigger = "Heal";
    public SpriteRenderer healPulseSprite;
    [Min(0.05f)] public float pulseDuration = 0.4f;

    private Health selfHealth;
    private float timer;
    private bool dead;
    private bool hasHealTrigger;
    private Coroutine pulseRoutine;
    private Color originalAuraColor;
    private Vector3 originalAuraScale;
    private readonly List<Health> candidates = new List<Health>();
    private readonly HashSet<Health> unique = new HashSet<Health>();

    private void Awake()
    {
        selfHealth = GetComponent<Health>();
        if (healerAnimator == null) healerAnimator = GetComponentInChildren<Animator>();

        if (healerAnimator != null && healerAnimator.runtimeAnimatorController != null)
        {
            foreach (AnimatorControllerParameter p in healerAnimator.parameters)
            {
                if (p.name == healTrigger && p.type == AnimatorControllerParameterType.Trigger)
                {
                    hasHealTrigger = true;
                    break;
                }
            }
        }

        if (healPulseSprite != null)
        {
            originalAuraColor = healPulseSprite.color;
            originalAuraScale = healPulseSprite.transform.localScale;
            healPulseSprite.enabled = false;
        }
        timer = healInterval;
    }

    private void OnEnable()
    {
        if (selfHealth == null) selfHealth = GetComponent<Health>();
        if (selfHealth != null) selfHealth.OnDied += HandleDeath;
    }

    private void OnDisable()
    {
        if (selfHealth != null) selfHealth.OnDied -= HandleDeath;
        if (pulseRoutine != null) { StopCoroutine(pulseRoutine); pulseRoutine = null; }
        if (healPulseSprite != null) healPulseSprite.enabled = false;
    }

    private void HandleDeath() { dead = true; }

    private void Update()
    {
        if (dead || selfHealth == null || selfHealth.CurrentHealth <= 0f || Time.timeScale == 0f)
            return;

        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = Mathf.Max(0.1f, healInterval);
        HealNearbyEnemies();
    }

    private void HealNearbyEnemies()
    {
        candidates.Clear();
        unique.Clear();

        // A low number of healers + 2-second interval keeps this simple query inexpensive.
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, healRadius, allyLayers);
        foreach (Collider2D hit in hits)
        {
            Health target = hit.GetComponentInParent<Health>();
            if (target == null || target == selfHealth || !unique.Add(target)) continue;
            if (!target.CompareTag("Enemy")) continue;
            // Keep Shadow Titan out of the heal pool by default.
            if (!allowHealingBoss && target.GetComponent<BossSpecialAttack>() != null) continue;
            if (target.CurrentHealth <= 0f || target.maxHealth <= 0f) continue;
            if (target.CurrentHealth >= target.maxHealth) continue;
            candidates.Add(target);
        }

        // Prioritize the most injured ally by HP percentage.
        candidates.Sort((a, b) =>
            (a.CurrentHealth / a.maxHealth).CompareTo(b.CurrentHealth / b.maxHealth));

        int count = Mathf.Min(maxTargetsPerPulse, candidates.Count);
        if (count == 0) return;

        for (int i = 0; i < count; i++) candidates[i].Heal(healAmount);

        if (hasHealTrigger && healerAnimator != null)
            healerAnimator.SetTrigger(healTrigger);

        if (healPulseSprite != null)
        {
            if (pulseRoutine != null) StopCoroutine(pulseRoutine);
            pulseRoutine = StartCoroutine(PlayPulse());
        }
    }

    private IEnumerator PlayPulse()
    {
        healPulseSprite.enabled = true;
        float elapsed = 0f;
        while (elapsed < pulseDuration && healPulseSprite != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, pulseDuration));
            healPulseSprite.transform.localScale = originalAuraScale * Mathf.Lerp(0.75f, 1.2f, t);
            Color c = originalAuraColor;
            c.a = originalAuraColor.a * (1f - t);
            healPulseSprite.color = c;
            yield return null;
        }
        if (healPulseSprite != null)
        {
            healPulseSprite.enabled = false;
            healPulseSprite.color = originalAuraColor;
            healPulseSprite.transform.localScale = originalAuraScale;
        }
        pulseRoutine = null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.25f, 1f, 0.4f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, healRadius);
    }
}
