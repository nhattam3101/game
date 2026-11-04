using System;
using System.Collections.Generic;
using UnityEngine;

// Stage 4A - Maps are visual scene roots; NO scene reloads or wave modifications.
// Add this component to the WaveManager GameObject in the active scene.
[DisallowMultipleComponent]
public class WaveMapSwitcher : MonoBehaviour
{
    [Serializable]
    public class MapEntry
    {
        public string mapName = "New Map";
        [Min(1)] public int firstWave = 1;
        [Min(1)] public int lastWave = 2;
        public GameObject mapRoot;
        public Color cameraBackground = new Color(0.35f, 0.47f, 0.40f, 1f);
    }

    [Header("Scene References")]
    public WaveManager waveManager;
    public Camera targetCamera;

    [Header("Maps - First matching wave range is used")]
    public List<MapEntry> maps = new List<MapEntry>();

    [Header("Stage 4C - Optional player choice")]
    [Tooltip("When enabled, the selected biome remains active for all waves. Disabled by default to preserve 4A behavior.")]
    public bool useSelectedMapForAllWaves = false;
    [SerializeField] private int selectedMapIndex = -1;

    public int SelectedMapIndex { get { return selectedMapIndex; } }

    // Called by MapSelectionScreen4C before WaveManager starts.
    public bool SelectMapForMatch(int index)
    {
        if (index < 0 || index >= maps.Count || maps[index] == null || maps[index].mapRoot == null)
        {
            Debug.LogWarning("WaveMapSwitcher: Invalid map selection: " + index, this);
            return false;
        }
        selectedMapIndex = index;
        useSelectedMapForAllWaves = true;
        ActivateIndex(index);
        return true;
    }

    // Available if you want the original Meadow->Desert->Ruins wave progression again.
    public void ReturnToWaveRotation()
    {
        useSelectedMapForAllWaves = false;
        selectedMapIndex = -1;
        ApplyMapForWave(waveManager != null ? Mathf.Max(1, waveManager.CurrentWave) : 1);
    }

    [Header("Options")]
    public bool updateCameraBackground = true;
    public bool logMapChanges = true;
    [Tooltip("Editor play testing only. Press 1/2/3 to preview maps; turn OFF afterward.")]
    public bool enablePreviewKeys = false;

    private int lastObservedWave = -1;
    private int activeMapIndex = -1;
    private Color originalCameraBackground;
    private bool savedCameraColor;

    public int ActiveMapIndex { get { return activeMapIndex; } }

    private void Awake()
    {
        if (waveManager == null)
            waveManager = GetComponent<WaveManager>();
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera != null)
        {
            originalCameraBackground = targetCamera.backgroundColor;
            savedCameraColor = true;
        }

        // Prepare a visible map for the opening announcement, before CurrentWave becomes 1.
        ApplyMapForWave(1);
    }

    private void Update()
    {
        if (waveManager != null)
        {
            int wave = waveManager.CurrentWave;
            if (wave > 0 && wave != lastObservedWave)
            {
                lastObservedWave = wave;
                ApplyMapForWave(wave);
            }
        }

        // Works with legacy Input Manager or "Both". Safe in new-input-only projects.
#if ENABLE_LEGACY_INPUT_MANAGER
        if (!enablePreviewKeys || Time.timeScale == 0f)
            return;

        if (Input.GetKeyDown(KeyCode.Alpha1)) PreviewMap(0);
        else if (Input.GetKeyDown(KeyCode.Alpha2)) PreviewMap(1);
        else if (Input.GetKeyDown(KeyCode.Alpha3)) PreviewMap(2);
#endif
    }

    public void ApplyMapForWave(int wave)
    {
        if (useSelectedMapForAllWaves && selectedMapIndex >= 0 &&
            selectedMapIndex < maps.Count && maps[selectedMapIndex] != null &&
            maps[selectedMapIndex].mapRoot != null)
        {
            ActivateIndex(selectedMapIndex);
            return;
        }

        int index = -1;
        for (int i = 0; i < maps.Count; i++)
        {
            MapEntry map = maps[i];
            if (map != null && map.mapRoot != null &&
                wave >= map.firstWave && wave <= map.lastWave)
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            if (logMapChanges)
                Debug.LogWarning("WaveMapSwitcher: No map assigned for Wave " + wave + ". Keeping current map.");
            return;
        }

        ActivateIndex(index);
    }

    public void PreviewMap(int index)
    {
        if (index < 0 || index >= maps.Count || maps[index] == null || maps[index].mapRoot == null)
            return;
        ActivateIndex(index);
    }

    private void ActivateIndex(int index)
    {
        if (index == activeMapIndex && maps[index].mapRoot.activeSelf)
            return;

        GameObject nextRoot = maps[index].mapRoot;
        for (int i = 0; i < maps.Count; i++)
        {
            MapEntry entry = maps[i];
            if (entry == null || entry.mapRoot == null)
                continue;

            // Avoid deactivating the WaveManager/Player/Camera accidentally.
            Transform root = entry.mapRoot.transform;
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            bool containsCriticalObject =
                transform.IsChildOf(root) ||
                (targetCamera != null && targetCamera.transform.IsChildOf(root)) ||
                (player != null && player.transform.IsChildOf(root)) ||
                (waveManager != null && waveManager.enemySpawner != null &&
                 waveManager.enemySpawner.transform.IsChildOf(root));

            if (containsCriticalObject)
            {
                Debug.LogError("WaveMapSwitcher: A Map Root contains Player, Camera, WaveManager or EnemySpawner. Move those OUTSIDE Maps!");
                return;
            }
        }

        // Only change the visual roots. Player, enemies, spawners, canvas and boss stay alive.
        for (int i = 0; i < maps.Count; i++)
        {
            MapEntry entry = maps[i];
            if (entry != null && entry.mapRoot != null)
                entry.mapRoot.SetActive(entry.mapRoot == nextRoot);
        }

        if (targetCamera != null && updateCameraBackground)
            targetCamera.backgroundColor = maps[index].cameraBackground;

        activeMapIndex = index;
        if (logMapChanges)
            Debug.Log("[Multiple Maps] Active map: " + maps[index].mapName);
    }

    private void OnDestroy()
    {
        if (savedCameraColor && targetCamera != null && updateCameraBackground)
            targetCamera.backgroundColor = originalCameraBackground;
    }
}
