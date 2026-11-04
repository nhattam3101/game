#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// CHI CHAY TRONG UNITY EDITOR. KHONG TAO TEXTURE / TILE KHI PLAY.
// Menu: Tools > MonsterSurvival > Build Static Tilemaps 4B.2
public class MonsterSurvivalTilemapBuilder : EditorWindow
{
    private const string RootPath = "Assets/MonsterSurvivalMaps4B2";
    private GameObject meadowRoot;
    private GameObject desertRoot;
    private GameObject ruinsRoot;
    private int width = 96;
    private int height = 96;
    private int randomSeed = 2026;
    private bool hideOldGround = true;
    private int decorationChancePercent = 0; // 0 = it nhat draw call; them bien the neu can.

    [MenuItem("Tools/MonsterSurvival/Build Static Tilemaps 4B.2")]
    private static void Open()
    {
        GetWindow<MonsterSurvivalTilemapBuilder>("Static Maps 4B.2");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("MONSTERSURVIVAL | MAPS 4B.2", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Tao Tilemap 1 lan trong EDITOR. Khi Play, Tilemap da duoc luu trong Scene, KHONG sinh texture runtime.\n" +
            "Keo 3 Map Root trong Hierarchy (khong keo Prefab).", MessageType.Info);

        meadowRoot = (GameObject)EditorGUILayout.ObjectField("Map_01_Meadow", meadowRoot, typeof(GameObject), true);
        desertRoot = (GameObject)EditorGUILayout.ObjectField("Map_02_Desert", desertRoot, typeof(GameObject), true);
        ruinsRoot = (GameObject)EditorGUILayout.ObjectField("Map_03_Ruins", ruinsRoot, typeof(GameObject), true);
        EditorGUILayout.Space(8);
        width = EditorGUILayout.IntSlider("Tiles wide", width, 32, 192);
        height = EditorGUILayout.IntSlider("Tiles high", height, 32, 192);
        randomSeed = EditorGUILayout.IntField("Map seed", randomSeed);
        decorationChancePercent = EditorGUILayout.IntSlider("Variation %", decorationChancePercent, 0, 20);
        hideOldGround = EditorGUILayout.Toggle("Disable old Ground Sprite", hideOldGround);
        EditorGUILayout.HelpBox("128px/128 PPU = 1 Unity unit per Tile. Map 96 x 96 bao phu 96 x 96 world units.", MessageType.None);
        EditorGUILayout.Space(8);

        GUI.enabled = !EditorApplication.isPlayingOrWillChangePlaymode;
        if (GUILayout.Button("BUILD 3 STATIC TILEMAPS", GUILayout.Height(38)))
            Build();
        GUI.enabled = true;
    }

    private void Build()
    {
        if (meadowRoot == null || desertRoot == null || ruinsRoot == null)
        {
            EditorUtility.DisplayDialog("Missing references", "Hay keo du 3 Map Root tu Hierarchy vao cua so.", "OK");
            return;
        }
        if (meadowRoot == desertRoot || meadowRoot == ruinsRoot || desertRoot == ruinsRoot)
        {
            EditorUtility.DisplayDialog("Wrong references", "3 Map Root phai la 3 GameObject khac nhau.", "OK");
            return;
        }
        GameObject[] roots = { meadowRoot, desertRoot, ruinsRoot };
        string[] names = { "Meadow", "Desert", "Ruins" };
        for (int i = 0; i < roots.Length; i++)
        {
            if (!roots[i].scene.IsValid())
            {
                EditorUtility.DisplayDialog("Scene required", "Map Root phai la doi tuong trong Hierarchy Scene, khong phai Prefab Asset.", "OK");
                return;
            }
            if (roots[i].transform.localScale != Vector3.one)
                Debug.LogWarning("Maps 4B.2: " + roots[i].name + " Scale khac (1,1,1), kich thuoc Tilemap se bi thay doi.");
        }

        EnsureFolders(RootPath + "/Generated");
        // Import tat ca anh truoc khi thay doi Scene.
        Sprite[][] sprites = new Sprite[3][];
        for (int m = 0; m < 3; m++)
        {
            sprites[m] = new Sprite[12];
            for (int i = 0; i < 12; i++)
            {
                string path = RootPath + "/Tiles/" + names[m] + "/" + names[m] + "_Tile" + i.ToString("00") + ".png";
                sprites[m][i] = LoadSprite(path);
                if (sprites[m][i] == null)
                {
                    EditorUtility.DisplayDialog("Tile missing", "Khong tim thay Sprite: " + path + "\nHay copy NGUYEN thu muc Assets tu ZIP vao project.", "OK");
                    return;
                }
            }
        }

        for (int m = 0; m < 3; m++)
        {
            Tile[] tiles = new Tile[12];
            EnsureFolders(RootPath + "/Generated/" + names[m]);
            for (int i = 0; i < 12; i++)
            {
                string path = RootPath + "/Generated/" + names[m] + "/" + names[m] + "_Ground" + i.ToString("00") + ".asset";
                Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
                if (tile == null)
                {
                    tile = ScriptableObject.CreateInstance<Tile>();
                    AssetDatabase.CreateAsset(tile, path);
                }
                tile.sprite = sprites[m][i];
                tile.color = Color.white;
                tile.colliderType = Tile.ColliderType.None; // Nen KHONG co collider.
                EditorUtility.SetDirty(tile);
                tiles[i] = tile;
            }
            BuildOne(roots[m], names[m], tiles, randomSeed + m * 18713);
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Maps 4B.2", "Da tao 3 Tilemap tinh trong Scene. Ctrl+S de luu Scene.\nMap cu (Ground) duoc " + (hideOldGround ? "TAT" : "GIU") + ".", "OK");
        Debug.Log("[Maps 4B.2] Built 3 editor-only tilemaps: " + width + "x" + height + ". No runtime texture generation.");
    }

    private static void EnsureFolders(string fullPath)
    {
        string[] parts = fullPath.Split('/');
        string parent = parts[0]; // Assets
        for (int i = 1; i < parts.Length; i++)
        {
            string path = parent + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, parts[i]);
            parent = path;
        }
    }

