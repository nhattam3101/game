
using System;
using UnityEngine;

public class Health : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 100f;

    public float CurrentHealth { get; private set; }

    public event Action<float, float> OnHealthChanged;
    public event Action OnDied;

    private bool isDead;

    private void Awake()
    {
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

        // Player duoc kiem tra la chan.
        if (CompareTag("Player"))
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
        CurrentHealth = Mathf.Max(
            0f,
            CurrentHealth - amount
        );

        OnHealthChanged?.Invoke(
            CurrentHealth,
            maxHealth
        );

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

            if (CompareTag("Player"))
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
}
