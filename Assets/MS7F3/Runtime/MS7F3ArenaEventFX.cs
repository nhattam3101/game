using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 7F.3 purely visual companion for an existing 7F.2 event.
/// Never edits the source LineRenderer, hazard geometry, Health, shield or damage.
/// Added at runtime by MS7F3ArenaVFXDirector, then destroyed with its event.
/// </summary>
[DisallowMultipleComponent]
public sealed class MS7F3ArenaEventFX : MonoBehaviour
{
    private MS7F2ArenaEvent source;
    private MS7F3ArenaVFXDirector director;
    private MS7F2ArenaEventDirector sourceDirector;
    private LineRenderer[] original;
    private LineRenderer[] halos;
    private LineRenderer[] chargeTraces;
    private Vector3[][] geometryPoints;
    private float[][] cumulativeDistances;
    private float[] perimeterLengths;
    private Material material;
    private float elapsedWarning;
    private float elapsedImpact;
    private bool impactObserved;
    private bool initialized;
    private bool stopped;

    public bool ImpactObserved { get { return impactObserved; } }
    public bool IsInitialized { get { return initialized; } }

    public static bool LooksLikeImpact(Color current, Color warning, Color impact)
    {
        float drw = current.r - warning.r;
        float dgw = current.g - warning.g;
        float dbw = current.b - warning.b;
        float dra = current.r - impact.r;
        float dga = current.g - impact.g;
        float dba = current.b - impact.b;
        float warningDistance = drw * drw + dgw * dgw + dbw * dbw;
        float impactDistance = dra * dra + dga * dga + dba * dba;
        // Require distinguishable tints; same-color settings use the timing fallback.
        return warningDistance > 0.0001f && impactDistance + 0.0001f < warningDistance;
    }

