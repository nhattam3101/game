#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Installs only 5A.2: a MainMenu profile card and a SampleScene Victory bank observer.</summary>
public static class MS5A2Installer
{
    private const string SaveRoot = "Assets/MonsterSurvival/Save5A2";
    private const string BackupRoot = SaveRoot + "/Backups";
    private static readonly Color Purple = new Color(.69f, .44f, .91f, 1f);
    private static readonly Color Gold = new Color(.97f, .76f, .46f, 1f);
    private static readonly Color White = new Color(.95f, .93f, .99f, 1f);

    [MenuItem("Tools/MonsterSurvival/5A Save System/2 - INSTALL 5A.2 Persistent Gold (BOTH scenes)")]
    public static void Install()
    {
        if (Application.isPlaying) { Debug.LogError("[5A.2] Stop Play Mode first."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string main = FindScene("MainMenu"), game = FindScene("SampleScene");
        if (string.IsNullOrEmpty(main) || string.IsNullOrEmpty(game))
        {
            EditorUtility.DisplayDialog("5A.2", "Could not find MainMenu and SampleScene assets.", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("5A.2 — Persistent Gold",
            "Install a PROFILE card under MainMenu V3 PAGE_Home and a Victory Gold observer in SampleScene?\n\n" +
            "Backups created once. Existing GameManager / PlayerProgression / HUD / Equipment are untouched.\n" +
            "Victory banks remaining match Gold only; Game Over and early exit do not bank.", "INSTALL", "CANCEL")) return;
        try
        {
            EnsureFolder(BackupRoot);
            BackupOnce(main, "MainMenu_before_5A2.unity");
            BackupOnce(game, "SampleScene_before_5A2.unity");

            Scene mainScene = EditorSceneManager.OpenScene(main, OpenSceneMode.Single);
            GameObject page = FindNamed(mainScene, "PAGE_Home");
            GameObject canvas = FindNamed(mainScene, "MMV3_Canvas");
            if (!page || !canvas || !page.transform.IsChildOf(canvas.transform))
                throw new InvalidOperationException("MainMenu V3 PAGE_Home under MMV3_Canvas was not found. No legacy UI will be modified.");
            AddProfileCard(page.transform);
            EditorSceneManager.MarkSceneDirty(mainScene);
            EditorSceneManager.SaveScene(mainScene);

            Scene gameScene = EditorSceneManager.OpenScene(game, OpenSceneMode.Single);
            GameManager gm = FindComponent<GameManager>(gameScene);
            PlayerProgression progress = FindComponent<PlayerProgression>(gameScene);
            if (!gm || !progress)
                throw new InvalidOperationException("SampleScene needs GameManager and PlayerProgression; no gameplay script was replaced.");
            GameObject root = FindNamed(gameScene, "MS5A2_BankOnVictory");
            if (!root)
            {
                root = new GameObject("MS5A2_BankOnVictory");
                SceneManager.MoveGameObjectToScene(root, gameScene);
            }
            var watcher = root.GetComponent<MS5A2BankOnVictory>();
            if (!watcher) watcher = root.AddComponent<MS5A2BankOnVictory>();
            watcher.gameManager = gm;
            watcher.progression = progress;
            EditorSceneManager.MarkSceneDirty(gameScene);
            EditorSceneManager.SaveScene(gameScene);
            EditorSceneManager.OpenScene(main, OpenSceneMode.Single);
            AssetDatabase.SaveAssets();
            Debug.Log("[5A.2] INSTALLED: MainMenu profile card + SampleScene Victory Gold bridge; original gameplay scripts unchanged.");
            EditorUtility.DisplayDialog("5A.2 installed", "MainMenu is open. Profile 01/02/03 in the top-right.\nRun Play -> map -> Victory, then return to MainMenu to see bank Gold.", "OK");
        }
        catch (Exception ex)
        {
            Debug.LogError("[5A.2] INSTALL STOPPED: " + ex);
            EditorUtility.DisplayDialog("5A.2 installation stopped", ex.Message + "\nBackups are in " + BackupRoot, "OK");
        }
    }

    [MenuItem("Tools/MonsterSurvival/5A Save System/3 - AUDIT 5A.2 OPEN Scene (READ ONLY)")]
    public static void Audit()
    {
        Scene scene = SceneManager.GetActiveScene();
        string name = scene.name;
        if (name == "MainMenu")
        {
            GameObject canvas = FindNamed(scene, "MMV3_Canvas");
            GameObject page = FindNamed(scene, "PAGE_Home");
            GameObject root = FindNamed(scene, "MS5A2_ProfileCard");
            var card = root ? root.GetComponent<MS5A2ProfileWidget>() : null;
            bool pass = canvas && page && root && card && root.transform.IsChildOf(page.transform) &&
                        card.slotTitle && card.bankText && card.statusText && card.previousButton && card.nextButton;
            Debug.Log("[5A.2 QA] MainMenu: " + (pass ? "PASS" : "FAIL") + " | ProfileCard=" + (card ? "YES" : "NO") +
                " | Button pair=" + (card && card.previousButton && card.nextButton ? "YES" : "NO"));
        }
        else if (name == "SampleScene")
        {
            GameObject root = FindNamed(scene, "MS5A2_BankOnVictory");
            var bridge = root ? root.GetComponent<MS5A2BankOnVictory>() : null;
            bool pass = bridge && bridge.gameManager && bridge.progression && bridge.gameManager.victoryPanel &&
                        bridge.gameManager.gameOverPanel;
            Debug.Log("[5A.2 QA] SampleScene: " + (pass ? "PASS" : "FAIL") +
                " | VictoryWatcher=" + (bridge ? "YES" : "NO") +
                " | GameManager=" + (bridge && bridge.gameManager ? "YES" : "NO") +
                " | PlayerProgression=" + (bridge && bridge.progression ? "YES" : "NO"));
        }
        else Debug.LogWarning("[5A.2 QA] Open MainMenu or SampleScene before auditing.");
        int bank, rev;
        string status;
        bool loaded = MS5A2GoldBank.TryGetBalance(MS5A2GoldBank.SelectedSlot, out bank, out rev, out status);
        Debug.Log("[5A.2 QA] Selected slot=" + MS5A2GoldBank.SelectedSlot +
                  " | bank=" + (loaded ? bank.ToString() : "INVALID") + " | revision=" + rev + " | " + status +
                  " | Manual Victory/GameOver tests required.");
    }

    [MenuItem("Tools/MonsterSurvival/5A Save System/4 - REMOVE 5A.2 Scene UI and Observer (BOTH scenes)")]
    public static void Remove()
    {
        if (Application.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!EditorUtility.DisplayDialog("Remove 5A.2", "Remove only the 5A.2 profile card and Victory watcher?\n\nPersistent JSON saves will NOT be deleted.", "REMOVE", "CANCEL")) return;
        string main = FindScene("MainMenu"), game = FindScene("SampleScene");
        if (string.IsNullOrEmpty(main) || string.IsNullOrEmpty(game)) return;
        Scene mainScene = EditorSceneManager.OpenScene(main, OpenSceneMode.Single);
        GameObject card = FindNamed(mainScene, "MS5A2_ProfileCard");
        if (card) UnityEngine.Object.DestroyImmediate(card);
        EditorSceneManager.MarkSceneDirty(mainScene); EditorSceneManager.SaveScene(mainScene);
        Scene gameScene = EditorSceneManager.OpenScene(game, OpenSceneMode.Single);
        GameObject watcher = FindNamed(gameScene, "MS5A2_BankOnVictory");
        if (watcher && watcher.GetComponent<MS5A2BankOnVictory>()) UnityEngine.Object.DestroyImmediate(watcher);
        EditorSceneManager.MarkSceneDirty(gameScene); EditorSceneManager.SaveScene(gameScene);
        EditorSceneManager.OpenScene(main, OpenSceneMode.Single);
        Debug.Log("[5A.2] Removed 5A.2 Scene additions only. Main slot JSON files untouched.");
    }

    [MenuItem("Tools/MonsterSurvival/5A Save System/5 - RUN 5A.2 ISOLATED Gold QA (QA SLOT ONLY)")]
    public static void IsolatedGoldQA()
    {
        if (Application.isPlaying) { Debug.LogWarning("[5A.2 QA] Run outside Play Mode."); return; }
        // Separate from 5A.1's qa_slot_03, and NEVER use a real slot.
        const int qaSlot = 2;
        int before, oldRevision;
        string status;
        if (!MS5A2GoldBank.TryGetBalance(qaSlot, out before, out oldRevision, out status, true))
        {
            Debug.LogError("[5A.2 QA] Cannot read isolated slot: " + status);
            return;
        }
        int after;
        string detail;
        if (!MS5A2GoldBank.TryDeposit(qaSlot, 17, out after, out detail, true))
        {
            Debug.LogError("[5A.2 QA] FAIL deposit: " + detail);
            return;
        }
        int loaded, revision;
        string loadStatus;
        bool pass = MS5A2GoldBank.TryGetBalance(qaSlot, out loaded, out revision, out loadStatus, true) &&
                    (long)loaded == (long)before + 17 && revision == oldRevision + 1;
        Debug.Log("[5A.2 QA] " + (pass ? "PASS" : "FAIL") + " QA-slot-02 Gold deposit/roundtrip: " +
                  before + " +17 => " + after + " / reloaded=" + loaded + " / rev=" + revision +
                  ". All real profile slots untouched. " + detail);
    }

    private static void AddProfileCard(Transform home)
    {
        Transform old = home.Find("MS5A2_ProfileCard");
        if (old)
        {
            var existingWidget = old.GetComponent<MS5A2ProfileWidget>();
            if (existingWidget && existingWidget.slotTitle && existingWidget.bankText && existingWidget.statusText && existingWidget.previousButton && existingWidget.nextButton)
            {
                Debug.Log("[5A.2] Existing ProfileCard kept, not duplicated.");
                return;
            }
            throw new InvalidOperationException("An incomplete MS5A2_ProfileCard already exists; fix manually before reinstalling.");
        }
        RectTransform root = Rect("MS5A2_ProfileCard", home);
        root.anchorMin = root.anchorMax = new Vector2(1f, 1f);
        root.pivot = new Vector2(1f, 1f);
        root.anchoredPosition = new Vector2(-36f, -39f);
        root.sizeDelta = new Vector2(392f, 129f);
        Image bg = root.gameObject.AddComponent<Image>();
        bg.color = new Color(.043f, .035f, .082f, .93f);
        bg.raycastTarget = false;
        var widget = root.gameObject.AddComponent<MS5A2ProfileWidget>();
        RectTransform line = Rect("GoldAccent", root);
        Fill(line, new Vector2(0f, .97f), new Vector2(1f, 1f));
        Image stripe = line.gameObject.AddComponent<Image>(); stripe.color = Gold; stripe.raycastTarget = false;
        widget.slotTitle = Text("PROFILE 01 / 03", root, new Vector2(.21f,.67f), new Vector2(.79f,.94f), 19, White);
        widget.bankText = Text("BANK GOLD    0", root, new Vector2(.10f,.36f), new Vector2(.90f,.68f), 26, Gold);
        widget.statusText = Text("NEW PROFILE", root, new Vector2(.10f,.07f), new Vector2(.90f,.31f), 14, Purple);
        widget.previousButton = NavButton("BTN_PreviousProfile", "<", root, new Vector2(.035f,.62f), new Vector2(.18f,.95f));
        widget.nextButton = NavButton("BTN_NextProfile", ">", root, new Vector2(.82f,.62f), new Vector2(.965f,.95f));
    }

    private static Button NavButton(string name, string text, Transform parent, Vector2 min, Vector2 max)
    {
        RectTransform rect = Rect(name, parent); Fill(rect, min, max);
        Image im = rect.gameObject.AddComponent<Image>();
        im.color = new Color(.19f,.13f,.31f,.95f); im.raycastTarget = true;
        Button button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = im;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f,.85f,.67f,1f);
        colors.pressedColor = new Color(.68f,.54f,.85f,1f);
        button.colors = colors;
        Text(text, rect, Vector2.zero, Vector2.one, 27, White);
        return button;
    }

    private static TMP_Text Text(string value, Transform parent, Vector2 min, Vector2 max, int size, Color color)
    {
        RectTransform rect = Rect("TXT_" + value.Replace(' ', '_'), parent);
        Fill(rect, min, max);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value; text.fontSize = size; text.color = color;
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        text.enableAutoSizing = true; text.fontSizeMin = Mathf.Max(11, size - 7); text.fontSizeMax = size;
        if (TMP_Settings.defaultFontAsset) text.font = TMP_Settings.defaultFontAsset;
        return text;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static void Fill(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
    }

    private static GameObject FindNamed(Scene scene, string name)
    {
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            if (go.name == name) return go;
            foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child.gameObject;
        }
        return null;
    }
    private static T FindComponent<T>(Scene scene) where T : Component
    {
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            T c = go.GetComponentInChildren<T>(true);
            if (c) return c;
        }
        return null;
    }
    private static string FindScene(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Scene " + name))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path).Equals(name, StringComparison.OrdinalIgnoreCase) &&
                !path.Contains("/Backups/")) return path;
        }
        return null;
    }
    private static void BackupOnce(string scenePath, string backupName)
    {
        string backup = BackupRoot + "/" + backupName;
        if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(backup) && !AssetDatabase.CopyAsset(scenePath, backup))
            throw new IOException("Could not create Scene backup: " + backup);
        AssetDatabase.ImportAsset(backup);
    }
    private static void EnsureFolder(string path)
    {
        string current = "Assets";
        string[] parts = path.Split('/');
        for (int i=1; i<parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif
