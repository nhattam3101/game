#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Stage 6D — non-destructive QA for the user's existing SampleScene and MainMenu.
// All auditing is READ ONLY; changes require pressing an explicitly labeled button.
public sealed class MS6DUIAuditWindow : EditorWindow
{
    private enum Severity { Pass, Info, Warning, Error }
    private sealed class Finding
    {
        public Severity severity;
        public string note;
        public Finding(Severity severity, string note) { this.severity = severity; this.note = note; }
    }
    private readonly List<Finding> findings = new List<Finding>();
    private Vector2 scroll;
    private string lastScene = "";

    [MenuItem("Tools/MonsterSurvival/6D UI QA/1 - OPEN UI AUDIT (READ ONLY)")]
    public static void OpenWindow()
    {
        var w = GetWindow<MS6DUIAuditWindow>("MonsterSurvival 6D QA");
        w.minSize = new Vector2(530, 390);
        w.Scan();
        w.Show();
    }

    [MenuItem("Tools/MonsterSurvival/6D UI QA/2 - SCAN OPEN Scene (READ ONLY)")]
    public static void ScanFromMenu()
    {
        OpenWindow();
    }

    [MenuItem("Tools/MonsterSurvival/6D UI QA/3 - SET default gameplay panels OFF (CONFIRM)")]
    public static void FixFromMenu()
    {
        if (ConfirmSetDefaults()) OpenWindow();
    }

    [MenuItem("Tools/MonsterSurvival/6D UI QA/4 - ADD runtime monitor (OPEN Scene)")]
    public static void InstallMonitorMenu()
    {
        InstallMonitor();
        OpenWindow();
    }

