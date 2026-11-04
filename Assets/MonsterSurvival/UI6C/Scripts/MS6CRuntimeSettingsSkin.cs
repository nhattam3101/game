using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// Optional runtime skin for SettingsWindow created dynamically by pause system.
// Only changes graphics, NEVER SetActive, listeners, timeScale or saved audio.
[DisallowMultipleComponent]
public sealed class MS6CRuntimeSettingsSkin : MonoBehaviour
{
    public Sprite panelSprite;
    public Sprite buttonSprite;
    public Color panelTint = new Color(0.95f, 0.92f, 1f, 1f);
    private float nextScan;
    private readonly HashSet<RectTransform> processed = new HashSet<RectTransform>();
    private void Update()
    {
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + 0.8f;
        // Include inactive objects: an old Settings prefab may first be hidden.
        var all = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var rt in all)
        {
            if (!rt || !rt.gameObject.scene.IsValid() || rt.gameObject.scene != gameObject.scene) continue;
            string n = rt.name.ToLowerInvariant();
            if (n != "settingswindow" && n != "settingspanel" && n != "settingswindow(clone)") continue;
            if (processed.Contains(rt)) continue;
            processed.Add(rt);
            var im = rt.GetComponent<Image>();
            if (im && panelSprite) { im.sprite = panelSprite; im.type = Image.Type.Sliced; im.color = panelTint; }
            var buttons = rt.GetComponentsInChildren<Button>(true);
            foreach (var b in buttons)
            {
                var bg = b.GetComponent<Image>();
                if (bg && buttonSprite) { bg.sprite = buttonSprite; bg.type = Image.Type.Sliced; bg.color = Color.white; }
                if (!b.GetComponent<MS6CButtonMotion>())
                {
                    b.transition = Selectable.Transition.None;
                    b.gameObject.AddComponent<MS6CButtonMotion>();
                }
            }
            // Recolor visual parts of existing Music/SFX sliders; never modify their values/events.
            foreach (var slider in rt.GetComponentsInChildren<Slider>(true))
            {
                foreach (var img in slider.GetComponentsInChildren<Image>(true))
                {
                    string element = img.name.ToLowerInvariant();
                    if (element.Contains("fill")) img.color = new Color(.61f, .39f, .89f, 1f);
                    else if (element.Contains("handle")) img.color = new Color(.96f, .74f, .48f, 1f);
                    else if (element.Contains("background")) img.color = new Color(.15f, .12f, .24f, 1f);
                }
            }
            foreach (var label in rt.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label) label.color = new Color(.91f, .87f, .98f, 1f);
            }
            Debug.Log("[6C] Styled runtime Settings UI: " + rt.name);
        }
    }
}
