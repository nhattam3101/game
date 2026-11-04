#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 5D.1: Only MainMenu Scene objects are modified. Never writes persistent Save JSON.
public static class MS5D1Installer
{
    private const string Menu = "Tools/MonsterSurvival/5D Profile Overview/";
    private const string AssetRoot = "Assets/MonsterSurvival/Features/Progression/Profile5D1";
    private const string Backup = AssetRoot + "/Backups/MainMenu_BEFORE_5D1.unity";
    private const string LauncherName = "MS5D1_BTN_ProfileOverview";
    private const string OverlayName = "MS5D1_ProfileOverview";

    [MenuItem(Menu + "1 - INSTALL 5D.1 Profile Overview (OPEN MainMenu)")]
    public static void Install()
    {
        if (Application.isPlaying) { EditorUtility.DisplayDialog("5D.1", "Stop Play Mode first.", "OK"); return; }
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "MainMenu" || string.IsNullOrEmpty(scene.path))
        { EditorUtility.DisplayDialog("5D.1", "Open saved MainMenu Scene first.", "OK"); return; }
        GameObject canvas = Find(scene, "MMV3_Canvas");
        GameObject home = Find(scene, "PAGE_Home");
        MS5A2ProfileWidget widget = FindComponent<MS5A2ProfileWidget>(scene);
        MMV3MenuController menu = FindComponent<MMV3MenuController>(scene);
        if (!canvas || !home || !widget || !menu || !canvas.GetComponent<Canvas>() ||
            !canvas.GetComponent<GraphicRaycaster>() || !home.transform.IsChildOf(canvas.transform))
        {
            EditorUtility.DisplayDialog("5D.1 prerequisites missing",
                "Requires MainMenu V3 with MMV3_Canvas/PAGE_Home, MMV3MenuController, and 5A.2 Profile Widget. Nothing changed.", "OK");
            return;
        }
        if (Find(scene, LauncherName) || Find(scene, OverlayName) || FindComponent<MS5D1ProfileOverview>(scene))
        { EditorUtility.DisplayDialog("5D.1", "Already installed or partially installed. Run AUDIT; nothing duplicated.", "OK"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!EditorUtility.DisplayDialog("Install 5D.1 / READ ONLY",
            "Add PROFILE button under ACHIEVEMENTS and a read-only overview overlay to MainMenu V3?\n\n"+
            "Shows the three-slot Save 5A, Permanent Upgrades 5B, map access and achievements 5C.\n"+
            "No JSON writes, no Gold purchases, no changes to SampleScene or gameplay.", "INSTALL", "CANCEL")) return;
        try
        {
            EnsureFolder(AssetRoot + "/Backups");
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(Backup))
            {
                if (!AssetDatabase.CopyAsset(scene.path, Backup)) throw new IOException("Could not back up MainMenu Scene.");
                AssetDatabase.ImportAsset(Backup);
            }
            GameObject btnObject = new GameObject(LauncherName, typeof(RectTransform), typeof(Image), typeof(Button));
            btnObject.layer = 5;
            btnObject.transform.SetParent(home.transform, false);
            Undo.RegisterCreatedObjectUndo(btnObject, "Install 5D.1 Launcher");
            RectTransform rect = btnObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-36f, -362f);
            rect.sizeDelta = new Vector2(245f, 52f);
            Image background = btnObject.GetComponent<Image>();
            background.color = new Color(.25f, .16f, .36f, 1f);
            background.raycastTarget = true;
            Button launcher = btnObject.GetComponent<Button>(); launcher.targetGraphic = background;
            ColorBlock colors = launcher.colors;
            colors.highlightedColor = new Color(1f, .84f, 1f, 1f);
            colors.pressedColor = new Color(.77f, .64f, .93f, 1f);
            launcher.colors = colors;
            GameObject label = new GameObject("TXT_Profile", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.layer = 5; label.transform.SetParent(btnObject.transform, false);
            RectTransform labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            TextMeshProUGUI text = label.GetComponent<TextMeshProUGUI>();
            text.text = "PROFILE"; text.fontSize = 22; text.fontSizeMin = 12; text.fontSizeMax = 22;
            text.enableAutoSizing = true; text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(.95f, .91f, 1f, 1f); text.raycastTarget = false;
            if (TMP_Settings.defaultFontAsset) text.font = TMP_Settings.defaultFontAsset;
            GameObject root = new GameObject(OverlayName, typeof(RectTransform), typeof(MS5D1ProfileOverview));
            root.layer = 5; root.transform.SetParent(canvas.transform, false);
            Undo.RegisterCreatedObjectUndo(root, "Install 5D.1 Overlay");
            RectTransform overlayRect = root.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero; overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = overlayRect.offsetMax = Vector2.zero;
            root.transform.SetAsLastSibling();
            MS5D1ProfileOverview component = root.GetComponent<MS5D1ProfileOverview>();
            component.launcher = launcher; component.existingProfileWidget = widget;
            EditorUtility.SetDirty(component);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[5D.1] INSTALLED: MainMenu profile button and read-only overlay. No Save files touched.");
            EditorUtility.DisplayDialog("5D.1 installed",
                "MainMenu: click PROFILE (below ACHIEVEMENTS).\n\n"+
                "All information is read from verified Save files. Press ESC or X to close.\n"+
                "Scene backup: " + Backup, "OK");
        }
        catch (Exception e)
        {
            Debug.LogError("[5D.1] INSTALL FAILED: " + e);
            EditorUtility.DisplayDialog("5D.1 install failed", e.Message, "OK");
        }
    }

    [MenuItem(Menu + "2 - AUDIT 5D.1 + 3 Profiles (READ ONLY)")]
    public static void Audit()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject launcher = Find(scene, LauncherName);
        MS5D1ProfileOverview overlay = FindComponent<MS5D1ProfileOverview>(scene);
        bool menuOk = scene.name == "MainMenu" && launcher && overlay && overlay.launcher &&
                      overlay.existingProfileWidget && launcher.GetComponent<Button>();
        Debug.Log("[5D.1 AUDIT] Scene=" + scene.name + " | Overview connected=" + menuOk +
                  " | Profile button=" + (launcher ? "FOUND" : "MISSING") + ". Read only.");
        for (int slot = 1; slot <= 3; slot++)
        {
            MS5A1SaveData data;
            bool recovered;
            string reason;
            if (MS5A1SaveService.TryLoad(slot, out data, out reason, out recovered))
            {
                int stacks = 0, equipment = 0, achievements = 0;
                if (data.inventorySnapshotPresent && data.inventorySlots != null)
                    foreach (MS5A3SavedStack s in data.inventorySlots)
                        if (s != null && s.quantity > 0 && !string.IsNullOrEmpty(s.itemId)) stacks++;
                if (data.inventorySnapshotPresent && data.equippedItems != null)
                    foreach (MS5A3SavedEquipped e in data.equippedItems)
                        if (e != null && !string.IsNullOrEmpty(e.itemId)) equipment++;
                if (data.unlockedContentIds != null)
                {
                    if (data.unlockedContentIds.Contains("achievement.shadow_titan")) achievements++;
                    if (data.unlockedContentIds.Contains("achievement.grave_warden")) achievements++;
                }
                Debug.Log("[5D.1 AUDIT] Profile " + slot + " VALID" + (recovered ? " [BACKUP ONLY]" : "") +
                    " | Gold=" + data.bankGold + " Rev=" + data.revision +
                    " | Inventory=" + (data.inventorySnapshotPresent ? stacks.ToString() + "/20" : "NO SNAPSHOT") +
                    " | Equipment=" + equipment + "/3 | Boss badges=" + achievements + "/2");
            }
            else Debug.Log("[5D.1 AUDIT] Profile " + slot + " | " + reason);
        }
        Debug.Log("[5D.1 AUDIT] No Save writes, Gold purchases, inventory mutations or Scene changes performed.");
    }

    [MenuItem(Menu + "3 - REMOVE 5D.1 UI ONLY (OPEN MainMenu)")]
    public static void Remove()
    {
        if (Application.isPlaying) { Debug.LogWarning("[5D.1] Stop Play Mode first."); return; }
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "MainMenu") { Debug.LogWarning("[5D.1] Open MainMenu."); return; }
        if (!EditorUtility.DisplayDialog("Remove 5D.1 UI",
            "Remove only 5D.1 PROFILE launcher and overview?\nSave files and all other menus remain intact.", "REMOVE", "CANCEL")) return;
        GameObject overlay = Find(scene, OverlayName), launcher = Find(scene, LauncherName);
        if (overlay) Undo.DestroyObjectImmediate(overlay);
        if (launcher) Undo.DestroyObjectImmediate(launcher);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[5D.1] Removed overview UI only; Save/Profile data unchanged.");
    }

    private static GameObject Find(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in all) if (t.name == name) return t.gameObject;
        }
        return null;
    }
    private static T FindComponent<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component) return component;
        }
        return null;
    }
    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string folder = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = folder + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(folder, parts[i]);
            folder = next;
        }
    }
}
#endif
