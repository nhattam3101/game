#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// EDITOR-ONLY. Creates obstacle prefabs + placements once; NO runtime Instantiate/texture generation.
public sealed class MonsterSurvivalObstacleBuilder4B4 : EditorWindow
{
    private const string Root = "Assets/MonsterSurvival/Features/World/MonsterSurvivalObstacles4B4";
    private const string Prefabs = Root + "/GeneratedPrefabs";

    private GameObject meadowRoot;
    private GameObject desertRoot;
    private GameObject ruinsRoot;
    private int obstacleCountPerMap = 14;
    private float placementRadius = 19f;
    private float clearCenterRadius = 6f;
    private float minimumSpacing = 2.25f;
    private int seed = 2026;

    // Each map gets decorative+solid variants, but only solid assets have colliders.
    private static readonly string[][] SpriteNames =
    {
        new[] {"Meadow_Tree_192", "Meadow_Rock_192"},
        new[] {"Desert_Cactus_192", "Desert_Boulder_192"},
        new[] {"Ruins_Pillar_192", "Ruins_Rubble_192", "Ruins_Crystal_192"}
    };

    [MenuItem("Tools/MonsterSurvival/Place Obstacles 4B.4")]
    private static void Open() => GetWindow<MonsterSurvivalObstacleBuilder4B4>("Obstacles 4B.4");

