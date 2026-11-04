using UnityEngine;
using UnityEngine.Tilemaps;

// CHI LA BO KIEM TRA DIEM SPAWN. CHUA TU DONG THAY CAC SPAWNER HIEN CO.
// Gan vao WaveManager (Scene). Sau do, cac spawner goi TryFindViewportPoint().
[DisallowMultipleComponent]
public class MapSafeSpawn2D : MonoBehaviour
{
    [Header("Reference")]
    public WaveMapSwitcher mapSwitcher;

    [Header("Default validation")]
    public LayerMask obstacleLayers;
    [Min(0.05f)] public float defaultClearanceRadius = 0.5f;
    [Min(0f)] public float defaultMinDistanceFromPlayer = 3.5f;
    [Range(0f, 0.4f)] public float defaultViewportMargin = 0.1f;
    [Range(1, 100)] public int defaultAttempts = 30;
    public bool requireGroundTile = true;

    private void Awake()
    {
        if (mapSwitcher == null) mapSwitcher = GetComponent<WaveMapSwitcher>();
    }

    private Tilemap GetCurrentGround()
    {
        if (mapSwitcher == null) return null;
        int index = mapSwitcher.ActiveMapIndex;
        if (index < 0 || index >= mapSwitcher.maps.Count) return null;
        WaveMapSwitcher.MapEntry entry = mapSwitcher.maps[index];
        if (entry == null || entry.mapRoot == null || !entry.mapRoot.activeInHierarchy)
            return null;
        Transform grid = entry.mapRoot.transform.Find("MapGrid_4B2");
        if (grid == null) return null;
        Transform ground = grid.Find("Ground_Tilemap");
        return ground != null ? ground.GetComponent<Tilemap>() : null;
    }

    // Kiem tra 5 diem (tam va 4 canh) tranh spawn sat mep Tilemap.
    public bool IsSafe(Vector2 position, Transform player,
        float minPlayerDistance, float clearanceRadius, LayerMask blockedLayers)
    {
        if (player != null &&
            Vector2.Distance(position, player.position) < Mathf.Max(0f, minPlayerDistance))
            return false;

        float radius = Mathf.Max(0.05f, clearanceRadius);
        if (blockedLayers.value != 0 &&
            Physics2D.OverlapCircle(position, radius, blockedLayers) != null)
            return false;

        if (requireGroundTile)
        {
            Tilemap ground = GetCurrentGround();
            if (ground == null) return false;
            if (!HasGround(ground, position) ||
                !HasGround(ground, position + Vector2.right * radius) ||
                !HasGround(ground, position + Vector2.left * radius) ||
                !HasGround(ground, position + Vector2.up * radius) ||
                !HasGround(ground, position + Vector2.down * radius))
                return false;
        }
        return true;
    }

    private static bool HasGround(Tilemap ground, Vector2 position)
    {
        Vector3Int cell = ground.WorldToCell(position);
        return ground.HasTile(cell);
    }

    // Dung cho Normal, Healer, Summoner, Boss (sau khi noi code).
    // Ham chi tinh khi co yeu cau spawn, KHONG chay Update moi frame.
    public bool TryFindViewportPoint(Camera camera, Transform player, out Vector3 result,
        float viewportMargin = -1f, float minPlayerDistance = -1f,
        float clearanceRadius = -1f, int attempts = -1,
        LayerMask? overrideBlockedLayers = null)
    {
        result = Vector3.zero;
        if (camera == null || !camera.orthographic) return false;
        float margin = viewportMargin >= 0f ? viewportMargin : defaultViewportMargin;
        float minDistance = minPlayerDistance >= 0f ? minPlayerDistance : defaultMinDistanceFromPlayer;
        float radius = clearanceRadius >= 0f ? clearanceRadius : defaultClearanceRadius;
        int count = attempts > 0 ? attempts : defaultAttempts;
        LayerMask layers = overrideBlockedLayers.HasValue ? overrideBlockedLayers.Value : obstacleLayers;
        margin = Mathf.Clamp(margin, 0f, 0.45f);
        float depth = Mathf.Abs(camera.transform.position.z); // Ban do top-down tren mat phang Z=0.

        for (int i = 0; i < count; i++)
        {
            Vector3 viewport = new Vector3(
                Random.Range(margin, 1f - margin),
                Random.Range(margin, 1f - margin), depth);
            Vector3 candidate = camera.ViewportToWorldPoint(viewport);
            candidate.z = 0f;
            if (!IsSafe(candidate, player, minDistance, radius, layers)) continue;
            result = candidate;
            return true;
        }
        return false;
    }
}
