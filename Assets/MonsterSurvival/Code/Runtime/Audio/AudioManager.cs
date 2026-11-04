
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance
    {
        get;
        private set;
    }

    [Header("Audio Sources")]
    public AudioSource musicSource;
    public AudioSource sfxSource;

    [Header("Audio Mixer")]
    public AudioMixer audioMixer;

    [Header("Music")]
    public AudioClip battleMusic;

    [Header("Sound Effects")]
    public AudioClip shootSound;

    [Range(0f, 1f)]
    public float shootVolume = 0.5f;

    private const string MusicVolumeKey =
        "MonsterSurvival_MusicVolume";

    private const string SFXVolumeKey =
        "MonsterSurvival_SFXVolume";

    private void Awake()
    {
        // Chi su dung mot AudioManager trong Scene
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        // Doc am luong da luu
        float musicVolume = PlayerPrefs.GetFloat(
            MusicVolumeKey,
            0.6f
        );

        float sfxVolume = PlayerPrefs.GetFloat(
            SFXVolumeKey,
            0.8f
        );

        // Ap dung am luong sau Awake
        ApplyMusicVolume(musicVolume);
        ApplySFXVolume(sfxVolume);

        // Bat nhac nen neu da gan
        PlayBattleMusic();
    }

    public void PlayShoot()
    {
        if (sfxSource == null || shootSound == null)
            return;

        sfxSource.PlayOneShot(
            shootSound,
            shootVolume
        );
    }

    public void PlayBattleMusic()
    {
        if (musicSource == null || battleMusic == null)
            return;

        // Khong phat lai neu nhac da dang chay
        if (musicSource.isPlaying &&
            musicSource.clip == battleMusic)
        {
            return;
        }

        musicSource.clip = battleMusic;
        musicSource.loop = true;
        musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource != null)
        {
            musicSource.Stop();
        }
    }

    // Du kien gan voi thanh Slider Music
    public void SetMusicVolume(float value)
    {
        value = Mathf.Clamp01(value);

        ApplyMusicVolume(value);

        PlayerPrefs.SetFloat(
            MusicVolumeKey,
            value
        );

        PlayerPrefs.Save();
    }

    // Du kien gan voi thanh Slider SFX
    public void SetSFXVolume(float value)
    {
        value = Mathf.Clamp01(value);

        ApplySFXVolume(value);

        PlayerPrefs.SetFloat(
            SFXVolumeKey,
            value
        );

        PlayerPrefs.Save();
    }

    public float GetMusicVolume()
    {
        return PlayerPrefs.GetFloat(
            MusicVolumeKey,
            0.6f
        );
    }

    public float GetSFXVolume()
    {
        return PlayerPrefs.GetFloat(
            SFXVolumeKey,
            0.8f
        );
    }

    private void ApplyMusicVolume(float value)
    {
        if (audioMixer == null)
            return;

        float decibels = VolumeToDecibels(value);

        bool success = audioMixer.SetFloat(
            "MusicVolume",
            decibels
        );

        if (!success)
        {
            Debug.LogWarning(
                "Khong tim thay Mixer parameter: MusicVolume"
            );
        }
    }

    private void ApplySFXVolume(float value)
    {
        if (audioMixer == null)
            return;

        float decibels = VolumeToDecibels(value);

        bool success = audioMixer.SetFloat(
            "SFXVolume",
            decibels
        );

        if (!success)
        {
            Debug.LogWarning(
                "Khong tim thay Mixer parameter: SFXVolume"
            );
        }
    }

    private float VolumeToDecibels(float value)
    {
        if (value <= 0.0001f)
            return -80f;

        return Mathf.Log10(value) * 20f;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