    [MenuItem("Tools/MonsterSurvival/6D UI QA/5 - REMOVE runtime monitor (OPEN Scene)")]
    public static void RemoveMonitorMenu()
    {
        RemoveMonitor();
        OpenWindow();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("MONSTERSURVIVAL · 6D UI QA", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Audit không sửa Scene. Nút sửa trạng thái / thêm monitor cần xác nhận riêng. Kiểm tra thủ công trong Play Mode vẫn bắt buộc.", MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Quét lại (READ ONLY)", GUILayout.Height(28))) Scan();
            if (GUILayout.Button("Lưu báo cáo .txt", GUILayout.Height(28))) Export();
        }
        EditorGUILayout.LabelField("Scene", string.IsNullOrEmpty(lastScene) ? "Chưa quét" : lastScene);
        int errors = 0, warnings = 0;
        foreach (Finding f in findings)
        {
            if (f.severity == Severity.Error) errors++;
            if (f.severity == Severity.Warning) warnings++;
        }
        EditorGUILayout.LabelField("Kết quả", errors + " lỗi cấu trúc · " + warnings + " cảnh báo · " + findings.Count + " mục");
        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (Finding f in findings)
        {
            string mark = f.severity == Severity.Pass ? "PASS" : f.severity == Severity.Warning ? "WARN" : f.severity == Severity.Error ? "ERROR" : "INFO";
            EditorGUILayout.LabelField("[" + mark + "] " + f.note, EditorStyles.wordWrappedLabel);
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Hành động tùy chọn (không chạy trong Play Mode)", EditorStyles.boldLabel);
        if (GUILayout.Button("Đặt Pause / Skill / Victory / GameOver về OFF trong Edit Mode"))
        {
            if (ConfirmSetDefaults()) Scan();
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Thêm Runtime Monitor (chỉ ghi log)")) { InstallMonitor(); Scan(); }
            if (GUILayout.Button("Gỡ Runtime Monitor")) { RemoveMonitor(); Scan(); }
        }
        EditorGUILayout.HelpBox("Không tự tắt Settings, không sửa script Pause, không thay timeScale, không thay nút/listeners. Hoàn tác thao tác sửa bằng Ctrl+Z trước khi lưu Scene hoặc dùng bản sao lưu.", MessageType.None);
    }

    private void Add(Severity s, string message) { findings.Add(new Finding(s, message)); }

    private void Scan()
    {
        findings.Clear();
        Scene scene = SceneManager.GetActiveScene();
        lastScene = scene.IsValid() ? scene.name + " (" + scene.path + ")" : "No active scene";
        if (!scene.IsValid()) { Add(Severity.Error, "Chưa có Scene hợp lệ."); Repaint(); return; }
        bool gameplay = scene.name.Equals("SampleScene", StringComparison.OrdinalIgnoreCase);
        bool menu = scene.name.Equals("MainMenu", StringComparison.OrdinalIgnoreCase);
        if (!gameplay && !menu)
            Add(Severity.Warning, "Đang mở Scene khác SampleScene/MainMenu; một số kiểm tra không áp dụng.");

        EventSystem es = FindComponent<EventSystem>(scene);
        Add(es ? Severity.Pass : Severity.Error, es ? "EventSystem tồn tại" : "Không tìm thấy EventSystem; UI Button có thể không nhận click.");

        if (gameplay) ScanGameplay(scene);
        if (menu) ScanMainMenu(scene);
        Add(Severity.Info, "Báo cáo Edit Mode không khẳng định hành vi Pause / Settings / Level Up thực tế. Hãy kiểm thử Play Mode.");
        Repaint();
    }

    private void ScanGameplay(Scene scene)
    {
        Canvas legacyCanvas = FindCanvasNamed(scene, "Canvas");
        GameObject pause = FindNamed(scene, "PausePanel");
        GameObject skill = FindNamed(scene, "SkillPanel");
        GameObject victory = FindNamed(scene, "VictoryPanel");
        GameObject death = FindNamed(scene, "GameOverPanel");
        Add(legacyCanvas ? Severity.Pass : Severity.Error,
            legacyCanvas ? "Gameplay Canvas tồn tại" : "Thiếu Canvas gameplay.");
        GameObject[] panels = { pause, skill, victory, death };
        string[] labels = { "PausePanel", "SkillPanel", "VictoryPanel", "GameOverPanel" };
        int active = 0;
        for (int i = 0; i < panels.Length; i++)
        {
            GameObject p = panels[i];
            if (!p) { Add(Severity.Warning, labels[i] + " không tìm thấy trong Scene."); continue; }
            if (p.activeSelf) active++;
            Add(p.activeSelf ? Severity.Warning : Severity.Pass,
                labels[i] + (p.activeSelf ? " đang ON khi lưu Scene (có thể chồng trong Edit Mode)." : " mặc định OFF trong Scene."));
        }
        if (active > 1) Add(Severity.Warning, active + " modal đang ON khi mở Scene; cần kiểm tra sau lúc Play, không kết luận đây là lỗi runtime.");

        MonoBehaviour gm = FindBehaviour(scene, "GameManager");
        MonoBehaviour sm = FindBehaviour(scene, "SkillManager");
        MonoBehaviour wm = FindBehaviour(scene, "WaveManager");
        Add(gm ? Severity.Pass : Severity.Error, gm ? "GameManager hiện diện." : "Thiếu GameManager.");
        Add(sm ? Severity.Pass : Severity.Error, sm ? "SkillManager hiện diện." : "Thiếu SkillManager.");
        Add(wm ? Severity.Pass : Severity.Warning, wm ? "WaveManager hiện diện." : "Không tìm thấy WaveManager.");
        if (gm != null)
        {
            CheckObjectReference(gm, "gameOverPanel", "GameManager.gameOverPanel");
            CheckObjectReference(gm, "victoryPanel", "GameManager.victoryPanel");
        }
        if (sm != null)
        {
            CheckObjectReference(sm, "skillPanel", "SkillManager.skillPanel");
            CheckArray(sm, "skillButtons", 3);
            CheckArray(sm, "skillNames", 3);
            CheckArray(sm, "skillDescriptions", 3);
        }
        GameObject hud = FindNamed(scene, "HUD6A2_Minimal");
        Add(hud ? Severity.Pass : Severity.Warning,
            hud ? "HUD6A2_Minimal có trong Scene." : "Không thấy HUD6A2_Minimal — kiểm tra HUD hiện tại.");
        if (hud)
        {
            CanvasScaler scaler = hud.GetComponent<CanvasScaler>();
            if (!scaler) scaler = hud.GetComponentInChildren<CanvasScaler>(true);
            Add(scaler && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize ? Severity.Pass : Severity.Warning,
                scaler ? "CanvasScaler HUD: " + scaler.uiScaleMode + ", reference=" + scaler.referenceResolution : "HUD thiếu CanvasScaler.");
            GameObject status = FindWithin(hud.transform, "GameStatus_6A3");
            Add(status && status.activeInHierarchy ? Severity.Pass : Severity.Info,
                status ? "Cụm GameStatus_6A3 " + (status.activeInHierarchy ? "ON" : "OFF") : "Cụm GameStatus_6A3 không thấy (có thể dùng thiết kế khác).");
        }
        if (legacyCanvas)
        {
            bool themed = legacyCanvas.GetComponent("MS6CStyleSnapshot") != null;
            bool guarded = legacyCanvas.GetComponent("MS6CModalCoordinator") != null;
            Add(themed ? Severity.Pass : Severity.Warning, themed ? "6C StyleSnapshot hiện diện." : "Chưa phát hiện 6C StyleSnapshot trên Canvas.");
            Add(guarded ? Severity.Pass : Severity.Warning, guarded ? "6C ModalCoordinator hiện diện." : "Chưa phát hiện 6C ModalCoordinator (Pause có thể chồng lên Settings).");
        }
        GameObject monitor = FindNamed(scene, "Canvas");
        bool hasRuntime = monitor && monitor.GetComponent<MS6DRuntimeMonitor>();
        Add(hasRuntime ? Severity.Pass : Severity.Info, hasRuntime ? "Runtime Monitor đang được gắn — chỉ ghi log." : "Chưa gắn Runtime Monitor; tùy chọn nếu cần kiểm thử modal trong Play Mode.");
        Add(Severity.Info, "Cần kiểm tra tay tại 16:9, 16:10, 4:3, 21:9; API Editor không xác nhận mọi kích thước Game View.");
    }

    private void ScanMainMenu(Scene scene)
    {
        GameObject canvas = FindNamed(scene, "MMV3_Canvas");
        Add(canvas && canvas.activeInHierarchy ? Severity.Pass : Severity.Error,
            canvas ? "MMV3_Canvas " + (canvas.activeInHierarchy ? "ON" : "OFF") : "Không tìm thấy MMV3_Canvas.");
        GameObject root = FindNamed(scene, "MMV3_Root");
        Add(root ? Severity.Pass : Severity.Error, root ? "MMV3_Root hiện diện" : "Thiếu MMV3_Root");
        string[] pages = { "PAGE_Home", "PAGE_SelectMap", "PAGE_Settings" };
        for (int i = 0; i < pages.Length; i++)
        {
            GameObject page = FindNamed(scene, pages[i]);
            Add(page ? Severity.Pass : Severity.Error, pages[i] + (page ? " hiện diện." : " không thấy."));
        }
        bool old = FindNamed(scene, "MainMenuCanvas") != null;
        Add(Severity.Info, old ? "MainMenuCanvas cũ vẫn được giữ để backup, không cần xóa." : "Không thấy MainMenuCanvas cũ.");
        Add(Severity.Info, "Dùng Play Mode thử: Home → Map → Back → Settings → Back → Start Adventure → Gameplay.");
    }

    private void CheckObjectReference(MonoBehaviour b, string property, string label)
    {
        SerializedObject so = new SerializedObject(b);
        SerializedProperty p = so.FindProperty(property);
        if (p == null) { Add(Severity.Info, label + " không có ở phiên bản script đang dùng."); return; }
        bool exists = p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue != null;
        Add(exists ? Severity.Pass : Severity.Warning, label + (exists ? " đã gắn." : " đang None."));
    }
    private void CheckArray(MonoBehaviour b, string property, int expected)
    {
        SerializedObject so = new SerializedObject(b);
        SerializedProperty p = so.FindProperty(property);
        if (p == null || !p.isArray) { Add(Severity.Warning, "SkillManager." + property + " chưa có array."); return; }
        bool ok = p.arraySize >= expected;
        for (int i = 0; i < expected && i < p.arraySize; i++)
        {
            SerializedProperty item = p.GetArrayElementAtIndex(i);
            if (item.propertyType != SerializedPropertyType.ObjectReference || item.objectReferenceValue == null) ok = false;
        }
        Add(ok ? Severity.Pass : Severity.Error,
            "SkillManager." + property + " = " + p.arraySize + " phần tử; " + (ok ? "3 tham chiếu đầu hợp lệ." : "thiếu phần tử hoặc tham chiếu."));
    }

    private static bool ConfirmSetDefaults()
    {
        if (Application.isPlaying) { EditorUtility.DisplayDialog("6D", "Dừng Play Mode trước khi chỉnh Scene.", "OK"); return false; }
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.name.Equals("SampleScene", StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog("6D", "Hãy mở SampleScene trước.", "OK"); return false;
        }
        if (string.IsNullOrEmpty(scene.path))
        {
            EditorUtility.DisplayDialog("6D", "Hãy lưu Scene ra asset trước khi tiếp tục.", "OK"); return false;
        }
        if (!EditorUtility.DisplayDialog("6D — Scene defaults",
            "Sao lưu SampleScene, sau đó chỉ đặt PausePanel / SkillPanel / VictoryPanel / GameOverPanel về OFF trong Edit Mode. KHÔNG sửa code gameplay hay nút. Tiếp tục?",
            "Sao lưu + Đặt OFF", "Hủy")) return false;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
        const string folder = "Assets/MonsterSurvival/UI6D/Backups";
        EnsureAssetFolder(folder);
        string dest = folder + "/SampleScene_BEFORE_6D_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity";
        if (!AssetDatabase.CopyAsset(scene.path, dest))
        {
            EditorUtility.DisplayDialog("6D", "Không tạo được bản backup; chưa thay đổi Scene.", "OK"); return false;
        }
        int changed = 0;
        foreach (string name in new[] { "PausePanel", "SkillPanel", "VictoryPanel", "GameOverPanel" })
        {
            GameObject p = FindNamed(scene, name);
            if (!p || !p.activeSelf) continue;
            Undo.RecordObject(p, "6D default modal OFF");
            p.SetActive(false);
            changed++;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[6D UI QA] Backed up " + scene.path + " -> " + dest + "; " + changed + " modal root(s) set OFF. Save the scene to keep changes.");
        EditorUtility.DisplayDialog("6D", "Đã sao lưu và đặt " + changed + " panel về OFF. Nhấn Ctrl+S để lưu. Có thể Undo trước khi lưu.", "OK");
        return true;
    }

    private static void InstallMonitor()
    {
        if (Application.isPlaying) { EditorUtility.DisplayDialog("6D", "Dừng Play Mode.", "OK"); return; }
        Scene scene = SceneManager.GetActiveScene();
        GameObject canvas = FindNamed(scene, "Canvas");
        if (!canvas || !canvas.GetComponent<Canvas>())
        {
            EditorUtility.DisplayDialog("6D", "Hãy mở SampleScene và kiểm tra GameObject Canvas.", "OK"); return;
        }
        MS6DRuntimeMonitor monitor = canvas.GetComponent<MS6DRuntimeMonitor>();
        if (monitor != null) { Debug.Log("[6D] Runtime monitor already installed."); return; }
        monitor = Undo.AddComponent<MS6DRuntimeMonitor>(canvas);
        monitor.pausePanel = FindNamed(scene, "PausePanel");
        monitor.skillPanel = FindNamed(scene, "SkillPanel");
        monitor.victoryPanel = FindNamed(scene, "VictoryPanel");
        monitor.gameOverPanel = FindNamed(scene, "GameOverPanel");
        EditorUtility.SetDirty(monitor);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[6D] Runtime monitor installed on Canvas (logs only). Remember Ctrl+S.");
    }

    private static void RemoveMonitor()
    {
        if (Application.isPlaying) { EditorUtility.DisplayDialog("6D", "Dừng Play Mode.", "OK"); return; }
        Scene scene = SceneManager.GetActiveScene();
        GameObject canvas = FindNamed(scene, "Canvas");
        if (!canvas) return;
        MS6DRuntimeMonitor monitor = canvas.GetComponent<MS6DRuntimeMonitor>();
        if (monitor == null) { Debug.Log("[6D] Runtime monitor not found."); return; }
        Undo.DestroyObjectImmediate(monitor);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[6D] Runtime monitor removed. Remember Ctrl+S.");
    }

    private void Export()
    {
        if (findings.Count == 0) Scan();
        string path = EditorUtility.SaveFilePanel("Lưu báo cáo 6D", Application.dataPath, "MonsterSurvival_6D_UI_QA", "txt");
        if (string.IsNullOrEmpty(path)) return;
        var sb = new StringBuilder();
        sb.AppendLine("MonsterSurvival UI QA 6D | " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine("Scene: " + lastScene);
        sb.AppendLine("READ ONLY audit. Play Mode tests must be verified manually.");
        sb.AppendLine(new string('-', 70));
        foreach (Finding f in findings) sb.AppendLine("[" + f.severity.ToString().ToUpperInvariant() + "] " + f.note);
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        Debug.Log("[6D] Report saved: " + path);
    }

    private static void EnsureAssetFolder(string path)
    {
        string[] segments = path.Split('/');
        string cur = segments[0];
        for (int i = 1; i < segments.Length; i++)
        {
            string next = cur + "/" + segments[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, segments[i]);
            cur = next;
        }
    }

    private static Canvas FindCanvasNamed(Scene scene, string name)
    {
        GameObject go = FindNamed(scene, name);
        return go ? go.GetComponent<Canvas>() : null;
    }
    private static GameObject FindWithin(Transform t, string name)
    {
        if (t == null) return null;
        foreach (Transform x in t.GetComponentsInChildren<Transform>(true))
            if (x.name.Equals(name, StringComparison.OrdinalIgnoreCase)) return x.gameObject;
        return null;
    }
    private static GameObject FindNamed(Scene scene, string name)
    {
        if (!scene.IsValid()) return null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            GameObject found = FindWithin(root.transform, name);
            if (found) return found;
        }
        return null;
    }
    private static T FindComponent<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T c = root.GetComponentInChildren<T>(true);
            if (c) return c;
        }
        return null;
    }
    private static MonoBehaviour FindBehaviour(Scene scene, string typeName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (MonoBehaviour c in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (c && c.GetType().Name == typeName) return c;
        return null;
    }
}
#endif
