using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Skins only the existing 7J.1 PLAYER HUB launcher; no new listeners or navigation.
[DisallowMultipleComponent]
public sealed class MS7J2MenuEntryStyle : MonoBehaviour
{
    public MMV3MenuController menu;
    private IEnumerator Start()
    {
        if (gameObject.scene.name != "MainMenu") yield break;
        if (!menu) menu = GetComponent<MMV3MenuController>();
        if (!menu || !menu.homePage) yield break;
        // 7J.1 creates its button after MainMenu Start.
        yield return null;
        yield return null;
        Button entry = null;
        foreach (Button button in menu.homePage.GetComponentsInChildren<Button>(true))
            if (button.name == "MS7J1_BTN_PlayerHub") { entry = button; break; }
        if (!entry) { Debug.LogWarning("[7J.2] PLAYER HUB launcher not found; no MainMenu skin applied."); yield break; }
        Image bg = entry.GetComponent<Image>();
        if (bg) bg.color = MS7J2UI.Card;
        var legacy = entry.GetComponentInChildren<Text>(true);
        if (legacy) legacy.enabled = false; // Keep original 7J.1 control and click handler.
        var previous = entry.transform.Find("MS7J2_StyledLabel");
        if (previous) yield break;
        TMP_FontAsset font = null;
        if (menu.selectedMapCaption) font = menu.selectedMapCaption.font;
        if (!font && menu.footerMessage) font = menu.footerMessage.font;
        if (!font)
        {
            var known = menu.GetComponentInChildren<TMP_Text>(true);
            if (known) font = known.font;
        }
        var rect = entry.GetComponent<RectTransform>();
        if (rect) rect.sizeDelta = new Vector2(230f, 56f);
        MS7J2UI.Rule("MS7J2_LauncherAccent", entry.transform, MS7J2UI.Gold, 0f, 0f, .015f, 1f);
        MS7J2UI.Label("MS7J2_StyledLabel", entry.transform, "PLAYER HUB", 22, MS7J2UI.Pale,
            TextAlignmentOptions.Center, font, .05f, .07f, .95f, .93f, true);
    }
}