    public void Initialize(MS7F2ArenaEvent originalEvent, MS7F3ArenaVFXDirector owner)
    {
        if (initialized || originalEvent == null || owner == null) return;
        source = originalEvent;
        director = owner;
        sourceDirector = owner.arenaEvents;
        if (sourceDirector == null) return;

        // 7F.2 creates exactly two original outlines. Capture BEFORE adding any VFX lines.
        LineRenderer[] found = source.GetComponentsInChildren<LineRenderer>(true);
        if (found.Length < 2) return;
        original = new LineRenderer[] { found[0], found[1] };

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) return;
        material = new Material(shader);
        material.name = "MS7F3_EventFX_RuntimeOnly";
        halos = new LineRenderer[2];
        chargeTraces = new LineRenderer[2];
        geometryPoints = new Vector3[2][];
        cumulativeDistances = new float[2][];
        perimeterLengths = new float[2];
        for (int i = 0; i < 2; i++)
        {
            halos[i] = CreateVisualLine(original[i], "7F3_Halo_" + i, 19, 0.20f, true);
            CopyGeometry(original[i], halos[i]);
            CacheClosedGeometry(i);
            chargeTraces[i] = CreateVisualLine(original[i], "7F3_Charge_" + i, 21, 0.105f, false);
            chargeTraces[i].positionCount = 0;
        }
        initialized = true;
        director.RecordInitialized(source.Kind);
    }

    private LineRenderer CreateVisualLine(LineRenderer parentLine, string objectName,
        int sortingOrder, float width, bool isLoop)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(parentLine.transform, false);
        LineRenderer result = go.AddComponent<LineRenderer>();
        result.useWorldSpace = false;
        result.loop = isLoop;
        result.startWidth = width;
        result.endWidth = width;
        result.numCapVertices = 2;
        result.numCornerVertices = 2;
        result.shadowCastingMode = ShadowCastingMode.Off;
        result.receiveShadows = false;
        result.sortingLayerID = parentLine.sortingLayerID;
        result.sortingOrder = sortingOrder;
        result.sharedMaterial = material;
        return result;
    }

    private static void CopyGeometry(LineRenderer originalLine, LineRenderer copy)
    {
        int count = originalLine.positionCount;
        copy.positionCount = count;
        for (int i = 0; i < count; i++)
            copy.SetPosition(i, originalLine.GetPosition(i));
    }

    private void CacheClosedGeometry(int index)
    {
        int count = original[index].positionCount;
        geometryPoints[index] = new Vector3[count];
        original[index].GetPositions(geometryPoints[index]);
        cumulativeDistances[index] = new float[count + 1];
        for (int i = 0; i < count; i++)
            cumulativeDistances[index][i + 1] = cumulativeDistances[index][i] +
                Vector3.Distance(geometryPoints[index][i], geometryPoints[index][(i + 1) % count]);
        perimeterLengths[index] = cumulativeDistances[index][count];
    }

    private Vector3 SampleCached(int index, float fraction)
    {
        Vector3[] points = geometryPoints[index];
        if (points == null || points.Length == 0) return Vector3.zero;
        float total = perimeterLengths[index];
        if (total <= 0.00001f) return points[0];
        float distance = Mathf.Clamp01(fraction) * total;
        float[] cumulative = cumulativeDistances[index];
        // Binary search the cached cumulative arc lengths; no per-frame allocations.
        int low = 0, high = points.Length - 1;
        while (low < high)
        {
            int mid = (low + high) / 2;
            if (distance > cumulative[mid + 1]) low = mid + 1;
            else high = mid;
        }
        int edge = low;
        float length = cumulative[edge + 1] - cumulative[edge];
        float localT = length > 0.00001f ? (distance - cumulative[edge]) / length : 0f;
        return Vector3.Lerp(points[edge], points[(edge + 1) % points.Length], localT);
    }

    // Samples a closed polyline including the edge from its last vertex to its first.
    // Tracing exactly on the 7F.2 original line avoids misleading expanded hit areas.
    public static Vector3 SampleClosedPolyline(Vector3[] points, float normalized)
    {
        if (points == null || points.Length == 0) return Vector3.zero;
        if (points.Length == 1) return points[0];
        float total = 0f;
        for (int i = 0; i < points.Length; i++)
            total += Vector3.Distance(points[i], points[(i + 1) % points.Length]);
        if (total < 0.00001f) return points[0];
        float remaining = Mathf.Repeat(normalized, 1f) * total;
        for (int i = 0; i < points.Length; i++)
        {
            Vector3 start = points[i];
            Vector3 end = points[(i + 1) % points.Length];
            float length = Vector3.Distance(start, end);
            if (remaining <= length || i == points.Length - 1)
                return Vector3.Lerp(start, end, length > 0.00001f ? remaining / length : 0f);
            remaining -= length;
        }
        return points[0];
    }

    private void Update()
    {
        if (!initialized || stopped || director == null || sourceDirector == null) return;
        if (!director.enableArenaVFX || source == null || !sourceDirector.enableMultipleEvents)
        {
            Shutdown();
            return;
        }
        if (Time.timeScale <= 0f) return;

        Color warning = sourceDirector.warningTint;
        Color impact = sourceDirector.activeTint;
        float duration = Mathf.Max(0.2f, sourceDirector.warningSeconds);
        bool colorSwitched = LooksLikeImpact(original[0].startColor, warning, impact);
        bool sameTints = (new Vector3(warning.r - impact.r, warning.g - impact.g,
            warning.b - impact.b)).sqrMagnitude < 0.0001f;
        bool currentlyImpact = colorSwitched || (sameTints && elapsedWarning >= duration);
        if (currentlyImpact && !impactObserved)
        {
            impactObserved = true;
            elapsedImpact = 0f;
            director.RecordImpactVisual(source.Kind);
        }

        if (!impactObserved)
        {
            elapsedWarning += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedWarning / duration);
            float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * director.pulseFrequency);
            Color halo = warning;
            halo.a = Mathf.Clamp01(director.glowOpacity * (0.60f + 0.25f * pulse));
            Color charge = Color.Lerp(warning, Color.white, 0.4f);
            charge.a = Mathf.Clamp01(0.5f + 0.45f * progress);
            for (int i = 0; i < 2; i++)
            {
                ApplyColor(halos[i], halo);
                halos[i].startWidth = director.glowWidth * (0.85f + 0.12f * pulse);
                halos[i].endWidth = halos[i].startWidth;
                // Fill each outline progressively as warning time runs out.
                TraceOriginalEdge(i, progress, charge);
            }
        }
        else
        {
            elapsedImpact += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedImpact /
                Mathf.Max(0.1f, sourceDirector.impactVisibleSeconds));
            Color halo = Color.Lerp(Color.white, impact, Mathf.Clamp01(progress * 3f));
            halo.a = Mathf.Clamp01(director.glowOpacity * (1f - progress) * 1.3f);
            for (int i = 0; i < 2; i++)
            {
                ApplyColor(halos[i], halo);
                halos[i].startWidth = director.glowWidth * (1.25f - 0.3f * progress);
                halos[i].endWidth = halos[i].startWidth;
                chargeTraces[i].positionCount = 0;
            }
        }
    }

    private void TraceOriginalEdge(int i, float progress, Color color)
    {
        if (original == null || original[i] == null) return;
        LineRenderer dst = chargeTraces[i];
        // The shortest 7F.2 warning is 0.2s; keep a visible start marker.
        float fraction = Mathf.Clamp(progress, 0.015f, 0.999f);
        const int segments = 36;
        if (geometryPoints[i] == null || geometryPoints[i].Length < 2) return;
        dst.positionCount = segments + 1;
        for (int k = 0; k <= segments; k++)
            dst.SetPosition(k, SampleCached(i, fraction * k / segments));
        ApplyColor(dst, color);
    }

    private static void ApplyColor(LineRenderer line, Color c)
    {
        if (line == null) return;
        line.startColor = c;
        line.endColor = c;
    }

    public void Shutdown()
    {
        if (stopped) return;
        stopped = true;
        if (director != null && initialized) director.RecordRemoved();
        // Remove ONLY our own children; never the original event / source outlines.
        if (halos != null)
            for (int i = 0; i < halos.Length; i++)
                if (halos[i] != null) Destroy(halos[i].gameObject);
        if (chargeTraces != null)
            for (int i = 0; i < chargeTraces.Length; i++)
                if (chargeTraces[i] != null) Destroy(chargeTraces[i].gameObject);
        Destroy(this);
    }

    private void OnDestroy()
    {
        // Destroyed along with source event: report removal once.
        if (!stopped && director != null && initialized) director.RecordRemoved();
        if (material != null) Destroy(material);
    }
}