    private static Sprite LoadSprite(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return null;
        bool dirty = importer.textureType != TextureImporterType.Sprite ||
                     importer.spriteImportMode != SpriteImportMode.Single ||
                     Math.Abs(importer.spritePixelsPerUnit - 128f) > 0.01f ||
                     importer.isReadable || importer.mipmapEnabled;
        if (dirty)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 128f;
            importer.isReadable = false;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private void BuildOne(GameObject mapRoot, string biome, Tile[] tiles, int seed)
    {
        Transform gridTransform = mapRoot.transform.Find("MapGrid_4B2");
        GameObject gridGo;
        if (gridTransform == null)
        {
            gridGo = new GameObject("MapGrid_4B2", typeof(Grid));
            Undo.RegisterCreatedObjectUndo(gridGo, "Create Map Grid");
            gridGo.transform.SetParent(mapRoot.transform, false);
        }
        else gridGo = gridTransform.gameObject;
        gridGo.transform.localPosition = Vector3.zero;
        gridGo.transform.localScale = Vector3.one;
        Grid grid = gridGo.GetComponent<Grid>();
        if (grid == null) grid = Undo.AddComponent<Grid>(gridGo);
        grid.cellSize = Vector3.one;

        Transform tileTransform = gridGo.transform.Find("Ground_Tilemap");
        GameObject tileGo;
        if (tileTransform == null)
        {
            tileGo = new GameObject("Ground_Tilemap", typeof(Tilemap), typeof(TilemapRenderer));
            Undo.RegisterCreatedObjectUndo(tileGo, "Create Ground Tilemap");
            tileGo.transform.SetParent(gridGo.transform, false);
        }
        else tileGo = tileTransform.gameObject;
        tileGo.transform.localPosition = Vector3.zero;
        tileGo.transform.localScale = Vector3.one;
        Tilemap tilemap = tileGo.GetComponent<Tilemap>();
        if (tilemap == null) tilemap = Undo.AddComponent<Tilemap>(tileGo);
        TilemapRenderer renderer = tileGo.GetComponent<TilemapRenderer>();
        if (renderer == null) renderer = Undo.AddComponent<TilemapRenderer>(tileGo);
        renderer.sortingOrder = -100;
        // Reset khi Build lai: khong de lai Tile cua size cu.
        Undo.RecordObject(tilemap, "Rebuild Ground Tilemap");
        tilemap.ClearAllTiles();

        BoundsInt bounds = new BoundsInt(-width / 2, -height / 2, 0, width, height, 1);
        TileBase[] cells = new TileBase[width * height];
        System.Random random = new System.Random(seed);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Mac dinh chi Tile00 de nhe nhat. Co the bat tile bien the trong Editor.
                int tileIndex = random.Next(100) < decorationChancePercent ? 1 + random.Next(11) : 0;
                cells[y * width + x] = tiles[tileIndex];
            }
        }
        tilemap.SetTilesBlock(bounds, cells);
        tilemap.RefreshAllTiles();
        EditorUtility.SetDirty(tilemap);

        Transform oldGround = mapRoot.transform.Find("Ground");
        if (hideOldGround && oldGround != null && oldGround.GetComponent<SpriteRenderer>() != null)
            oldGround.gameObject.SetActive(false); // GIU LAI de quay ve ban 4B cu.
        Debug.Log("[Maps 4B.2] " + mapRoot.name + " / " + biome + " / " + width + "x" + height);
    }
}
#endif
