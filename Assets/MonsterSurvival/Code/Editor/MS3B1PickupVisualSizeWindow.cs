#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 3B.1: change only the rendered size of the 3A demo pickups.
/// Preserve the root's Transform / Collider2D / Rigidbody2D / MS3AItemPickup,
/// so pickup reach and 3B loot-table references stay unchanged.
/// </summary>
public sealed class MS3B1PickupVisualSizeWindow : EditorWindow
{
    private const string Demo = "Assets/MonsterSurvival/Features/Inventory/Inventory3A/Content/";
    private const string PotionPath = Demo + "Pickup_Potion.prefab";
    private const string CrystalPath = Demo + "Pickup_Crystal.prefab";
    private const string VisualName = "MS3B1_SmallerWorldVisual";
    private const string BackupFolder = "MonsterSurvival_3B1_PickupBackups";

    // Percentage of ORIGINAL rendered world size. These are not root transforms.
    private float potionFraction = .42f;
    private float crystalFraction = .42f;

    [MenuItem("Tools/MonsterSurvival/3B Item Drops/3 - RESIZE Ground Potion and Crystal (3B.1)")]
    public static void Open()
    {
        var window = GetWindow<MS3B1PickupVisualSizeWindow>("3B.1 Small Ground Loot");
        window.minSize = new Vector2(470f, 345f);
        window.Show();
    }

