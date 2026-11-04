#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Stage 6A.4 - read-only Scene validator + separate opt-in responsive component.
// Intentionally does NOT disable panels, replace Canvas, or alter gameplay scripts.
public sealed class HUD6A4ValidatorWindow : EditorWindow
{
    private enum Severity { Pass, Warning, Error, Information }

    private struct Finding
    {
        public Severity severity;
        public string message;
        public Finding(Severity level, string text) { severity = level; message = text; }
    }

    private readonly List<Finding> findings = new List<Finding>();
    private Vector2 scroll;

    [MenuItem("Tools/MonsterSurvival/6A.4 HUD QA/1 - Open Validator (READ ONLY)")]
    public static void Open()
    {
        HUD6A4ValidatorWindow w = GetWindow<HUD6A4ValidatorWindow>("HUD 6A.4 Validator");
        w.minSize = new Vector2(480, 440);
        w.RunScan();
        w.Show();
    }

    [MenuItem("Tools/MonsterSurvival/6A.4 HUD QA/2 - Install Responsive Corners (OPEN Scene)")]
    public static void Install()
    {
        if (!CanEdit()) return;
        GameObject hud = FindHUD();
        if (hud == null)
        {
            EditorUtility.DisplayDialog("6A.4", "Khong tim thay HUD6A2_Minimal trong Scene. Cai 6A.2 va 6A.3 truoc.", "OK");
            return;
        }
        Transform left = hud.transform.Find("PlayerStatus");
        Transform right = hud.transform.Find("GameStatus_6A3");
        if (left == null || right == null)
        {
            EditorUtility.DisplayDialog("6A.4", "Can PlayerStatus va GameStatus_6A3 truoc khi cai responsive.", "OK");
            return;
        }
        if (hud.GetComponent<HUD6A4ResponsiveCorners>() != null)
        {
            EditorUtility.DisplayDialog("6A.4", "Responsive 6A.4 da duoc gan, khong them trung.", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("6A.4 - Confirm", "Chi gan 1 component vao HUD6A2_Minimal. KHONG sua script gameplay, KHONG tat UI cu. Tiep tuc?", "Install", "Cancel"))
            return;
        HUD6A4ResponsiveCorners layout = Undo.AddComponent<HUD6A4ResponsiveCorners>(hud);
        layout.playerStatus = left.GetComponent<RectTransform>();
        layout.gameStatus6A3 = right.GetComponent<RectTransform>();
        EditorUtility.SetDirty(hud);
        EditorSceneManager.MarkSceneDirty(hud.scene);
        Selection.activeGameObject = hud;
        EditorUtility.DisplayDialog("6A.4", "Da gan Responsive Corners. Luu Ctrl+S va chay Play Mode de kiem tra cac ty le. Khong co panel nao bi tat tu dong.", "OK");
    }

    [MenuItem("Tools/MonsterSurvival/6A.4 HUD QA/3 - Remove Responsive Corners (OPEN Scene)")]
    public static void Remove()
    {
        if (!CanEdit()) return;
        GameObject hud = FindHUD();
        if (hud == null) return;
        HUD6A4ResponsiveCorners current = hud.GetComponent<HUD6A4ResponsiveCorners>();
        if (current == null)
        {
            EditorUtility.DisplayDialog("6A.4", "Chua gan Responsive 6A.4. Khong co gi can go.", "OK");
            return;
        }
        Undo.DestroyObjectImmediate(current);
        EditorSceneManager.MarkSceneDirty(hud.scene);
        EditorUtility.DisplayDialog("6A.4", "Da go component 6A.4. HUD 6A.2 va 6A.3 van nguyen ven.", "OK");
    }

    private static bool CanEdit()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("6A.4", "Dung Play Mode truoc khi sua Scene.", "OK");
            return false;
        }
        return true;
    }

    private static GameObject FindHUD()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded) return null;
        foreach (GameObject go in scene.GetRootGameObjects())
            if (go.name == "HUD6A2_Minimal") return go;
        return null;
    }

    private static Transform FindInScene(string name)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded) return null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == name) return root.transform;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in all)
                if (t.name == name) return t;
        }
        return null;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("MONSTERSURVIVAL - HUD 6A.4", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Quet Scene KHONG sua gi. Cai responsive bang menu 2; go bang menu 3. Game View phai thu thu cong o nhieu ti le.", MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Quet lai Scene", GUILayout.Height(30))) RunScan();
            if (GUILayout.Button("Xuat bao cao .txt", GUILayout.Height(30))) ExportReport();
        }
        GUILayout.Space(8);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (Finding f in findings)
        {
            MessageType type = f.severity == Severity.Error ? MessageType.Error :
                f.severity == Severity.Warning ? MessageType.Warning : MessageType.Info;
            string prefix = f.severity == Severity.Pass ? "[OK] " :
                f.severity == Severity.Warning ? "[CHECK] " :
                f.severity == Severity.Error ? "[ERROR] " : "[INFO] ";
            EditorGUILayout.HelpBox(prefix + f.message, type);
        }
        EditorGUILayout.EndScrollView();
        GUILayout.Space(8);
        EditorGUILayout.LabelField("Ty le can thu (Game View)", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("1920x1080 (16:9) | 1920x1200 (16:10) | 1440x1080 (4:3)");
        EditorGUILayout.LabelField("2560x1080 (21:9) | 1280x720 | tuy chon 1080x1920 (doc)");
        EditorGUILayout.HelpBox("Kiem thu Play Mode: HP, EXP, Gold, Wave, Pause -> Settings, Level Up -> SkillPanel, Victory/Retry va 2 Boss.", MessageType.None);
    }

    private void Add(Severity level, string message) { findings.Add(new Finding(level, message)); }

    private void RunScan()
    {
        findings.Clear();
        Scene scene = SceneManager.GetActiveScene();
        Add(Severity.Information, "Scene: " + (scene.IsValid() ? scene.name : "INVALID") +
            " | Play Mode: " + EditorApplication.isPlaying);

        GameObject hud = FindHUD();
        if (hud == null)
        {
            Add(Severity.Error, "HUD6A2_Minimal KHONG co trong Scene dang mo.");
            Repaint();
            return;
        }
        Add(Severity.Pass, "HUD6A2_Minimal ton tai.");
        Canvas canvas = hud.GetComponent<Canvas>();
        CanvasScaler scaler = hud.GetComponent<CanvasScaler>();
        HUD6A2CompactView leftView = hud.GetComponent<HUD6A2CompactView>();
        if (canvas == null) Add(Severity.Error, "HUD6A2_Minimal thieu Canvas.");
        else if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            Add(Severity.Warning, "Canvas khong o Screen Space Overlay; can thu vi tri tren Game View.");
        else Add(Severity.Pass, "Canvas Screen Space Overlay.");
        if (scaler == null) Add(Severity.Error, "Thieu CanvasScaler.");
        else
        {
            if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
                Add(Severity.Warning, "CanvasScaler chua o Scale With Screen Size.");
            else Add(Severity.Pass, "CanvasScaler dung Scale With Screen Size: " +
                scaler.referenceResolution.x + "x" + scaler.referenceResolution.y +
                " / Match=" + scaler.matchWidthOrHeight.ToString("0.00"));
        }
        if (leftView == null) Add(Severity.Error, "Thieu HUD6A2CompactView.");
        else
        {
            Add(Severity.Pass, "HUD6A2CompactView duoc giu nguyen.");
            if (leftView.playerHealth == null || leftView.playerProgression == null)
                Add(Severity.Warning, "Player Health/Progression chua gan trong Inspector; View co the tu tim runtime.");
            if (leftView.waveManager == null) Add(Severity.Warning, "WaveManager chua gan cho HUD 6A.2.");
        }
        Transform left = hud.transform.Find("PlayerStatus");
        Transform oldStatus = hud.transform.Find("GameStatus");
        Transform right = hud.transform.Find("GameStatus_6A3");
        if (left == null) Add(Severity.Error, "Thieu PlayerStatus.");
        else CheckCorner(left as RectTransform, "PlayerStatus", false);
        if (right == null) Add(Severity.Error, "Thieu GameStatus_6A3. Cai 6A.3 truoc.");
        else
        {
            CheckCorner(right as RectTransform, "GameStatus_6A3", true);
            HUD6A3GameStatusView st = right.GetComponent<HUD6A3GameStatusView>();
            if (st == null) Add(Severity.Error, "Thieu HUD6A3GameStatusView.");
            else
            {
                if (st.waveManager == null) Add(Severity.Warning, "6A.3 WaveManager chua gan, can kiem tra runtime.");
                if (st.playerProgression == null) Add(Severity.Warning, "6A.3 PlayerProgression chua gan, can kiem tra runtime.");
                if (st.waveSourceText == null && st.timeSourceText == null)
                    Add(Severity.Warning, "6A.3 khong co nguon Wave/Timer Text.");
                else Add(Severity.Pass, "6A.3 da co nguon Wave/Timer.");
            }
        }
        if (oldStatus != null && oldStatus.gameObject.activeSelf)
            Add(Severity.Warning, "GameStatus 6A.2 cu dang bat. Co the trung cum Wave/Gold 6A.3.");
        if (right != null && !right.gameObject.activeSelf)
            Add(Severity.Warning, "GameStatus_6A3 dang tat.");
        HUD6A4ResponsiveCorners responsive = hud.GetComponent<HUD6A4ResponsiveCorners>();
        if (responsive == null) Add(Severity.Information, "6A.4 Responsive Corners CHUA cai. Menu 2 de gan neu muon.");
        else Add(Severity.Pass, "6A.4 Responsive Corners da gan vao dung HUD root.");

        // Read-only reminder for old TMP draw calls; never disable GameObjects or text source.
        string[] oldTexts = { "HealthText", "GoldText", "ExpText", "WaveText", "Time" };
        foreach (string label in oldTexts)
        {
            Transform found = FindInScene(label);
            if (found == null || found.IsChildOf(hud.transform)) continue;
            TMP_Text t = found.GetComponent<TMP_Text>();
            if (t != null && t.enabled && t.gameObject.activeInHierarchy)
                Add(Severity.Warning, "Text cu '" + label + "' dang hien va co the trung HUD moi. Chi tat component TMP sau khi test.");
        }
        Transform pause = FindInScene("PausePanel");
        Transform skill = FindInScene("SkillPanel");
        Transform settings = FindInScene("SettingsWindow");
        Transform gameOver = FindInScene("GameOverPanel");
        Transform victory = FindInScene("VictoryPanel");
        if (pause != null && skill != null && pause.gameObject.activeInHierarchy && skill.gameObject.activeInHierarchy)
            Add(Severity.Warning, "PausePanel va SkillPanel dang hien dong thoi, co the che nhau.");
        if (settings != null && skill != null && settings.gameObject.activeInHierarchy && skill.gameObject.activeInHierarchy)
            Add(Severity.Warning, "SettingsWindow va SkillPanel dang hien dong thoi.");
        if (gameOver != null && victory != null && gameOver.gameObject.activeInHierarchy && victory.gameObject.activeInHierarchy)
            Add(Severity.Warning, "GameOverPanel va VictoryPanel dang hien dong thoi.");
        Add(Severity.Information, "Khong tu tat Pause, Settings, Skill, Boss HP hay old Canvas.");
        Add(Severity.Information, "Game View hien tai: " + Screen.width + "x" + Screen.height +
            " (Edit Mode khong dai dien cho moi ty le; can thu Play Mode).");
        Repaint();
    }

    private void CheckCorner(RectTransform t, string title, bool topRight)
    {
        if (t == null) { Add(Severity.Error, title + " khong co RectTransform."); return; }
        Vector2 expected = topRight ? Vector2.one : new Vector2(0f, 1f);
        if (Vector2.Distance(t.anchorMin, expected) > 0.01f ||
            Vector2.Distance(t.anchorMax, expected) > 0.01f)
            Add(Severity.Warning, title + " anchor chua la " + (topRight ? "Top Right" : "Top Left") + ".");
        else Add(Severity.Pass, title + " neo dung " + (topRight ? "Top Right" : "Top Left") +
            ", kich thuoc " + t.rect.width.ToString("0") + "x" + t.rect.height.ToString("0") + ".");
    }

    private void ExportReport()
    {
        RunScan();
        string path = EditorUtility.SaveFilePanel("Save HUD 6A.4 report", Application.dataPath,
            "HUD6A4_Audit_" + SceneManager.GetActiveScene().name, "txt");
        if (string.IsNullOrEmpty(path)) return;
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("MONSTERSURVIVAL - HUD 6A.4 READ-ONLY AUDIT");
        sb.AppendLine("Time (local): " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        foreach (Finding f in findings)
            sb.AppendLine("[" + f.severity.ToString().ToUpperInvariant() + "] " + f.message);
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        EditorUtility.RevealInFinder(path);
    }
}
#endif
