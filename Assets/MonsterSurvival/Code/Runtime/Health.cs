
using System;
using UnityEngine;

public class Health : MonoBehaviour
{
    [Header("Health Settings")]
    [Min(1f)]
    public float maxHealth = 100f;

    [Header("Player Damage Protection")]
    [Tooltip("Brief protection after taking real damage. Only applies to the Player tag.")]
    [Min(0f)] public float playerHitInvulnerability = 0.18f;

    public float CurrentHealth { get; private set; }
    public bool IsDead => isDead;
    public bool IsInvulnerable => Time.time < invulnerableUntil;
    public float NormalizedHealth => maxHealth > 0f ? CurrentHealth / maxHealth : 0f;

    public event Action<float, float> OnHealthChanged;
    public event Action<float, Health> OnDamaged;
    public event Action OnDied;

    private bool isDead;
    private float invulnerableUntil;

    private void Awake()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        CurrentHealth = maxHealth;

        OnHealthChanged?.Invoke(
            CurrentHealth,
            maxHealth
        );
    }

    // Ham cu: giu tuong thich voi Bullet.cs.
    public void TakeDamage(float amount)
    {
        TakeDamage(amount, null);
    }

    // Ham moi: co thong tin ke tan cong.
    public void TakeDamage(
        float amount,
        Health attacker
    )
    {
        if (isDead || amount <= 0f)
            return;

        bool isPlayer = CompareTag("Player");
        if (isPlayer && IsInvulnerable)
            return;

        // Player duoc kiem tra la chan.
        if (isPlayer)
        {
            PlayerShield shield =
                GetComponent<PlayerShield>();

            if (shield != null &&
                shield.TryBlockHit(amount, attacker))
            {
                // La chan da chan sat thuong.
                return;
            }
        }

        // Neu khong duoc chan, tru HP.
        float healthBefore = CurrentHealth;
        CurrentHealth = Mathf.Max(
            0f,
            CurrentHealth - amount
        );
        float damageTaken = healthBefore - CurrentHealth;

        if (isPlayer && CurrentHealth > 0f && playerHitInvulnerability > 0f)
            GrantInvulnerability(playerHitInvulnerability);

        OnHealthChanged?.Invoke(
            CurrentHealth,
            maxHealth
        );

        OnDamaged?.Invoke(damageTaken, attacker);

        Debug.Log(
            gameObject.name +
            " nhan " + amount +
            " damage. HP: " +
            CurrentHealth + "/" + maxHealth
        );

        if (CurrentHealth <= 0f)
        {
            isDead = true;

            OnDied?.Invoke();

            if (isPlayer)
            {
                Debug.Log("Player da chet!");
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }

    public void Heal(float amount)
    {
        if (isDead || amount <= 0f)
            return;

        CurrentHealth = Mathf.Min(
            maxHealth,
            CurrentHealth + amount
        );

        OnHealthChanged?.Invoke(
            CurrentHealth,
            maxHealth
        );
    }

    public void GrantInvulnerability(float duration)
    {
        if (duration <= 0f || isDead)
            return;

        invulnerableUntil = Mathf.Max(
            invulnerableUntil,
            Time.time + duration
        );
    }

    public void SetMaxHealth(float value, bool healToFull)
    {
        float previousMax = Mathf.Max(1f, maxHealth);
        float previousRatio = Mathf.Clamp01(CurrentHealth / previousMax);

        maxHealth = Mathf.Max(1f, value);
        CurrentHealth = healToFull
            ? maxHealth
            : Mathf.Clamp(maxHealth * previousRatio, 0f, maxHealth);

        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }
}
