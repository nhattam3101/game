using UnityEngine;
using UnityEngine.SceneManagement;

// Defense-in-depth: verifies the actual selected map in SampleScene. If some
// external launcher bypasses MainMenu START, this still blocks locked maps.
[DisallowMultipleComponent]
[DefaultExecutionOrder(-14000)]
public sealed class MS5C3GameplayGate : MonoBehaviour
{
    public WaveMapSwitcher mapSwitcher;
    public WaveManager waveManager;
    public EnemySpawner enemySpawner;
    public string mainMenuSceneName = "MainMenu";
    private int capturedSlot;
    private bool inspected;
    private float startedAt;

    private void Awake()
    {
        capturedSlot = MS5A2GoldBank.SelectedSlot;
        startedAt = Time.unscaledTime;
        if (!waveManager) waveManager = FindFirstObjectByType<WaveManager>();
        if (!mapSwitcher && waveManager) mapSwitcher = waveManager.GetComponent<WaveMapSwitcher>();
        if (!enemySpawner && waveManager) enemySpawner = waveManager.enemySpawner;
    }

    private void LateUpdate()
    {
        if (inspected || !mapSwitcher) return;
        int selectedIndex = mapSwitcher.SelectedMapIndex;
        if (selectedIndex < 0)
        {
            // Direct SampleScene tests with no selected map remain possible.
            // The normal V3 bootstrap will select a map shortly after scene load.
            if (Time.unscaledTime - startedAt > 8f)
            { inspected = true; Debug.LogWarning("[5C.3] No selected map after 8s; verify MMV3GameplayBootstrap.", this); }
            return;
        }
        inspected = true;
        MS5C3MapPolicy.State state;
        bool valid = MS5C3MapPolicy.TryGet(capturedSlot, selectedIndex, out state);
        if (valid && state.allowed)
        {
            Debug.Log("[5C.3] Gameplay map verified: index=" + selectedIndex + ", profile=" + capturedSlot + ", mode=" + state.status, this);
            return;
        }
        // Stop gameplay before routing back; never silently substitute a map.
        if (waveManager) waveManager.enabled = false;
        if (enemySpawner) enemySpawner.enabled = false;
        Time.timeScale = 0f;
        Debug.LogError("[5C.3] MAP ACCESS DENIED: profile=" + capturedSlot + ", map=" + selectedIndex + ", reason=" + state.status, this);
        if (Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(mainMenuSceneName);
        }
        else Debug.LogError("[5C.3] MainMenu not in Build Profiles; gameplay remains paused.", this);
    }
}
