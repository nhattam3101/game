using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

// 7F.4.1 - reads the actual map shown by the existing WaveMapSwitcher.
// Does NOT read/change Save/Profile, menu scripts, WaveManager or hazard damage.
// Runs BEFORE MS7F4MapBalanceDirector.Update (-400) so the ID is ready for the same frame.
[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public sealed class MS7F41AutoMapIdBinder : MonoBehaviour
{
    [Header("Existing components on WaveManager")]
    public WaveMapSwitcher mapSwitcher;
    public MS7F4MapBalanceDirector mapBalance;

    [Header("Automatic map identity")]
    public bool enableAutoBinding = true;
    public bool logChanges = true;

    [HideInInspector] public string previousOverrideBeforeInstall = "";

    public int ResolvedIndex { get; private set; } = -1;
    public string ResolvedMapId { get; private set; } = "";
    public int IdChanges { get; private set; }
    public int NoActiveMapFrames { get; private set; }
    public bool ProfileMatched { get; private set; }
    public string Status { get; private set; } = "Awaiting Play Mode";

    private string runtimeOriginalOverride = "";
    private bool capturedOriginal;
    private bool wroteOverride;
    private readonly List<string> recent = new List<string>();

    private void Awake()
    {
        ResolveReferences();
        CaptureOriginal();
    }

    private void ResolveReferences()
    {
        if (mapSwitcher == null) mapSwitcher = GetComponent<WaveMapSwitcher>();
        if (mapBalance == null) mapBalance = GetComponent<MS7F4MapBalanceDirector>();
    }

    private void CaptureOriginal()
    {
        if (capturedOriginal || mapBalance == null) return;
        runtimeOriginalOverride = mapBalance.mapIdOverride;
        capturedOriginal = true;
    }

    public static bool IsIndexValid(WaveMapSwitcher switcher, int index)
    {
        return switcher != null && switcher.maps != null && index >= 0 &&
            index < switcher.maps.Count && switcher.maps[index] != null &&
            switcher.maps[index].mapRoot != null &&
            !string.IsNullOrWhiteSpace(switcher.maps[index].mapName);
    }

    public static string GetIdForIndex(WaveMapSwitcher switcher, int index)
    {
        return IsIndexValid(switcher, index) ? switcher.maps[index].mapName.Trim() : "";
    }

    public static bool HasEnabledProfile(MS7F4MapBalanceDirector balance, string id)
    {
        if (balance == null || balance.mapProfiles == null) return false;
        foreach (MS7F4MapBalanceDirector.MapProfile p in balance.mapProfiles)
            if (p != null && p.enabled && p.waves != null && p.waves.Count > 0 &&
                MS7F4MapBalanceDirector.SameMap(p.mapId, id))
                return true;
        return false;
    }

    // ActiveMapIndex is authoritative: it reflects the map ROOT that WaveMapSwitcher
    // has actually activated, not a stale menu choice or an assumed scene name.
    public bool TryGetActualMap(out int index, out string mapId)
    {
        index = -1;
        mapId = "";
        if (mapSwitcher == null) return false;
        int active = mapSwitcher.ActiveMapIndex;
        if (IsIndexValid(mapSwitcher, active) && mapSwitcher.maps[active].mapRoot.activeSelf)
        {
            index = active;
            mapId = GetIdForIndex(mapSwitcher, active);
            return true;
        }
        // The map selection can be applied just before the roots are activated.
        // Only trust SelectedMapIndex when the selected-map gameplay mode is enabled.
        int selected = mapSwitcher.SelectedMapIndex;
        if (mapSwitcher.useSelectedMapForAllWaves && IsIndexValid(mapSwitcher, selected) &&
            mapSwitcher.maps[selected].mapRoot.activeSelf)
        {
            index = selected;
            mapId = GetIdForIndex(mapSwitcher, selected);
            return true;
        }
        return false;
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        ResolveReferences();
        CaptureOriginal();
        if (mapSwitcher == null || mapBalance == null)
        {
            Status = "MISSING WaveMapSwitcher or MS7F4MapBalanceDirector";
            ReleaseIfOwned();
            return;
        }
        if (!enableAutoBinding)
        {
            Status = "Auto binding disabled";
            ReleaseIfOwned();
            return;
        }

        int index;
        string id;
        if (!TryGetActualMap(out index, out id))
        {
            NoActiveMapFrames++;
            Status = "No confirmed active Map Root; fallback to Scene/manual ID";
            ResolvedMapId = "";
            ResolvedIndex = -1;
            ProfileMatched = false;
            ReleaseIfOwned();
            return;
        }

        ProfileMatched = HasEnabledProfile(mapBalance, id);
        Status = ProfileMatched ? "ACTIVE MAP + MATCHING PROFILE" : "ACTIVE MAP, NO MATCHING 7F.4 PROFILE";
        if (ResolvedIndex != index || !string.Equals(ResolvedMapId, id, StringComparison.Ordinal))
        {
            ResolvedIndex = index;
            ResolvedMapId = id;
            IdChanges++;
            Record("ACTIVE MAP index=" + index + " id='" + id + "' profile=" + ProfileMatched);
        }
        if (!string.Equals(mapBalance.mapIdOverride, id, StringComparison.Ordinal))
            mapBalance.mapIdOverride = id;
        wroteOverride = true;
    }

    private void ReleaseIfOwned()
    {
        if (!wroteOverride || mapBalance == null) return;
        mapBalance.mapIdOverride = capturedOriginal ? runtimeOriginalOverride : "";
        wroteOverride = false;
        Record("RELEASE auto ID; original override restored");
    }

    private void OnDisable()
    {
        if (Application.isPlaying) ReleaseIfOwned();
    }

    private void OnDestroy()
    {
        if (Application.isPlaying) ReleaseIfOwned();
    }

    private void Record(string msg)
    {
        string line = Time.time.ToString("F2") + "s | " + msg;
        if (logChanges) Debug.Log("[7F.4.1] " + msg, this);
        recent.Add(line);
        if (recent.Count > 60) recent.RemoveAt(0);
    }

    public string GetReport()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("7F.4.1 AUTO MAP ID - READ ONLY RUNTIME REPORT");
        sb.AppendLine("Playing=" + Application.isPlaying + " Scene=" + SceneManager.GetActiveScene().name);
        sb.AppendLine("Enabled=" + enableAutoBinding + " ActiveIndex=" +
            (mapSwitcher == null ? -1 : mapSwitcher.ActiveMapIndex) +
            " SelectedIndex=" + (mapSwitcher == null ? -1 : mapSwitcher.SelectedMapIndex));
        sb.AppendLine("ResolvedIndex=" + ResolvedIndex + " ResolvedMapId='" + ResolvedMapId +
            "' ProfileMatched=" + ProfileMatched + " Status=" + Status);
        sb.AppendLine("7F4EffectiveMapId='" + (mapBalance == null ? "MISSING" : mapBalance.ActiveMapId) +
            "' 7F4OptIn=" + (mapBalance != null && mapBalance.enableMapBalancing));
        sb.AppendLine("IdChanges=" + IdChanges + " UnresolvedFrames=" + NoActiveMapFrames);
        sb.AppendLine("--- MAP ID TRACE ---");
        foreach (string t in recent) sb.AppendLine(t);
        sb.AppendLine("NOTE: same-scene maps use WaveMapSwitcher.maps[index].mapName; Scene name is fallback only.");
        return sb.ToString();
    }
}
