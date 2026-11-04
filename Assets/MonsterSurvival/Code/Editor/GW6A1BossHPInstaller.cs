#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Installs ONLY visuals on an explicitly selected GraveWarden Boss prefab.
public static class GW6A1BossHPInstaller
{
    private const string Root = "Assets/MonsterSurvival/Features/UI/HUD6A1_BossHP";
    private const string SpriteRoot = Root + "/Sprites/";
    private const string BarName = "GW_HUD6A1_BossHPBar";

    private static readonly string[] SpriteFiles = {
        "GW6A1_Backplate.png",
        "GW6A1_DamageTrail.png",
        "GW6A1_HP_Purple.png",
        "GW6A1_HP_Phase2.png",
        "GW6A1_OrnateBorder.png"
    };

    [MenuItem("Tools/MonsterSurvival/6A.1 Boss HP V2/1 - Prepare sprites")]
    public static void PrepareSprites()
    {
        for (int i = 0; i < SpriteFiles.Length; i++)
        {
            string path = SpriteRoot + SpriteFiles[i];
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError("GW6A1: Sprite missing: " + path);
                return;
            }
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 160f;
            // In Unity 6, alignment and mesh type belong to TextureImporterSettings.
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = i == 1 || i == 2 || i == 3 ? 9 : 0; // Custom / Center
            importer.SetTextureSettings(settings);
            importer.spritePivot = (i == 1 || i == 2 || i == 3) ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 0.5f);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        AssetDatabase.Refresh();
        Debug.Log("[GW6A1] Prepared five 2D sprites (PPU=160, Point, no compression).");
        EditorUtility.DisplayDialog("Boss HP V2", "5 sprites prepared. Chon GraveWardenBoss.prefab roi chay buoc 2.", "OK");
    }

    [MenuItem("Tools/MonsterSurvival/6A.1 Boss HP V2/2 - Install on SELECTED GraveWarden Prefab")]
    public static void InstallSelected()
    {
        GameObject selected = Selection.activeObject as GameObject;
        string prefabPath = selected == null ? null : AssetDatabase.GetAssetPath(selected);
        if (string.IsNullOrEmpty(prefabPath) ||
            !prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
            !selected.name.Contains("GraveWarden"))
        {
            EditorUtility.DisplayDialog("Boss HP V2", "Chon Prefab GraveWardenBoss trong Project (khong chon Scene Instance).", "OK");
            return;
        }
        var sprites = new Sprite[SpriteFiles.Length];
        for (int i = 0; i < SpriteFiles.Length; i++)
        {
            sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteRoot + SpriteFiles[i]);
            if (sprites[i] == null)
            {
                EditorUtility.DisplayDialog("Boss HP V2", "Chua import sprite. Chay buoc 1 Prepare sprites truoc.", "OK");
                return;
            }
        }
        var answer = EditorUtility.DisplayDialogComplex(
            "6A.1 - Boss HP V2",
            "Cong cu se sao luu va bo sung Boss HP Visual moi vao prefab. " +
            "Thanh HP cu KHONG bi xoa/tat tu dong. Chi tiep tuc neu ban dang o Edit Mode.",
            "Backup + Install", "Cancel", "Install (no backup)");
        if (answer == 1) return;
        if (answer == 0)
        {
            const string backupFolder = Root + "/Backups";
            if (!AssetDatabase.IsValidFolder(backupFolder))
                AssetDatabase.CreateFolder(Root, "Backups");
            string filename = selected.name + "_Backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".prefab";
            string backupPath = AssetDatabase.GenerateUniqueAssetPath(backupFolder + "/" + filename);
            if (!AssetDatabase.CopyAsset(prefabPath, backupPath))
            {
                EditorUtility.DisplayDialog("Boss HP V2", "Khong tao duoc backup. Chua sua Prefab.", "OK");
                return;
            }
            Debug.Log("[GW6A1] Backup created: " + backupPath);
        }

        GameObject prefab = null;
        try
        {
            prefab = PrefabUtility.LoadPrefabContents(prefabPath);
            var health = prefab.GetComponent<Health>();
            var ai = prefab.GetComponent<GraveWardenBossAI>();
            if (health == null || ai == null)
            {
                EditorUtility.DisplayDialog("Boss HP V2", "Prefab khong co Health hoac GraveWardenBossAI tren root. Chua thay doi.", "OK");
                return;
            }
            var old = prefab.transform.Find(BarName);
            var bar = old != null ? old.gameObject : new GameObject(BarName);
            bar.transform.SetParent(prefab.transform, false);
            bar.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            bar.transform.localRotation = Quaternion.identity;
            bar.transform.localScale = Vector3.one * 0.62f;
            bar.SetActive(true);

            SpriteRenderer sample = null;
            var gwVisual = prefab.transform.Find("GW_Visual");
            if (gwVisual != null) sample = gwVisual.GetComponent<SpriteRenderer>();
            if (sample == null) sample = prefab.GetComponentInChildren<SpriteRenderer>();
            int sortingId = sample != null ? sample.sortingLayerID : 0;
            int baseOrder = sample != null ? sample.sortingOrder + 15 : 100;

            var bg = EnsureLayer(bar.transform,"Backplate", sprites[0],0f,baseOrder + 0,sortingId);
            var tr = EnsureLayer(bar.transform,"DamageTrail", sprites[1],-1.6f,baseOrder + 1,sortingId);
            var hp = EnsureLayer(bar.transform,"HPFill", sprites[2],-1.6f,baseOrder + 2,sortingId);
            var border = EnsureLayer(bar.transform,"Border", sprites[4],0f,baseOrder + 3,sortingId);

            var component = prefab.GetComponent<GW6A1BossHPBarV2>();
            if (component == null) component = prefab.AddComponent<GW6A1BossHPBarV2>();
            var so = new SerializedObject(component);
            so.FindProperty("health").objectReferenceValue = health;
            so.FindProperty("graveWardenAI").objectReferenceValue = ai;
            so.FindProperty("barRoot").objectReferenceValue = bar.transform;
            so.FindProperty("backplate").objectReferenceValue = bg;
            so.FindProperty("damageTrail").objectReferenceValue = tr;
            so.FindProperty("hpFill").objectReferenceValue = hp;
            so.FindProperty("ornateBorder").objectReferenceValue = border;
            so.FindProperty("normalHpSprite").objectReferenceValue = sprites[2];
            so.FindProperty("phase2HpSprite").objectReferenceValue = sprites[3];
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
            Debug.Log("[GW6A1] Boss HP V2 installed: " + prefabPath);
        }
        catch (Exception error)
        {
            Debug.LogError("[GW6A1] Install failed: " + error);
            EditorUtility.DisplayDialog("Boss HP V2", "Gap loi khi cai. Xem Unity Console, neu can khoi phuc tu backup.", "OK");
            return;
        }
        finally
        {
            if (prefab != null) PrefabUtility.UnloadPrefabContents(prefab);
        }
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Boss HP V2", "Da gan vao GraveWardenBoss prefab. Neu thay 2 thanh mau, chi tat renderer cua thanh cu.", "OK");
    }

    private static SpriteRenderer EnsureLayer(Transform parent, string name, Sprite sprite,
        float x, int order, int sortingLayerId)
    {
        var node = parent.Find(name);
        if (node == null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            node = go.transform;
        }
        node.localPosition = new Vector3(x, 0f, 0f);
        node.localScale = Vector3.one;
        node.localRotation = Quaternion.identity;
        var r = node.GetComponent<SpriteRenderer>();
        if (r == null) r = node.gameObject.AddComponent<SpriteRenderer>();
        r.sprite = sprite;
        r.sortingLayerID = sortingLayerId;
        r.sortingOrder = order;
        r.color = Color.white;
        r.enabled = true;
        return r;
    }

    [MenuItem("Tools/MonsterSurvival/6A.1 Boss HP V2/3 - Remove V2 from SELECTED Prefab")]
    public static void RemoveSelected()
    {
        GameObject selected = Selection.activeObject as GameObject;
        string prefabPath = selected != null ? AssetDatabase.GetAssetPath(selected) : null;
        if (string.IsNullOrEmpty(prefabPath) ||
            !prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
            !selected.name.Contains("GraveWarden"))
        {
            EditorUtility.DisplayDialog("Boss HP V2", "Chon GraveWardenBoss.prefab trong Project.", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("Remove only Boss HP V2?",
            "Chi go bo object GW_HUD6A1_BossHPBar va script GW6A1BossHPBarV2. Health/AI/Animator giu nguyen.",
            "Remove V2", "Cancel")) return;
        var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var bar = prefab.transform.Find(BarName);
            if (bar != null) UnityEngine.Object.DestroyImmediate(bar.gameObject);
            var script = prefab.GetComponent<GW6A1BossHPBarV2>();
            if (script != null) UnityEngine.Object.DestroyImmediate(script);
            PrefabUtility.SaveAsPrefabAsset(prefab,prefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Boss HP V2", "Da go Boss HP V2. Ban co the bat lai thanh HP cu.", "OK");
    }
}
#endif
