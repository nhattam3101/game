#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Safe additive install / audit / uninstall, Scene only (no existing gameplay script replaced).
public static class MS3D5EquipmentInstaller
{
    private const string Root = "Assets/MonsterSurvival/Equipment3D5";
    private const string AuraPath = Root + "/Art/MS3D5_Aura.png";
    private const string RingPath = Root + "/Art/MS3D5_Ring.png";
    private const string BackupDir = Root + "/Backups";
    private const string GlowName = "MS3D5_RarityGlow";
    private const string PulseName = "MS3D5_EquipFlash";

    [MenuItem("Tools/MonsterSurvival/3D Equipment/13 - INSTALL 3D.5 Equipment VFX (OPEN Scene)")]
    public static void Install()
    {
        if (Application.isPlaying) { Message("Stop Play Mode before installing."); return; }
        Scene scene = SceneManager.GetActiveScene();
        var ui = FindSceneComponent<MS3DEquipmentUI>(scene);
        var inv = ui ? ui.inventoryUI : null;
        var player = ui ? ui.equipment : null;
        if (!ui || !inv || !player || !inv.panel || !ui.equipmentWindow ||
            ui.slotBorders == null || ui.slotBorders.Length < 3)
        {
            Message("Missing 3D.1 Equipment UI / Player Equipment / slot cards. Open installed SampleScene, run 3D.1 AUDIT first. No changes made.");
            return;
        }
        if (ui.GetComponent<MS3D5EquipmentVFX>() || player.GetComponent<MS3D5WorldEquipBurst>())
        {
            Message("3D.5 component already exists. Use AUDIT or REMOVE first. No duplicate installation.");
            return;
        }
        Sprite aura = PrepareSprite(AuraPath), ring = PrepareSprite(RingPath);
        if (!aura || !ring) { Message("Missing 3D.5 PNG sprites in Equipment3D5/Art. Import the full ZIP Assets folder."); return; }
        for (int i = 0; i < 3; i++)
            if (!ui.slotBorders[i] || !ui.slotBorders[i].transform.parent.GetComponent<RectTransform>())
            { Message("Equipment slot " + i + " missing expected RectTransform. Installation aborted."); return; }
        if (string.IsNullOrEmpty(scene.path)) { Message("Save SampleScene to Assets before installing."); return; }
        if (!EditorUtility.DisplayDialog("MonsterSurvival 3D.5", "Back up the open Scene and add quality glow, equip/unequip pulses, and a read-only equipment audit?\n\nNo 3A-3D4 scripts or catalogs will be replaced. No gameplay stats will be touched.", "INSTALL", "Cancel")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BackupScene(scene);

        var world = Undo.AddComponent<MS3D5WorldEquipBurst>(player.gameObject);
        Undo.RecordObject(world, "3D.5 world VFX");
        world.ringSprite = ring;
        var vfx = Undo.AddComponent<MS3D5EquipmentVFX>(ui.gameObject);
        Undo.RecordObject(vfx, "3D.5 equipment VFX");
        vfx.equipmentUI = ui;
        vfx.equipment = player;
        vfx.playerWorldFX = world;
        vfx.rarityGlows = new Image[3];
        vfx.pulseRings = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            RectTransform card = (RectTransform)ui.slotBorders[i].transform.parent;
            var glow = MakeImage(GlowName, card, aura);
            var g = glow.rectTransform;
            g.anchorMin = Vector2.zero; g.anchorMax = Vector2.one;
            g.offsetMin = g.offsetMax = Vector2.zero;
            g.SetAsFirstSibling();
            var pulse = MakeImage(PulseName, card, ring);
            var p = pulse.rectTransform;
            p.anchorMin = p.anchorMax = new Vector2(.20f, .51f);
            p.sizeDelta = new Vector2(116, 116);
            p.anchoredPosition = Vector2.zero;
            p.SetSiblingIndex(1);
            pulse.color = Color.clear;
            vfx.rarityGlows[i] = glow;
            vfx.pulseRings[i] = pulse;
        }
        EditorUtility.SetDirty(vfx);
        EditorUtility.SetDirty(world);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[3D.5] Installed cosmetic UI and World FX. Existing Inventory, Equipment events, all Stats and Loot Tables unchanged.");
        Message("Installed! Play -> collect equipment -> I -> EQUIP / UNEQUIP. For checks use menu 14 - AUDIT and 16 - LIVE QA.");
    }

