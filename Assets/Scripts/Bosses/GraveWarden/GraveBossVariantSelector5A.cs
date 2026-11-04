using UnityEngine;

// MonsterSurvival 5A: Keeps the existing WaveManager, Victory, boss HP UI
// and map-selection screen unchanged. Chooses the Wave 5 boss by selected map.
[DisallowMultipleComponent]
public class GraveBossVariantSelector5A : MonoBehaviour
{
    [Header("Scene components (WaveManager GameObject)")]
    public WaveManager waveManager;
    public WaveMapSwitcher mapSwitcher;

    [Header("Boss prefabs (from Project)")]
    [Tooltip("Optional: leave empty to reuse the original WaveManager Boss Prefab.")]
    public GameObject shadowTitanPrefab;
    public GameObject graveWardenPrefab;

    [Header("Map selection")]
    [Tooltip("0 = Meadow; 1 = Desert; 2 = Ancient Ruins")]
    [Range(0, 2)] public int graveWardenMapIndex = 2;
    public string graveWardenBossName = "THE GRAVE WARDEN";
    public string shadowTitanBossName = "SHADOW TITAN";
    public bool logSelection = true;

    private GameObject originalBoss;
    private string originalBossName;
    private int appliedMapIndex = -1;

    private void Awake()
    {
        if (waveManager == null) waveManager = GetComponent<WaveManager>();
        if (mapSwitcher == null) mapSwitcher = GetComponent<WaveMapSwitcher>();
        if (waveManager != null)
        {
            originalBoss = waveManager.bossPrefab;
            originalBossName = waveManager.bossName;
        }
    }

    private void Update()
    {
        if (waveManager == null || mapSwitcher == null) return;
        int index = mapSwitcher.SelectedMapIndex;
        if (index < 0 || index == appliedMapIndex) return;

        // Never swap the boss after the boss wave has begun.
        if (waveManager.CurrentWave >= waveManager.bossWave) return;

        ApplyMapBoss(index);
    }

    public void ApplyMapBoss(int index)
    {
        if (waveManager == null || index < 0 || index > 2) return;
        if (waveManager.CurrentWave >= waveManager.bossWave) return;

        if (index == graveWardenMapIndex)
        {
            if (graveWardenPrefab == null || graveWardenPrefab.GetComponent<Health>() == null)
            {
                Debug.LogError("[Boss 5A] Grave Warden Prefab is missing or has no Health. Keeping original Boss!", this);
                return;
            }
            waveManager.bossPrefab = graveWardenPrefab;
            waveManager.bossName = graveWardenBossName;
        }
        else
        {
            GameObject prefab = shadowTitanPrefab != null ? shadowTitanPrefab : originalBoss;
            if (prefab == null || prefab.GetComponent<Health>() == null)
            {
                Debug.LogError("[Boss 5A] Shadow Titan Prefab is missing or has no Health.", this);
                return;
            }
            waveManager.bossPrefab = prefab;
            waveManager.bossName = !string.IsNullOrEmpty(shadowTitanBossName)
                ? shadowTitanBossName : originalBossName;
        }

        appliedMapIndex = index;
        if (logSelection)
            Debug.Log("[Boss 5A] Map " + index + " -> Wave " + waveManager.bossWave +
                      " Boss: " + waveManager.bossName, this);
    }
}
