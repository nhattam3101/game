
using UnityEngine;

public class WaveBossAudioController : MonoBehaviour
{
    [Header("Game References")]
    public WaveManager waveManager;
    public GameManager gameManager;

    [Header("Wave Audio")]
    public AudioClip waveStartSound;

    [Range(0f, 1f)]
    public float waveStartVolume = 0.7f;

    [Header("Boss Audio")]
    public AudioClip bossWarningSound;
    public AudioClip bossMusic;

    [Range(0f, 1f)]
    public float bossWarningVolume = 0.9f;

    [Header("Victory Audio")]
    public AudioClip victorySound;

    [Range(0f, 1f)]
    public float victoryVolume = 0.9f;

    private int lastWave = 0;
    private bool victorySoundPlayed = false;
    private bool bossMusicStarted = false;

    private void Start()
    {
        if (waveManager == null)
        {
            Debug.LogWarning(
                "WaveBossAudio: Chua gan WaveManager!"
            );
        }

        if (gameManager == null)
        {
            Debug.LogWarning(
                "WaveBossAudio: Chua gan GameManager!"
            );
        }
    }

    private void Update()
    {
        CheckVictory();
        CheckWaveChange();
    }

    private void CheckWaveChange()
    {
        if (waveManager == null)
            return;

        if (gameManager != null &&
            gameManager.IsGameOver)
        {
            return;
        }

        int currentWave = waveManager.CurrentWave;

        if (currentWave <= 0 ||
            currentWave == lastWave)
        {
            return;
        }

        lastWave = currentWave;

        Debug.Log(
            "Audio: Bat dau Wave " + currentWave
        );

        // Wave Boss
        if (currentWave == waveManager.bossWave)
        {
            PlaySFX(
                bossWarningSound,
                bossWarningVolume
            );

            StartBossMusic();

            return;
        }

        // Cac Wave thong thuong
        PlaySFX(
            waveStartSound,
            waveStartVolume
        );
    }

    private void StartBossMusic()
    {
        if (bossMusicStarted)
            return;

        if (bossMusic == null)
        {
            Debug.LogWarning(
                "WaveBossAudio: Chua gan Boss Music!"
            );

            return;
        }

        if (AudioManager.Instance == null)
            return;

        AudioSource musicSource =
            AudioManager.Instance.musicSource;

        if (musicSource == null)
            return;

        // Thay nhac thuong bang nhac Boss
        musicSource.Stop();

        musicSource.clip = bossMusic;
        musicSource.loop = true;

        musicSource.Play();

        bossMusicStarted = true;

        Debug.Log("Boss Music Started!");
    }

    private void CheckVictory()
    {
        if (victorySoundPlayed)
            return;

        if (gameManager == null ||
            gameManager.victoryPanel == null)
        {
            return;
        }

        // Chi phat khi Victory Panel thuc su mo
        if (!gameManager.victoryPanel.activeInHierarchy)
            return;

        victorySoundPlayed = true;

        // Dung nhac nen khi chien thang
        if (AudioManager.Instance != null &&
            AudioManager.Instance.musicSource != null)
        {
            AudioManager.Instance.musicSource.Stop();
        }

        PlaySFX(
            victorySound,
            victoryVolume
        );

        Debug.Log("Victory Sound Played!");
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
}
