#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// QA deliberately uses qa_slot_03.json. It never writes a real save slot.
public sealed class MS5A1SaveQAWindow : EditorWindow
{
    private Vector2 scroll;
    private string report = "Chưa chạy kiểm thử. Dữ liệu Main slot chỉ được đọc.";

    [MenuItem("Tools/MonsterSurvival/5A Save System/1 - OPEN 5A.1 Save Core QA")]
    public static void ShowWindow()
    {
        var w = GetWindow<MS5A1SaveQAWindow>("5A.1 Save QA");
        w.minSize = new Vector2(620f, 440f);
        w.Show();
    }

    [MenuItem("Tools/MonsterSurvival/5A Save System/2 - READ ONLY Audit Save Slots")]
    public static void AuditToConsole()
    {
        Debug.Log(BuildAudit());
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("MONSTERSURVIVAL — 5A.1 SAVE DATA CORE", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Mục đích: thử hệ thống ghi/đọc file JSON có backup. Không liên kết Gold, Inventory hay Equipment trong phiên bản này.", MessageType.Info);
        EditorGUILayout.LabelField("Persistent data path:", EditorStyles.miniBoldLabel);
        EditorGUILayout.SelectableLabel(MS5A1SaveService.SaveDirectory,
            EditorStyles.textField, GUILayout.Height(32));
        EditorGUILayout.Space(5);

        if (GUILayout.Button("READ ONLY — kiểm tra Main slot 1–3", GUILayout.Height(31)))
            report = BuildAudit();
        if (GUILayout.Button("RUN ISOLATED QA — chỉ ghi qa_slot_03", GUILayout.Height(36)))
        {
            if (EditorUtility.DisplayDialog("5A.1 Save QA — thử nghiệm cách ly",
                "Chỉ thao tác trên qa_slot_03.json và qa_slot_03.json.bak trong persistentDataPath. " +
                "KHÔNG tạo, thay hay xoá Main slot 1–3. Chạy thử?", "Chạy QA", "Huỷ"))
                report = RunIsolatedTest();
        }
        if (GUILayout.Button("OPEN persistentDataPath folder", GUILayout.Height(27)))
        {
            string dir = MS5A1SaveService.SaveDirectory;
            if (Directory.Exists(dir)) EditorUtility.RevealInFinder(dir);
            else EditorUtility.DisplayDialog("5A.1", "Chưa có thư mục dữ liệu. Hãy chạy QA để tạo qa_slot_03.", "OK");
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("REPORT", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
        if (GUILayout.Button("COPY REPORT", GUILayout.Height(25)))
            EditorGUIUtility.systemCopyBuffer = report;
    }

    private static string BuildAudit()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("5A.1 READ ONLY SAVE AUDIT");
        sb.AppendLine("UTC: " + DateTime.UtcNow.ToString("o"));
        sb.AppendLine("Folder: " + MS5A1SaveService.SaveDirectory);
        for (int slot = 1; slot <= 3; slot++)
        {
            MS5A1SaveData data;
            string message;
            bool recovered;
            bool ok = MS5A1SaveService.TryLoad(slot, out data, out message, out recovered);
            sb.AppendLine("Main slot " + slot + ": " + (ok ? "VALID" : "EMPTY/INVALID") +
                          ", backup recovery=" + recovered + ", " + message);
            if (ok)
                sb.AppendLine("   revision=" + data.revision +
                              ", stored bankGold=" + data.bankGold + " (chưa sync gameplay)");
        }
        sb.AppendLine("Main slot audit KHÔNG ghi file.");
        return sb.ToString();
    }

    private static string RunIsolatedTest()
    {
        const int qaSlot = 3;
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("MONSTERSURVIVAL 5A.1 — ISOLATED QA");
        sb.AppendLine("UTC: " + DateTime.UtcNow.ToString("o"));
        sb.AppendLine("Only QA path: " + MS5A1SaveService.GetSavePath(qaSlot, true));
        string path = MS5A1SaveService.GetSavePath(qaSlot, true);
        byte[] originalMain = null;
        bool tampered = false;
        int pass = 0;
        int total = 5;
        try
        {
            MS5A1SaveData data;
            string message;
            bool recovered;
            if (!MS5A1SaveService.TryLoad(qaSlot, out data, out message, out recovered, true))
            {
                if (File.Exists(path) || File.Exists(MS5A1SaveService.GetBackupPath(qaSlot, true)))
                    throw new InvalidOperationException("QA slot cũ đang hỏng/khác phiên bản; không ghi đè: " + message);
                data = MS5A1SaveService.CreateFresh(qaSlot, true);
            }
            sb.AppendLine("PASS 1/5 — QA slot load/create OK"); pass++;

            MS5A1SaveData saved;
            if (!MS5A1SaveService.TrySave(qaSlot, data, out saved, out message, true))
                throw new IOException(message);
            sb.AppendLine("PASS 2/5 — Save revision " + saved.revision); pass++;

            MS5A1SaveData loaded;
            if (!MS5A1SaveService.TryLoad(qaSlot, out loaded, out message, out recovered, true) ||
                recovered || loaded.revision != saved.revision)
                throw new IOException("Load roundtrip lỗi: " + message);
            sb.AppendLine("PASS 3/5 — JSON + SHA256 load roundtrip"); pass++;

            // Create a second snapshot: previous valid primary must become .bak.
            if (!MS5A1SaveService.TrySave(qaSlot, loaded, out saved, out message, true))
                throw new IOException("Không tạo được backup: " + message);
            if (!File.Exists(MS5A1SaveService.GetBackupPath(qaSlot, true)))
                throw new IOException("Không tìm thấy .bak sau khi lưu lần hai");
            sb.AppendLine("PASS 4/5 — Automatic .bak created"); pass++;

            // Only fake damage on isolated QA slot. Guaranteed restore in finally.
            originalMain = File.ReadAllBytes(path);
            tampered = true;
            File.WriteAllText(path, "{ INTENTIONALLY_BROKEN_QA_FILE }", new UTF8Encoding(false));
            MS5A1SaveData fallback;
            if (!MS5A1SaveService.TryLoad(qaSlot, out fallback, out message, out recovered, true) || !recovered)
                throw new IOException("Backup recovery failed: " + message);
            sb.AppendLine("PASS 5/5 — Corrupt primary detected; loaded valid QA backup"); pass++;
        }
        catch (Exception ex)
        {
            sb.AppendLine("FAILED — " + ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            if (tampered && originalMain != null)
            {
                try { File.WriteAllBytes(path, originalMain); sb.AppendLine("QA main file restored to original bytes."); }
                catch (Exception e) { sb.AppendLine("URGENT: QA main restore FAILED: " + e.Message); }
            }
        }
        sb.AppendLine("RESULT: " + pass + "/" + total + " checks (Editor QA, không phải Build/Gameplay test)");
        sb.AppendLine("Real slot 1–3 not modified. No Inventory/Equipment/Gold sync.");
        string result = sb.ToString();
        Debug.Log(result);
        return result;
    }
}
#endif
