using UnityEngine;
using UnityEngine.SceneManagement;

// Called BEFORE 7I.1's Awake() so it latches the correct mode for this run.
// Works alongside MMV3GameplayBootstrap; no WaveManager or Save modifications.
[DefaultExecutionOrder(-16000)]
[DisallowMultipleComponent]
public sealed class MS7I2GameplayModeBootstrap : MonoBehaviour
{
    public MS7I1EndlessDirector endlessDirector;
    public MS7I2GameMode appliedMode = MS7I2GameMode.Normal;
    public bool cameFromMainMenu;
    public bool applied;
    public string status = "NOT STARTED";

    private void Awake()
    {
        if (SceneManager.GetActiveScene().name != "SampleScene") return;
        // Consume even if dependencies missing, so a stale ENDLESS request cannot leak into a later run.
        appliedMode = MS7I2ModeSession.ConsumeForGameplay(out cameFromMainMenu);
        if (!endlessDirector) endlessDirector = GetComponent<MS7I1EndlessDirector>();
        if (!endlessDirector)
        {
            status = "BLOCKED: 7I.1 MS7I1EndlessDirector missing";
            Debug.LogError("[7I.2] " + status, this);
            return;
        }
        if (!endlessDirector.enabled)
        {
            status = "BLOCKED: 7I.1 component is disabled";
            Debug.LogError("[7I.2] " + status, this);
            return;
        }
        // 7I.1's Awake is default order 0; this Awake is -16000.
        endlessDirector.enableEndlessMode = appliedMode == MS7I2GameMode.Endless;
        applied = true;
        status = "APPLIED: " + appliedMode + (cameFromMainMenu ? " FROM MAINMENU" : " DIRECT SCENE => NORMAL");
        Debug.Log("[7I.2] " + status, this);
    }
}
