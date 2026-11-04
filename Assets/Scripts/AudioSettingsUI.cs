
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AudioSettingsUI : MonoBehaviour
{
    [Header("Music Settings")]
    public Slider musicSlider;
    public TMP_Text musicValueText;

    [Header("SFX Settings")]
    public Slider sfxSlider;
    public TMP_Text sfxValueText;

    private void Awake()
    {
        // Lang nghe thao tac keo Slider
        if (musicSlider != null)
        {
            musicSlider.onValueChanged.AddListener(
                OnMusicVolumeChanged
            );
        }

        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.AddListener(
                OnSFXVolumeChanged
            );
        }
    }

    private void OnEnable()
    {
        // Dong bo am luong moi khi mo Settings
        RefreshSettings();
    }

    public void RefreshSettings()
    {
        if (AudioManager.Instance == null)
        {
            return;
        }

        float musicVolume =
            AudioManager.Instance.GetMusicVolume();

        float sfxVolume =
            AudioManager.Instance.GetSFXVolume();

        // Cap nhat Slider ma khong goi su kien
        if (musicSlider != null)
        {
            musicSlider.SetValueWithoutNotify(
                musicVolume
            );
        }

        if (sfxSlider != null)
        {
            sfxSlider.SetValueWithoutNotify(
                sfxVolume
            );
        }

        UpdateMusicText(musicVolume);
        UpdateSFXText(sfxVolume);
    }

    private void OnMusicVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMusicVolume(
                value
            );
        }

        UpdateMusicText(value);
    }

    private void OnSFXVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetSFXVolume(
                value
            );
        }

        UpdateSFXText(value);
    }

    private void UpdateMusicText(float value)
    {
        if (musicValueText == null)
            return;

        int percent = Mathf.RoundToInt(
            Mathf.Clamp01(value) * 100f
        );

        musicValueText.text = percent + "%";
    }

    private void UpdateSFXText(float value)
    {
        if (sfxValueText == null)
            return;

        int percent = Mathf.RoundToInt(
            Mathf.Clamp01(value) * 100f
        );

        sfxValueText.text = percent + "%";
    }

    private void OnDestroy()
    {
        if (musicSlider != null)
        {
            musicSlider.onValueChanged.RemoveListener(
                OnMusicVolumeChanged
            );
        }

        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.RemoveListener(
                OnSFXVolumeChanged
            );
        }
    }
}
