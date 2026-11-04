using System;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Stage 6A.3 - presentation only, intentionally does NOT edit game state.
// Read Health/Progression/WaveManager through the existing 6A.2 view when available.
[DisallowMultipleComponent]
public sealed class HUD6A3GameStatusView : MonoBehaviour
{
    [Header("Gameplay References")]
    public PlayerProgression playerProgression;
    public WaveManager waveManager;

    [Tooltip("WaveManager.waveText is the source of the current wave countdown.")]
    public TMP_Text waveSourceText;
    [Tooltip("Optional original Canvas/Time text; used when there is no wave countdown.")]
    public TMP_Text timeSourceText;
    [Tooltip("ON: prefer the real wave countdown (28s), otherwise prefer elapsed scene time (03:24).")]
    public bool preferWaveCountdown = true;

    [Header("6A.3 View Objects")]
    public TMP_Text waveLabel;
    public TMP_Text timerLabel;
    public TMP_Text goldLabel;
    public Image waveAccent;
    public Image goldIcon;

    [Header("Very Subtle Visual Feedback")]
    [Min(0.05f)] public float waveFlashSeconds = 0.45f;
    [Min(0.05f)] public float goldFlashSeconds = 0.30f;
    public Color idleAccentColor = new Color(0.60f, 0.40f, 0.87f, 0.75f);
    public Color waveFlashColor = new Color(0.86f, 0.69f, 1f, 1f);
    public Color idleGoldColor = new Color(0.96f, 0.83f, 0.61f, 1f);
    public Color goldFlashColor = new Color(1f, 0.96f, 0.76f, 1f);

    private int observedWave = -1;
    private int observedCoins = int.MinValue;
    private float displayedCoins;
    private bool coinDisplayInitialized;
    private float waveFlashRemaining;
    private float goldFlashRemaining;
    private float nextSearch;
    private float nextRefresh;

    private static readonly Regex ClockPattern = new Regex(@"\b\d{1,2}:\d{2}(?::\d{2})?\b", RegexOptions.Compiled);

    private void OnEnable()
    {
        observedWave = -1;
        observedCoins = int.MinValue;
        coinDisplayInitialized = false;
        ResolveSources();
        RefreshDisplay();
    }

    private void Update()
    {
        // Visual-only animation, does not alter WaveManager/PlayerProgression state.
        float dt = Time.unscaledDeltaTime;
        waveFlashRemaining = Mathf.Max(0f, waveFlashRemaining - dt);
        goldFlashRemaining = Mathf.Max(0f, goldFlashRemaining - dt);

        if (waveAccent != null)
        {
            float t = waveFlashSeconds > 0f ? waveFlashRemaining / waveFlashSeconds : 0f;
            waveAccent.color = Color.Lerp(idleAccentColor, waveFlashColor, t);
        }
        if (goldLabel != null)
        {
            float t = goldFlashSeconds > 0f ? goldFlashRemaining / goldFlashSeconds : 0f;
            goldLabel.color = Color.Lerp(idleGoldColor, goldFlashColor, t);
        }

        if (Time.unscaledTime >= nextSearch)
        {
            nextSearch = Time.unscaledTime + 1.0f;
            if (playerProgression == null || waveManager == null || waveSourceText == null)
                ResolveSources();
        }
        if (Time.unscaledTime >= nextRefresh)
        {
            nextRefresh = Time.unscaledTime + 0.10f;
            RefreshDisplay();
        }

        if (goldLabel != null && coinDisplayInitialized && playerProgression != null)
        {
            float target = Mathf.Max(0, playerProgression.coins);
            // 250 coins/second minimum + dynamic ramp for large pickups.
            float speed = Mathf.Max(250f, Mathf.Abs(target - displayedCoins) * 9f);
            displayedCoins = Mathf.MoveTowards(displayedCoins, target, speed * dt);
            goldLabel.text = Mathf.RoundToInt(displayedCoins).ToString("N0");
        }
    }

    private void ResolveSources()
    {
        var existing = GetComponentInParent<HUD6A2CompactView>();
        if (existing != null)
        {
            if (playerProgression == null) playerProgression = existing.playerProgression;
            if (waveManager == null) waveManager = existing.waveManager;
            if (waveSourceText == null) waveSourceText = existing.existingWaveText;
        }

        if (playerProgression == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerProgression = player.GetComponent<PlayerProgression>();
        }
        if (waveManager == null) waveManager = FindAnyObjectByType<WaveManager>();
        if (waveSourceText == null && waveManager != null) waveSourceText = waveManager.waveText;
    }

    private void RefreshDisplay()
    {
        int wave = waveManager != null ? waveManager.CurrentWave : 0;
        if (wave != observedWave)
        {
            if (observedWave > 0 && wave > 0) waveFlashRemaining = waveFlashSeconds;
            observedWave = wave;
        }
        if (waveLabel != null)
        {
            if (wave > 0)
            {
                int total = waveManager.totalWaves;
                waveLabel.text = total > 0 ? "WAVE " + wave + "/" + total : "WAVE " + wave;
            }
            else waveLabel.text = "WAVE --";
        }

        if (timerLabel != null)
            timerLabel.text = SelectTimeText();

        if (playerProgression != null)
        {
            int coins = Mathf.Max(0, playerProgression.coins);
            if (observedCoins != coins)
            {
                if (observedCoins != int.MinValue) goldFlashRemaining = goldFlashSeconds;
                observedCoins = coins;
            }
            if (!coinDisplayInitialized)
            {
                displayedCoins = coins;
                coinDisplayInitialized = true;
                if (goldLabel != null) goldLabel.text = coins.ToString("N0");
            }
        }
        else if (goldLabel != null)
            goldLabel.text = "--";
    }

    private string SelectTimeText()
    {
        string waveCountdown = ExtractWaveCountdown(waveSourceText != null ? waveSourceText.text : null);
        string elapsedClock = ExtractElapsedClock(timeSourceText != null ? timeSourceText.text : null);
        if (preferWaveCountdown)
            return !string.IsNullOrEmpty(waveCountdown) ? waveCountdown
                 : !string.IsNullOrEmpty(elapsedClock) ? elapsedClock : "--:--";
        return !string.IsNullOrEmpty(elapsedClock) ? elapsedClock
             : !string.IsNullOrEmpty(waveCountdown) ? waveCountdown : "--:--";
    }

    private static string ExtractWaveCountdown(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return null;
        // E.g. "WAVE 3/5 | 28s" or "Nghi truoc Wave 3 | 12s".
        int pipe = source.LastIndexOf('|');
        if (pipe < 0 || pipe + 1 >= source.Length) return null;
        string token = source.Substring(pipe + 1).Trim();
        if (token.EndsWith("s", StringComparison.OrdinalIgnoreCase))
        {
            string number = token.Substring(0, token.Length - 1);
            int seconds;
            if (int.TryParse(number, out seconds) && seconds >= 0)
                return seconds + "s";
        }
        var match = ClockPattern.Match(token);
        return match.Success ? match.Value : null;
    }

    private static string ExtractElapsedClock(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return null;
        var match = ClockPattern.Match(source);
        return match.Success ? match.Value : null;
    }
}
