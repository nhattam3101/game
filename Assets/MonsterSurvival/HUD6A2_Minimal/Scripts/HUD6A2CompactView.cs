using TMPro;
using UnityEngine;
using UnityEngine.UI;

// MonsterSurvival 6A.2 - UI ONLY. No changes to Health, progression, or waves.
[DisallowMultipleComponent]
public sealed class HUD6A2CompactView : MonoBehaviour
{
    [Header("Sources (can auto-resolve at runtime)")]
    public Health playerHealth;
    public PlayerProgression playerProgression;
    public WaveManager waveManager;

    [Tooltip("Optional: old WaveManager waveText containing the existing countdown.")]
    public TMP_Text existingWaveText;

    [Header("Generated graphics")]
    public Image healthImmediateFill;
    public Image healthDelayedFill;
    public Image experienceFill;
    public TMP_Text healthValue;
    public TMP_Text levelValue;
    public TMP_Text experienceValue;
    public TMP_Text goldValue;
    public TMP_Text waveValue;
    public TMP_Text timerValue;

    [Header("Animation")]
    [Min(0.1f)] public float trailingHealthPerSecond = 0.52f;
    [Min(0.1f)] public float expChangePerSecond = 1.85f;

    private float nextLookup;
    private float nextTextRefresh;
    private bool initialized;

    private void Start()
    {
        ResolveSources();
        RefreshText();
    }

    private void Update()
    {
        // No scene changes or polling searches every frame.
        if (Time.unscaledTime >= nextLookup)
        {
            nextLookup = Time.unscaledTime + 1.0f;
            if (playerHealth == null || playerProgression == null || waveManager == null)
                ResolveSources();
        }

        float hpRatio = playerHealth != null && playerHealth.maxHealth > 0f
            ? Mathf.Clamp01(playerHealth.CurrentHealth / playerHealth.maxHealth) : 0f;

        if (!initialized)
        {
            initialized = true;
            if (healthImmediateFill != null) healthImmediateFill.fillAmount = hpRatio;
            if (healthDelayedFill != null) healthDelayedFill.fillAmount = hpRatio;
            if (experienceFill != null) experienceFill.fillAmount = CurrentExpRatio();
        }

        if (healthImmediateFill != null)
            healthImmediateFill.fillAmount = hpRatio;

        if (healthDelayedFill != null)
        {
            // Healing is instant, damage leaves a brief amber-colored trail.
            if (hpRatio > healthDelayedFill.fillAmount)
                healthDelayedFill.fillAmount = hpRatio;
            else
                healthDelayedFill.fillAmount = Mathf.MoveTowards(
                    healthDelayedFill.fillAmount, hpRatio,
                    trailingHealthPerSecond * Time.unscaledDeltaTime);
        }

        if (experienceFill != null)
        {
            float ratio = CurrentExpRatio();
            // On level-up EXP returns close to zero: snap to new level's start.
            if (experienceFill.fillAmount > 0.85f && ratio < 0.3f)
                experienceFill.fillAmount = ratio;
            else
                experienceFill.fillAmount = Mathf.MoveTowards(
                    experienceFill.fillAmount, ratio,
                    expChangePerSecond * Time.unscaledDeltaTime);
        }

        if (Time.unscaledTime >= nextTextRefresh)
        {
            nextTextRefresh = Time.unscaledTime + 0.12f;
            RefreshText();
        }
    }

    private float CurrentExpRatio()
    {
        return playerProgression != null && playerProgression.expToNextLevel > 0
            ? Mathf.Clamp01((float)playerProgression.currentExp / playerProgression.expToNextLevel)
            : 0f;
    }

    private void ResolveSources()
    {
        if (playerHealth == null || playerProgression == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                if (playerHealth == null) playerHealth = player.GetComponent<Health>();
                if (playerProgression == null)
                    playerProgression = player.GetComponent<PlayerProgression>();
            }
        }

        if (waveManager == null)
            waveManager = FindFirstObjectByType<WaveManager>();

        if (existingWaveText == null && waveManager != null)
            existingWaveText = waveManager.waveText;
    }

    private void RefreshText()
    {
        if (healthValue != null)
            healthValue.text = playerHealth != null
                ? Mathf.CeilToInt(Mathf.Max(0, playerHealth.CurrentHealth)) + " / " +
                  Mathf.CeilToInt(Mathf.Max(0, playerHealth.maxHealth))
                : "-- / --";

        if (levelValue != null)
            levelValue.text = playerProgression != null
                ? "LV " + playerProgression.level : "LV --";

        if (experienceValue != null)
            experienceValue.text = playerProgression != null
                ? "EXP " + playerProgression.currentExp + " / " + playerProgression.expToNextLevel
                : "EXP --";

        if (goldValue != null)
            goldValue.text = playerProgression != null
                ? playerProgression.coins.ToString("N0") : "--";

        if (waveValue != null)
            waveValue.text = waveManager != null
                ? "WAVE " + Mathf.Max(0, waveManager.CurrentWave) : "WAVE --";

        if (timerValue != null)
        {
            string countdown = GetCountdownFromExistingText();
            timerValue.text = countdown;
            timerValue.gameObject.SetActive(!string.IsNullOrEmpty(countdown));
        }
    }

    private string GetCountdownFromExistingText()
    {
        if (existingWaveText == null) return string.Empty;
        string text = existingWaveText.text;
        if (string.IsNullOrEmpty(text)) return string.Empty;

        // Common format: "WAVE 3/5 | 28s". Mirror, never create a second timer.
        int pipe = text.LastIndexOf('|');
        if (pipe >= 0 && pipe + 1 < text.Length)
        {
            string part = text.Substring(pipe + 1).Trim();
            if (part.EndsWith("s") || part.Contains(":")) return part;
        }

        // If the old UI has no recognized countdown, display only WAVE, no made-up timer.
        return string.Empty;
    }
}
