using UnityEngine;
using UnityEngine.UI;

// VISUAL-only guard. Never SetActive(), timeScale or change any game state.
// If Settings/Level-up/End screen opens over Pause, hide only the Pause window graphics.
[DisallowMultipleComponent]
public sealed class MS6CModalCoordinator : MonoBehaviour
{
    public GameObject pausePanel;
    public RectTransform pauseWindow;
    public GameObject skillPanel;
    public GameObject victoryPanel;
    public GameObject gameOverPanel;

    private CanvasGroup pauseWindowGroup;
    private Image pauseOverlay;
    private bool overlayRaycastOriginal;
    private bool suppressed;
    private float nextSettingsCheck;
    private bool cachedSettingsShown;
    private bool cachedNestedSettings;
    private void Awake()
    {
        if (pauseWindow)
        {
            pauseWindowGroup = pauseWindow.GetComponent<CanvasGroup>();
            if (!pauseWindowGroup) pauseWindowGroup = pauseWindow.gameObject.AddComponent<CanvasGroup>();
        }
        if (pausePanel) pauseOverlay = pausePanel.GetComponent<Image>();
        if (pauseOverlay) overlayRaycastOriginal = pauseOverlay.raycastTarget;
    }
    private void LateUpdate()
    {
        bool pauseShown = pausePanel && pausePanel.activeInHierarchy;
        bool skillShown = skillPanel && skillPanel.activeInHierarchy;
        bool resultShown = (victoryPanel && victoryPanel.activeInHierarchy) ||
                           (gameOverPanel && gameOverPanel.activeInHierarchy);
        bool settingsShown = false;
        bool nestedSettings = false;
        // Limit scan to UI hierarchy, and only if Pause is on screen.
        if (pauseShown && Time.unscaledTime >= nextSettingsCheck)
        {
            nextSettingsCheck = Time.unscaledTime + 0.12f;
            {
                var all = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                foreach (RectTransform rt in all)
                {
                    if (!rt || rt.gameObject.scene != gameObject.scene || !rt.gameObject.activeInHierarchy) continue;
                    string n = rt.name.ToLowerInvariant();
                    if (n == "settingswindow" || n == "settingspanel" || n == "settingswindow(clone)")
                    {
                        settingsShown = true;
                        nestedSettings = pauseWindow && rt.IsChildOf(pauseWindow);
                        break;
                    }
                }
            }
            cachedSettingsShown = settingsShown;
            cachedNestedSettings = nestedSettings;
        }
        else if (pauseShown)
        {
            settingsShown = cachedSettingsShown;
            nestedSettings = cachedNestedSettings;
        }
        // Never hide a parent containing the Settings page itself.
        bool hidePauseWindow = pauseShown && (skillShown || resultShown || (settingsShown && !nestedSettings));
        if (hidePauseWindow == suppressed) return;
        suppressed = hidePauseWindow;
        if (pauseWindowGroup)
        {
            pauseWindowGroup.alpha = suppressed ? 0f : 1f;
            pauseWindowGroup.interactable = !suppressed;
            pauseWindowGroup.blocksRaycasts = !suppressed;
        }
        if (pauseOverlay) pauseOverlay.raycastTarget = suppressed ? false : overlayRaycastOriginal;
    }
    private void OnDisable()
    {
        if (pauseWindowGroup) { pauseWindowGroup.alpha = 1f; pauseWindowGroup.interactable = true; pauseWindowGroup.blocksRaycasts = true; }
        if (pauseOverlay) pauseOverlay.raycastTarget = overlayRaycastOriginal;
    }
}
