
using UnityEngine;

[RequireComponent(typeof(Health))]
public class EnemyAudioEvents : MonoBehaviour
{
    [Header("Audio Clips")]
    public AudioClip hitSound;
    public AudioClip deathSound;

    [Header("Volumes")]
    [Range(0f, 1f)]
    public float hitVolume = 0.2f;

    [Range(0f, 1f)]
    public float deathVolume = 0.5f;

    [Header("Prevent Sound Spam")]
    [Min(0f)]
    public float minHitSoundSpacing = 0.06f;

    private Health enemyHealth;
    private float previousHealth;

    // Gioi han tieng trung dan tren toan bo quai
    private static float nextHitSoundTime;

    private void Start()
    {
        enemyHealth = GetComponent<Health>();

        if (enemyHealth == null)
        {
            Debug.LogWarning(
                "EnemyAudioEvents: Khong tim thay Health!"
            );

            return;
        }

        previousHealth = enemyHealth.CurrentHealth;

        enemyHealth.OnHealthChanged +=
            HandleHealthChanged;

        enemyHealth.OnDied +=
            HandleEnemyDeath;
    }

    private void HandleHealthChanged(
        float currentHealth,
        float maxHealth)
    {
        bool tookDamage =
            currentHealth < previousHealth;

        previousHealth = currentHealth;

        // Neu HP ve 0, chi phat tieng chet.
        if (!tookDamage || currentHealth <= 0f)
            return;

        // Han che qua nhieu tieng trung dan
        // phat cung luc.
        float now = Time.unscaledTime;

        if (now < nextHitSoundTime)
            return;

        nextHitSoundTime =
            now + minHitSoundSpacing;

        PlaySFX(hitSound, hitVolume);
    }

    private void HandleEnemyDeath()
    {
        PlaySFX(deathSound, deathVolume);
    }

    private void PlaySFX(
        AudioClip clip,
        float volume)
    {
        if (clip == null)
            return;

        if (AudioManager.Instance == null)
            return;

        AudioSource source =
            AudioManager.Instance.sfxSource;

        if (source == null)
            return;

        source.PlayOneShot(
            clip,
            Mathf.Clamp01(volume)
        );
    }

    private void OnDestroy()
    {
        if (enemyHealth == null)
            return;

        enemyHealth.OnHealthChanged -=
            HandleHealthChanged;

        enemyHealth.OnDied -=
            HandleEnemyDeath;
    }
}