    private void OnGUI()
    {
        GUILayout.Space(12f);
        EditorGUILayout.LabelField("MONSTERSURVIVAL 3B.1 — SMALLER GROUND DROPS", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Only the WORLD SPRITE is resized. Item icons in Inventory, pickup trigger radius, " +
            "Rigidbody2D, loot chance and amount are NOT changed.\n\n" +
            "The original root SpriteRenderer is kept but hidden; a smaller visual child is created. " +
            "Repeated Apply updates the same child (no duplicates).", MessageType.Info);

        bool potionExists = AssetDatabase.LoadAssetAtPath<GameObject>(PotionPath) != null;
        bool crystalExists = AssetDatabase.LoadAssetAtPath<GameObject>(CrystalPath) != null;
        EditorGUILayout.LabelField("Potion prefab", potionExists ? "FOUND" : "MISSING");
        EditorGUILayout.LabelField("Crystal prefab", crystalExists ? "FOUND" : "MISSING");
        GUILayout.Space(8f);

        potionFraction = EditorGUILayout.Slider("Potion visual size", potionFraction, .20f, 1f);
        crystalFraction = EditorGUILayout.Slider("Crystal visual size", crystalFraction, .20f, 1f);
        EditorGUILayout.LabelField("100% = original size; 42% = smaller world sprite", EditorStyles.miniLabel);
        GUILayout.Space(8f);

        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying || (!potionExists && !crystalExists)))
        {
            if (GUILayout.Button("APPLY — RESIZE WORLD SPRITES", GUILayout.Height(36)))
                Apply();
            if (GUILayout.Button("RESTORE — ORIGINAL VISUAL SIZE", GUILayout.Height(29)))
                Restore();
        }
        if (GUILayout.Button("AUDIT PICKUP SIZE (READ ONLY)", GUILayout.Height(28)))
            Audit();
        GUILayout.Space(4f);
        EditorGUILayout.HelpBox(
            "Do not rerun 3A Installer or 3B setup. Existing Loot Tables point to these same prefabs. " +
            "For custom pickup prefabs, adjust them separately; this tool edits only the two 3A demo prefabs.",
            MessageType.None);
    }

    private void Apply()
    {
        if (!EditorUtility.DisplayDialog("Inventory 3B.1", 
            "Resize visual appearance of Potion and Crystal pickup prefabs?\n\n" +
            "The original root SpriteRenderer will be disabled and a smaller child will be created. " +
            "Collider2D and pickup script remain unchanged. Raw prefab backups are saved once outside Assets.",
            "Apply", "Cancel")) return;

        int ok = 0;
        int failures = 0;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PotionPath) != null)
            if (Patch(PotionPath, potionFraction)) ok++; else failures++;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(CrystalPath) != null)
            if (Patch(CrystalPath, crystalFraction)) ok++; else failures++;
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Inventory 3B.1] Resized " + ok + " pickup prefab(s). Errors=" + failures + ". Pickup triggers unchanged.");
        EditorUtility.DisplayDialog("Inventory 3B.1", "Updated: " + ok + ", errors: " + failures + ".\nSee Console. Test a new drop in Play Mode.", "OK");
    }

    private static bool Patch(string assetPath, float factor)
    {
        GameObject root = null;
        try
        {
            BackupOriginal(assetPath);
            root = PrefabUtility.LoadPrefabContents(assetPath);
            SpriteRenderer original = root.GetComponent<SpriteRenderer>();
            if (original == null || original.sprite == null)
            {
                Debug.LogError("[3B.1] Expected a root SpriteRenderer with sprite in " + assetPath);
                return false;
            }
            Transform visual = root.transform.Find(VisualName);
            if (visual == null)
            {
                GameObject child = new GameObject(VisualName);
                child.transform.SetParent(root.transform, false);
                visual = child.transform;
            }
            SpriteRenderer small = visual.GetComponent<SpriteRenderer>();
            if (small == null) small = visual.gameObject.AddComponent<SpriteRenderer>();
            // Copy Sprite, materials, tint, sorting layer/order so the world visual matches the prefab.
            EditorUtility.CopySerialized(original, small);
            small.enabled = true;
            original.enabled = false;
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = new Vector3(Mathf.Clamp(factor, .20f, 1f), Mathf.Clamp(factor, .20f, 1f), 1f);
            if (PrefabUtility.SaveAsPrefabAsset(root, assetPath) == null)
            {
                Debug.LogError("[3B.1] Save prefab failed: " + assetPath);
                return false;
            }
            Debug.Log("[3B.1] " + assetPath + " now " + Mathf.RoundToInt(factor * 100f) + "% visual size. Root scale / collider untouched.");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError("[3B.1] Apply failed: " + assetPath + " — " + ex.Message);
            return false;
        }
        finally { if (root != null) PrefabUtility.UnloadPrefabContents(root); }
    }

    private void Restore()
    {
        if (!EditorUtility.DisplayDialog("Inventory 3B.1", 
            "Restore ONLY the original world sprite presentation for both demo pickups? " +
            "Inventory, loot tables and colliders will not change.", "Restore", "Cancel")) return;
        int ok = 0;
        if (RestorePrefab(PotionPath)) ok++;
        if (RestorePrefab(CrystalPath)) ok++;
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[3B.1] Restored original visual size on " + ok + " pickup prefab(s).");
    }

    private static bool RestorePrefab(string assetPath)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) == null) return false;
        GameObject root = null;
        try
        {
            root = PrefabUtility.LoadPrefabContents(assetPath);
            Transform child = root.transform.Find(VisualName);
            if (child == null) return false;
            SpriteRenderer original = root.GetComponent<SpriteRenderer>();
            if (original == null)
            {
                Debug.LogError("[3B.1] Cannot restore; root SpriteRenderer missing: " + assetPath);
                return false;
            }
            DestroyImmediate(child.gameObject);
            original.enabled = true;
            if (PrefabUtility.SaveAsPrefabAsset(root, assetPath) == null) return false;
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError("[3B.1] Restore failed: " + assetPath + " — " + ex.Message);
            return false;
        }
        finally { if (root != null) PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void Audit()
    {
        Report(PotionPath);
        Report(CrystalPath);
    }

    private static void Report(string path)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) { Debug.LogWarning("[3B.1 AUDIT] Missing: " + path); return; }
        SpriteRenderer rootSprite = asset.GetComponent<SpriteRenderer>();
        Transform visual = asset.transform.Find(VisualName);
        SpriteRenderer childSprite = visual ? visual.GetComponent<SpriteRenderer>() : null;
        CircleCollider2D trigger = asset.GetComponent<CircleCollider2D>();
        MS3AItemPickup pickup = asset.GetComponent<MS3AItemPickup>();
        string ratio = visual ? Mathf.RoundToInt(visual.localScale.x * 100f) + "%" : "100%";
        Debug.Log("[3B.1 AUDIT] " + asset.name + " | visual=" + ratio +
                  " | root SpriteRenderer=" + (rootSprite ? (rootSprite.enabled ? "ON" : "OFF") : "MISSING") +
                  " | child SpriteRenderer=" + (childSprite ? "YES" : "NO") +
                  " | trigger=" + (trigger && trigger.isTrigger ? "YES" : "MISSING/OFF") +
                  " | pickup=" + (pickup ? "YES" : "MISSING") +
                  " | rootScale=" + asset.transform.localScale);
    }

    private static void BackupOriginal(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string backup = Path.Combine(projectRoot, BackupFolder);
        Directory.CreateDirectory(backup);
        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrEmpty(guid)) return;
        string path = Path.Combine(backup, guid + ".prefab");
        if (!File.Exists(path))
        {
            string source = Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
            File.Copy(source, path);
        }
    }
}
#endif
