using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// V3.3 - optional, quiet UI sounds. Does not modify Button.onClick.
// Uses the EXISTING AudioManager SFX source / mixer settings.
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class MMV33ButtonSounds : MonoBehaviour,
    IPointerEnterHandler, IPointerClickHandler, ISelectHandler
{
    public AudioClip hoverClip;
    public AudioClip clickClip;
    [Range(0f, 1f)] public float hoverVolume = 0.20f;
    [Range(0f, 1f)] public float clickVolume = 0.26f;

    private Button button;
    private float lastHoverTime = -10f;

    private void Awake() { button = GetComponent<Button>(); }

    public void OnPointerEnter(PointerEventData eventData) { PlayHover(); }
    public void OnSelect(BaseEventData eventData) { PlayHover(); }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (button != null && button.IsActive() && button.IsInteractable())
            Play(clickClip, clickVolume);
    }

    private void PlayHover()
    {
        if (button == null || !button.IsActive() || !button.IsInteractable()) return;
        if (Time.unscaledTime - lastHoverTime < 0.14f) return;
        lastHoverTime = Time.unscaledTime;
        Play(hoverClip, hoverVolume);
    }

    private static void Play(AudioClip clip, float volume)
    {
        if (clip == null || volume <= 0f) return;
        AudioManager a = AudioManager.Instance;
        if (a == null || a.sfxSource == null || !a.sfxSource.isActiveAndEnabled) return;
        a.sfxSource.PlayOneShot(clip, volume);
    }
}
