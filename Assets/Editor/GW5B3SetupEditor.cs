#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Editor-only importer + prefab binding. No runtime FindObjects, no AI or WaveManager changes.
public static class GW5B3SetupEditor
{
    private const string Root = "Assets/MonsterSurvival/GraveWarden5B3/Frames/";

    [MenuItem("Tools/MonsterSurvival/5B.3 Setup Grave Warden VFX")]
    public static void Setup()
    {
        string prefabPath = AssetDatabase.GetAssetPath(Selection.activeObject);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null || prefab.name != "GraveWardenBoss" ||
            PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.NotAPrefab)
        {
            EditorUtility.DisplayDialog("Grave Warden 5B.3",
                "Select the GraveWardenBoss PREFAB asset in the Project window first, then run this menu command. Do not select the Scene instance.", "OK");
            return;
        }

        string[] keys = {"TombWall", "WraithPortal", "Reanimate", "SpiritOrbit", "SoulHarvest"};
        Sprite[][] arrays = new Sprite[keys.Length][];
        for (int i = 0; i < keys.Length; i++)
        {
            arrays[i] = new Sprite[6];
            for (int j = 0; j < 6; j++)
            {
                string path = Root + keys[i] + "/" + keys[i] + "_" + j.ToString("00") + ".png";
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogError("[GW5B3] Missing VFX PNG: " + path);
                    EditorUtility.DisplayDialog("Grave Warden 5B.3", "Missing image: " + path + "\nImport the Frames folder first.", "OK");
                    return;
                }
                if (importer.textureType != TextureImporterType.Sprite ||
                    importer.spritePixelsPerUnit != 256f || importer.mipmapEnabled ||
                    importer.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = 256f;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.SaveAndReimport();
                }
                arrays[i][j] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (arrays[i][j] == null)
                {
                    Debug.LogError("[GW5B3] Could not load Sprite from " + path);
                    return;
                }
            }
        }

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            GraveWardenVFX5B3 vfx = root.GetComponent<GraveWardenVFX5B3>();
            if (vfx == null) vfx = root.AddComponent<GraveWardenVFX5B3>();
            vfx.tombWallRise = arrays[0];
            vfx.wraithPortal = arrays[1];
            vfx.reanimateRise = arrays[2];
            vfx.spiritOrbit = arrays[3];
            vfx.soulHarvest = arrays[4];
            EditorUtility.SetDirty(vfx);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("[GW5B3] VFX successfully configured on " + prefabPath + ": 5 effects / 30 Sprites.");
        EditorUtility.DisplayDialog("Grave Warden 5B.3", "Done! Bound 30 VFX frames to GraveWardenVFX5B3 on the prefab. Check Inspector, then Play Ancient Ruins Wave 5.", "OK");
    }
}
#endif
