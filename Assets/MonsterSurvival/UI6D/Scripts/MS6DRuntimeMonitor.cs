using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

// 6D — diagnostics only. Never changes visibility, pause, audio, skill or game state.
// Can be installed on the gameplay Canvas, or removed without affecting the game.
[DisallowMultipleComponent]
public sealed class MS6DRuntimeMonitor : MonoBehaviour
{
    [Header("Existing UI (optional; can auto-discover by name)")]
    public GameObject pausePanel;
    public GameObject skillPanel;
    public GameObject victoryPanel;
    public GameObject gameOverPanel;
    public GameObject settingsPanel;

    [Header("Read-only monitoring")]
    [Range(0.2f, 3f)] public float checkInterval = 0.5f;
    public bool reportWhenResolved = true;

    private float nextCheck;
    private string lastIssue = "";
    private readonly StringBuilder issues = new StringBuilder(220);

    private void Awake() { Discover(); }

    private void Discover()
    {
        if (!pausePanel) pausePanel = FindByName("PausePanel");
        if (!skillPanel) skillPanel = FindByName("SkillPanel");
        if (!victoryPanel) victoryPanel = FindByName("VictoryPanel");
        if (!gameOverPanel) gameOverPanel = FindByName("GameOverPanel");
        if (!settingsPanel) settingsPanel = FindByName("SettingsWindow", "SettingsPanel");
    }

    private GameObject FindByName(params string[] names)
    {
        GameObject inactiveMatch = null;
        var all = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (RectTransform t in all)
        {
            if (!t || t.gameObject.scene != gameObject.scene) continue;
            foreach (string n in names)
            {
                if (!string.Equals(t.name, n, System.StringComparison.OrdinalIgnoreCase)) continue;
                if (t.gameObject.activeInHierarchy) return t.gameObject;
                if (!inactiveMatch) inactiveMatch = t.gameObject;
            }
        }
        return inactiveMatch;
    }

    private static bool IsVisible(GameObject g)
    {
        if (!g || !g.activeInHierarchy) return false;
        for (Transform t = g.transform; t != null; t = t.parent)
        {
            CanvasGroup cg = t.GetComponent<CanvasGroup>();
            if (cg != null && cg.alpha < 0.05f) return false;
        }
        return true;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + checkInterval;
        // Settings may be dynamically spawned by the existing Pause UI.
        if (!settingsPanel || !settingsPanel.scene.IsValid() || !settingsPanel.activeInHierarchy)
            settingsPanel = FindByName("SettingsWindow", "SettingsPanel");

        bool pause = IsVisible(pausePanel);
        bool skill = IsVisible(skillPanel);
        bool win = IsVisible(victoryPanel);
        bool lose = IsVisible(gameOverPanel);
        bool settings = IsVisible(settingsPanel);
        issues.Length = 0;

        if (win && lose) issues.Append("Victory and GameOver visible together; ");
        if (skill && (win || lose)) issues.Append("Skill Selection overlaps result screen; ");
        if (pause && skill) issues.Append("Pause overlaps Skill Selection; ");
        if (pause && (win || lose)) issues.Append("Pause overlaps result screen; ");
        if (settings && (skill || win || lose)) issues.Append("Settings overlaps another modal; ");
        // Pause + Settings is allowed if PauseWindow's visual is suppressed by 6C.
        if (pause && settings)
        {
            GameObject window = FindByName("PauseWindow");
            if (IsVisible(window))
                issues.Append("PauseWindow visible behind Settings (check 6C coordinator); ");
        }

        string issue = issues.ToString();
        if (issue == lastIssue) return;
        lastIssue = issue;
        if (!string.IsNullOrEmpty(issue))
            Debug.LogWarning("[6D UI QA] " + issue + "Screen=" + Screen.width + "x" + Screen.height, this);
        else if (reportWhenResolved)
            Debug.Log("[6D UI QA] No conflicting active modals detected at " + Screen.width + "x" + Screen.height + ". This does not validate button functions.", this);
    }
}
