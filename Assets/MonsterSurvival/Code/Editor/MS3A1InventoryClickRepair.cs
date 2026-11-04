#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Standalone Scene repair for Inventory3A. Does not reinstall inventory,
// change item stacks, rewrite gameplay scripts or destroy scene objects.
public static class MS3A1InventoryClickRepair
{
    private const string MenuRoot = "Tools/MonsterSurvival/3A Inventory/";

    [MenuItem(MenuRoot + "4 - REPAIR Item Slot Clicks (OPEN Scene)")]
    public static void Repair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Inventory 3A.1", "Stop Play Mode before applying a Scene repair.", "OK");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[3A.1] No loaded active Scene.");
            return;
        }

        var inventoryRoot = GetInventoryRoot(scene);
        if (inventoryRoot == null)
        {
            EditorUtility.DisplayDialog("Inventory 3A.1", "MS3A_InventoryCanvas was not found in the open Scene. Install 3A before running this repair.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("Inventory 3A.1 — Repair Slot Clicks",
            "Enable raycastTarget on the 20 slot Images in the open Scene. This only fixes pointer interaction; all existing item, UI and gameplay references are retained. Continue?",
            "Repair", "Cancel"))
            return;

        int seen = 0, changed = 0;
        foreach (var cell in inventoryRoot.GetComponentsInChildren<MS3AInventorySlotUI>(true))
        {
            if (cell == null || cell.frame == null) continue;
            seen++;
            if (!cell.frame.raycastTarget)
            {
                Undo.RecordObject(cell.frame, "Enable Inventory Slot Raycast");
                cell.frame.raycastTarget = true;
                EditorUtility.SetDirty(cell.frame);
                changed++;
            }
            if (cell.button != null && cell.button.targetGraphic == null)
            {
                Undo.RecordObject(cell.button, "Link Inventory Slot Graphic");
                cell.button.targetGraphic = cell.frame;
                EditorUtility.SetDirty(cell.button);
                changed++;
            }
        }

        if (changed > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        Debug.Log("[3A.1] Slot repair: found=" + seen + "/20, changes=" + changed + ", scene=" + scene.name);
        EditorUtility.DisplayDialog("Inventory 3A.1", "Checked " + seen + " item slots. Applied " + changed + " changes. Now enter Play Mode and try selecting a potion.", "OK");
    }

    [MenuItem(MenuRoot + "5 - AUDIT Item Slot Clicks (READ ONLY)")]
    public static void Audit()
    {
        var root = GetInventoryRoot(SceneManager.GetActiveScene());
        if (root == null)
        {
            Debug.LogWarning("[3A.1] No MS3A_InventoryCanvas found in active Scene.");
            return;
        }
        int slots = 0, clickable = 0;
        foreach (var cell in root.GetComponentsInChildren<MS3AInventorySlotUI>(true))
        {
            slots++;
            if (cell.frame != null && cell.frame.raycastTarget &&
                cell.button != null && cell.button.targetGraphic != null && cell.button.interactable)
                clickable++;
        }
        var ui = root.GetComponent<MS3AInventoryUI>();
        var raycaster = root.GetComponent<GraphicRaycaster>();
        var eventSystem = Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
        Debug.Log("[3A.1 AUDIT] Scene=" + SceneManager.GetActiveScene().name +
                  " | slot raycasts=" + clickable + "/" + slots +
                  " | USE ref=" + (ui != null && ui.useButton != null ? "YES" : "NO") +
                  " | EventSystem=" + (eventSystem != null ? "YES" : "NO") +
                  " | Canvas GraphicRaycaster=" + (raycaster != null ? "YES" : "NO") +
                  " | Note: USE is only enabled after selecting a HealConsumable; using at full HP is intentionally rejected.");
    }

    private static GameObject GetInventoryRoot(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return null;
        foreach (var go in scene.GetRootGameObjects())
            if (go.name == "MS3A_InventoryCanvas") return go;
        return null;
    }
}
#endif