    private void OnGUI()
    {
        EditorGUILayout.LabelField("MONSTERSURVIVAL | OBSTACLES 4B.4", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Tao Prefab + Collider va dat vat can TRONG EDITOR. Ground Tilemap 4B.2 can co truoc. KHONG ve texture luc Play.", MessageType.Info);
        meadowRoot = (GameObject)EditorGUILayout.ObjectField("Map_01_Meadow", meadowRoot, typeof(GameObject), true);
        desertRoot = (GameObject)EditorGUILayout.ObjectField("Map_02_Desert", desertRoot, typeof(GameObject), true);
        ruinsRoot = (GameObject)EditorGUILayout.ObjectField("Map_03_Ruins", ruinsRoot, typeof(GameObject), true);
        obstacleCountPerMap = EditorGUILayout.IntSlider("Obstacles per map", obstacleCountPerMap, 2, 40);
        placementRadius = EditorGUILayout.Slider("Placement radius", placementRadius, 8f, 42f);
        clearCenterRadius = EditorGUILayout.Slider("Clear center radius", clearCenterRadius, 3f, 10f);
        minimumSpacing = EditorGUILayout.Slider("Min spacing", minimumSpacing, 1.2f, 5f);
        seed = EditorGUILayout.IntField("Random seed", seed);
        EditorGUILayout.HelpBox("Generated props can be moved or deleted manually afterward. Rebuilding removes only children of Obstacles_4B4.", MessageType.None);
        GUI.enabled = !EditorApplication.isPlayingOrWillChangePlaymode;
        if (GUILayout.Button("CREATE PREFABS + PLACE OBSTACLES", GUILayout.Height(36))) Build();
        GUI.enabled = true;
    }

    private void Build()
    {
        int obstacleLayer = LayerMask.NameToLayer("Obstacles");
        if (obstacleLayer < 0)
        {
            EditorUtility.DisplayDialog("Obstacles layer missing", "Tao Layer 'Obstacles' trong Tags and Layers truoc, roi quay lai.", "OK");
            return;
        }
        GameObject[] roots = {meadowRoot, desertRoot, ruinsRoot};
        HashSet<GameObject> unique = new HashSet<GameObject>();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] == null || !roots[i].scene.IsValid() || !unique.Add(roots[i]))
            {
                EditorUtility.DisplayDialog("Map Root incorrect", "Gan 3 Map Root KHAC NHAU tu Scene Hierarchy.", "OK");
                return;
            }
            if (roots[i].transform.Find("MapGrid_4B2/Ground_Tilemap") == null)
            {
                EditorUtility.DisplayDialog("Ground Tilemap missing", roots[i].name + " chua co MapGrid_4B2/Ground_Tilemap. Hay Build Tilemaps 4B.2 truoc.", "OK");
                return;
            }
        }
        if (!AssetDatabase.IsValidFolder(Prefabs))
            AssetDatabase.CreateFolder(Root, "GeneratedPrefabs");

        GameObject[][] mapPrefabs = new GameObject[3][];
        for (int m = 0; m < 3; m++)
        {
            mapPrefabs[m] = new GameObject[SpriteNames[m].Length];
            for (int i = 0; i < SpriteNames[m].Length; i++)
            {
                string name = SpriteNames[m][i];
                string imagePath = Root + "/Art/Props/" + name + ".png";
                Sprite sprite = LoadSprite(imagePath);
                if (sprite == null)
                {
                    EditorUtility.DisplayDialog("Sprite missing", "Missing sprite: " + imagePath + ". Copy full Assets folder from ZIP.", "OK");
                    return;
                }
                mapPrefabs[m][i] = MakePrefab(name, sprite, obstacleLayer);
            }
        }
        for (int i = 0; i < roots.Length; i++)
            PlaceOnMap(roots[i], mapPrefabs[i], i);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Obstacles 4B.4", "Da tao prefab va vat can trong Scene. Ctrl+S de luu. Gan Obstacles vao MapSafeSpawn2D/EnemyObstacleAvoidance2D.", "OK");
    }

    private static Sprite LoadSprite(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return null;
        if (importer.textureType != TextureImporterType.Sprite ||
            importer.spritePixelsPerUnit != 192f || importer.mipmapEnabled)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 192f;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static GameObject MakePrefab(string name, Sprite sprite, int layer)
    {
        string path = Prefabs + "/" + name + ".prefab";
        GameObject old = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (old != null) return old; // Don't overwrite user edits to a prefab.
        GameObject temp = new GameObject(name);
        temp.layer = layer;
        SpriteRenderer sr = temp.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = -5; // Above Ground (-100), below characters (usually 0).
        // Visual sprite stays 1 unit wide; Collider covers only solid trunk/rock core.
        if (name.Contains("Tree") || name.Contains("Cactus") || name.Contains("Crystal"))
        {
            CircleCollider2D circle = temp.AddComponent<CircleCollider2D>();
            circle.radius = 0.24f;
            circle.offset = new Vector2(0f, -0.22f);
            circle.isTrigger = false;
        }
        else if (name.Contains("Pillar"))
        {
            BoxCollider2D box = temp.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.4f, 0.46f);
            box.offset = new Vector2(0f, -0.16f);
            box.isTrigger = false;
        }
        else
        {
            CircleCollider2D circle = temp.AddComponent<CircleCollider2D>();
            circle.radius = 0.31f;
            circle.isTrigger = false;
        }
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
        DestroyImmediate(temp);
        return prefab;
    }

    private void PlaceOnMap(GameObject mapRoot, GameObject[] prefabs, int index)
    {
        Transform old = mapRoot.transform.Find("Obstacles_4B4");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        GameObject container = new GameObject("Obstacles_4B4");
        Undo.RegisterCreatedObjectUndo(container, "Create map obstacles");
        container.transform.SetParent(mapRoot.transform, false);
        Tilemap tilemap = mapRoot.transform.Find("MapGrid_4B2/Ground_Tilemap").GetComponent<Tilemap>();
        System.Random rng = new System.Random(seed + index * 463);
        List<Vector2> placed = new List<Vector2>();
        int created = 0;
        int maxAttempts = obstacleCountPerMap * 80;

        for (int attempts = 0; attempts < maxAttempts && created < obstacleCountPerMap; attempts++)
        {
            // Editor-only deterministic placement around map center, leaving a safe starting zone.
            double angle = rng.NextDouble() * Math.PI * 2d;
            double t = rng.NextDouble();
            float radius = Mathf.Lerp(clearCenterRadius, placementRadius, Mathf.Sqrt((float)t));
            Vector3 pos = mapRoot.transform.position +
                new Vector3(Mathf.Cos((float)angle), Mathf.Sin((float)angle), 0f) * radius;
            pos.z = 0f;
            if (!tilemap.HasTile(tilemap.WorldToCell(pos))) continue;
            bool tooClose = false;
            foreach (Vector2 p in placed)
            {
                if (Vector2.Distance(p, pos) < minimumSpacing) { tooClose = true; break; }
            }
            if (tooClose) continue;
            GameObject prefab = prefabs[rng.Next(prefabs.Length)];
            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, mapRoot.scene);
            Undo.RegisterCreatedObjectUndo(inst, "Place obstacle");
            inst.transform.SetParent(container.transform, true);
            inst.transform.position = pos;
            inst.name = prefab.name;
            placed.Add(pos);
            created++;
        }
        Debug.Log("[Obstacles 4B.4] " + mapRoot.name + ": placed " + created + "/" + obstacleCountPerMap + ". Move props manually if they sit on a road.");
    }
}
#endif