    [MenuItem("Tools/MonsterSurvival/3D Equipment/14 - AUDIT 3D.5 VFX (READ ONLY)")]
    public static void Audit()
    {
        Scene scene = SceneManager.GetActiveScene();
        var ui = FindSceneComponent<MS3DEquipmentUI>(scene);
        var vfx = ui ? ui.GetComponent<MS3D5EquipmentVFX>() : null;
        var eq = ui ? ui.equipment : null;
        var world = eq ? eq.GetComponent<MS3D5WorldEquipBurst>() : null;
        int glow = 0, flash = 0;
        if (vfx != null)
            for (int i = 0; i < 3; i++)
            {
                if (vfx.rarityGlows != null && vfx.rarityGlows.Length > i && vfx.rarityGlows[i] &&
                    !vfx.rarityGlows[i].raycastTarget) glow++;
                if (vfx.pulseRings != null && vfx.pulseRings.Length > i && vfx.pulseRings[i] &&
                    !vfx.pulseRings[i].raycastTarget) flash++;
            }
        Debug.Log("[3D.5 AUDIT] Scene=" + scene.name +
                  " | Inventory=" + (ui && ui.inventoryUI ? "OK" : "MISSING") +
                  " | Equipment=" + (eq ? "OK" : "MISSING") +
                  " | WorldFX=" + (world && world.ringSprite ? "OK" : "MISSING") +
                  " | Glows=" + glow + "/3" +
                  " | Pulses=" + flash + "/3" +
                  " | Existing equip/unequip buttons unchanged.");
        if (!vfx || !world || !world.ringSprite || glow != 3 || flash != 3)
            Debug.LogWarning("[3D.5 AUDIT] Installation incomplete. Inspect component refs; do not run INSTALL twice.");
    }

    [MenuItem("Tools/MonsterSurvival/3D Equipment/15 - REMOVE 3D.5 Visuals (OPEN Scene)")]
    public static void Remove()
    {
        if (Application.isPlaying) { Message("Stop Play Mode before removal."); return; }
        Scene scene = SceneManager.GetActiveScene();
        var ui = FindSceneComponent<MS3DEquipmentUI>(scene);
        var vfx = ui ? ui.GetComponent<MS3D5EquipmentVFX>() : null;
        var world = ui && ui.equipment ? ui.equipment.GetComponent<MS3D5WorldEquipBurst>() : null;
        if (!vfx && !world) { Message("No 3D.5 components in this Scene."); return; }
        if (!EditorUtility.DisplayDialog("Remove 3D.5", "Remove ONLY 3D.5 UI glow/ring objects and the two visual components? Existing Inventory and Equipment remain.", "REMOVE", "Cancel")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (vfx)
        {
            if (vfx.rarityGlows != null)
                foreach (var image in vfx.rarityGlows) if (image) Undo.DestroyObjectImmediate(image.gameObject);
            if (vfx.pulseRings != null)
                foreach (var image in vfx.pulseRings) if (image) Undo.DestroyObjectImmediate(image.gameObject);
            Undo.DestroyObjectImmediate(vfx);
        }
        if (world) Undo.DestroyObjectImmediate(world);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Message("3D.5 effects removed. Other scripts, scenes and saved item assets untouched.");
    }

    private static Image MakeImage(string name, Transform parent, Sprite sprite)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(go, "3D.5 add UI effect");
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.raycastTarget = false;
        return image;
    }

    private static Sprite PrepareSprite(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (!importer) return null;
        if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static T FindSceneComponent<T>(Scene scene) where T : Component
    {
        if (!scene.IsValid()) return null;
        foreach (var go in scene.GetRootGameObjects())
        {
            var result = go.GetComponentInChildren<T>(true);
            if (result) return result;
        }
        return null;
    }
    private static void BackupScene(Scene scene)
    {
        string relative = BackupDir + "/" + Path.GetFileNameWithoutExtension(scene.path) + "_Before3D5.unity";
        EnsureFolder(BackupDir);
        if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(relative))
        {
            if (!AssetDatabase.CopyAsset(scene.path, relative))
                throw new InvalidOperationException("Cannot back up " + scene.path);
            AssetDatabase.ImportAsset(relative);
        }
    }
    private static void EnsureFolder(string folder)
    {
        var parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
    private static void Message(string msg) { EditorUtility.DisplayDialog("MonsterSurvival 3D.5", msg, "OK"); }
}
#endif
