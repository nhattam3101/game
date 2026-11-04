
using UnityEngine;

public class SkillAudioEvents : MonoBehaviour
{
    [Header("References")]
    public SkillManager skillManager;

    [Header("Audio Clips")]
    public AudioClip levelUpSound;
    public AudioClip skillSelectSound;

    [Header("Volumes")]
    [Range(0f, 1f)]
    public float levelUpVolume = 0.7f;

    [Range(0f, 1f)]
    public float skillSelectVolume = 0.5f;

    private bool wasSkillPanelOpen = false;

    private void Awake()
    {
        if (skillManager == null)
        {
            skillManager =
                GetComponent<SkillManager>();
        }
    }

    private void Update()
    {
        if (skillManager == null ||
            skillManager.skillPanel == null)
        {
            return;
        }

        bool isOpen =
            skillManager.skillPanel.activeInHierarchy;

        // Bang ky nang vua duoc mo
        if (isOpen && !wasSkillPanelOpen)
        {
            PlaySFX(
                levelUpSound,
                levelUpVolume
            );
        }

        // Bang ky nang vua dong sau khi chon
        if (!isOpen && wasSkillPanelOpen)
        {
            PlaySFX(
                skillSelectSound,
                skillSelectVolume
            );
        }

        wasSkillPanelOpen = isOpen;
    }

    private void PlaySFX(
        AudioClip clip,
        float volume)
    {
        if (clip == null ||
            AudioManager.Instance == null)
        {
            return;
        }

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
