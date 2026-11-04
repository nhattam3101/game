using System.Collections;
using UnityEngine;

// IMPORTANT: must be on an active object in SampleScene, with old MapSelectionScreen4C disabled.
// Runs before WaveManager.Awake/Start; allows map to be applied before waves begin.
[DefaultExecutionOrder(-15000)]
[DisallowMultipleComponent]
public sealed class MMV3GameplayBootstrap : MonoBehaviour
{
    public WaveManager waveManager;
    public EnemySpawner enemySpawner;
    public WaveMapSwitcher mapSwitcher;
    public MapBiomeRules4C biomeRules;

    [Range(0, 2)] public int directSceneTestMapIndex = 0;

    private void Awake()
    {
        if (!waveManager) waveManager = FindAnyObjectByType<WaveManager>();
        if (!enemySpawner && waveManager) enemySpawner = waveManager.enemySpawner;
        if (!mapSwitcher && waveManager) mapSwitcher = waveManager.GetComponent<WaveMapSwitcher>();
        if (!biomeRules && waveManager) biomeRules = waveManager.GetComponent<MapBiomeRules4C>();

        // Freeze before any WaveManager.Start can start RunWaves.
        if (waveManager) waveManager.enabled = false;
        if (enemySpawner) enemySpawner.enabled = false;
        Time.timeScale = 0f;
    }

    private IEnumerator Start()
    {
        // Let other components finish their Start() before setting biome rules.
        // WaveManager remains disabled, and the persistent curtain stays visible.
        yield return null;
        if (!waveManager || !enemySpawner || !mapSwitcher || !biomeRules ||
            mapSwitcher.maps == null || mapSwitcher.maps.Count < 3)
        {
            Debug.LogError("[Menu V3] Missing WaveManager / EnemySpawner / WaveMapSwitcher / MapBiomeRules4C. Waves remain paused.", this);
            MMV3TransitionCurtain.NotifyGameplayReady();
            yield break;
        }

        int index = MMV3MapSession.ConsumeOrDefault(directSceneTestMapIndex);
        if (index < 0 || index >= mapSwitcher.maps.Count || mapSwitcher.maps[index] == null ||
            mapSwitcher.maps[index].mapRoot == null)
        {
            Debug.LogError("[Menu V3] Selected map index has no Map Root: " + index, this);
            MMV3TransitionCurtain.NotifyGameplayReady();
            yield break;
        }

        if (!mapSwitcher.SelectMapForMatch(index))
        {
            Debug.LogError("[Menu V3] WaveMapSwitcher declined map: " + index, this);
            MMV3TransitionCurtain.NotifyGameplayReady();
            yield break;
        }

        biomeRules.StartSelectedMap(index);
        enemySpawner.enabled = false; // WaveManager controls it during each wave.
        waveManager.enabled = true;
        Time.timeScale = 1f;
        Debug.Log("[Menu V3] Map " + index + " applied. WaveManager ready.", this);
        MMV3TransitionCurtain.NotifyGameplayReady();
    }
}
