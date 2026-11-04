using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 7F.3 read-only visual enhancer. Runs next to existing 7F.2 director on WaveManager.
/// Never creates hazard events and never changes the Health, damage or shield system.
/// </summary>
[DisallowMultipleComponent]
public sealed class MS7F3ArenaVFXDirector : MonoBehaviour
{
    [Header("Existing 7F.2 system")]
    public MS7F2ArenaEventDirector arenaEvents;

    [Header("Opt-in visual enhancements (OFF after install)")]
    public bool enableArenaVFX = false;
    [Range(0.10f, 0.65f)] public float glowWidth = 0.22f;
    [Range(0.05f, 0.90f)] public float glowOpacity = 0.40f;
    [Range(1f, 20f)] public float pulseFrequency = 9f;
    [Range(0.02f, 0.25f)] public float scanInterval = 0.06f;

    [Header("Optional diagnostics")]
    public bool logToConsole = false;

    public int VFXInitialized { get; private set; }
    public int TwinVisuals { get; private set; }
    public int CrossfireVisuals { get; private set; }
    public int RingVisuals { get; private set; }
    public int ImpactVisuals { get; private set; }
    public int VisualsRemoved { get; private set; }
    public int ActiveVisuals { get; private set; }
    public bool PauseObserved { get; private set; }

    private float scanTimer;
    private readonly List<string> trace = new List<string>();
    private readonly List<MS7F3ArenaEventFX> attached = new List<MS7F3ArenaEventFX>();
    private bool wasEnabled;

    private void Awake() { if (arenaEvents == null) arenaEvents = GetComponent<MS7F2ArenaEventDirector>(); }

    private void Update()
    {
        if (!Application.isPlaying) return;
        if (arenaEvents == null) arenaEvents = GetComponent<MS7F2ArenaEventDirector>();
        if (!enableArenaVFX || arenaEvents == null || !arenaEvents.enableMultipleEvents)
        {
            if (wasEnabled) ClearAttached();
            wasEnabled = false;
            return;
        }
        wasEnabled = true;
        if (Time.timeScale <= 0f) { PauseObserved = true; return; }
        scanTimer -= Time.deltaTime;
        if (scanTimer > 0f) return;
        scanTimer = Mathf.Clamp(scanInterval, 0.02f, 0.25f);

        // On ordinary arena scenes only one 7F.2 event exists at a time.
        MS7F2ArenaEvent[] events = FindObjectsByType<MS7F2ArenaEvent>(FindObjectsSortMode.None);
        for (int i = 0; i < events.Length; i++)
        {
            MS7F2ArenaEvent e = events[i];
            if (e == null || e.GetComponent<MS7F3ArenaEventFX>() != null) continue;
            MS7F3ArenaEventFX fx = e.gameObject.AddComponent<MS7F3ArenaEventFX>();
            fx.Initialize(e, this);
            if (fx.IsInitialized) attached.Add(fx);
            else Destroy(fx);
        }
        attached.RemoveAll(x => x == null);
    }

    private void ClearAttached()
    {
        for (int i = 0; i < attached.Count; i++)
            if (attached[i] != null) attached[i].Shutdown();
        attached.Clear();
    }

    public void RecordInitialized(MS7F2ArenaEvent.EventKind kind)
    {
        VFXInitialized++;
        ActiveVisuals++;
        if (kind == MS7F2ArenaEvent.EventKind.TwinStrike) TwinVisuals++;
        else if (kind == MS7F2ArenaEvent.EventKind.Crossfire) CrossfireVisuals++;
        else RingVisuals++;
        Log("READY " + kind + " (original hit geometry unchanged)");
    }
    public void RecordImpactVisual(MS7F2ArenaEvent.EventKind kind)
    {
        ImpactVisuals++;
        Log("IMPACT_VFX " + kind + " (visual only)");
    }
    public void RecordRemoved()
    {
        VisualsRemoved++;
        ActiveVisuals = Mathf.Max(0, ActiveVisuals - 1);
    }
    private void Log(string message)
    {
        string entry = Time.time.ToString("F2") + "s | " + message;
        trace.Add(entry);
        if (trace.Count > 100) trace.RemoveAt(0);
        if (logToConsole) Debug.Log("[7F.3] " + message, this);
    }

    public string GetRuntimeReport()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("7F.3 ENVIRONMENTAL VFX - PLAY MODE (READ ONLY)");
        sb.AppendLine("Playing=" + Application.isPlaying + " EnableVFX=" + enableArenaVFX +
            " Source7F2Enabled=" + (arenaEvents != null && arenaEvents.enableMultipleEvents));
        sb.AppendLine("VFXInitialized=" + VFXInitialized + " Twin=" + TwinVisuals +
            " Crossfire=" + CrossfireVisuals + " HollowRing=" + RingVisuals +
            " ImpactVisuals=" + ImpactVisuals + " Active=" + ActiveVisuals +
            " Removed=" + VisualsRemoved + " PauseSeen=" + PauseObserved);
        sb.AppendLine("NOTE: visual counters do not prove on-screen appearance, readable colors or damage safety.");
        foreach (string item in trace) sb.AppendLine(item);
        return sb.ToString();
    }

    private void OnDisable() { if (Application.isPlaying) ClearAttached(); }
    private void OnDestroy() { if (Application.isPlaying) ClearAttached(); }
    private void OnValidate()
    {
        glowWidth = Mathf.Clamp(glowWidth, 0.10f, 0.65f);
        glowOpacity = Mathf.Clamp(glowOpacity, 0.05f, 0.90f);
        pulseFrequency = Mathf.Clamp(pulseFrequency, 1f, 20f);
        scanInterval = Mathf.Clamp(scanInterval, 0.02f, 0.25f);
    }
}
