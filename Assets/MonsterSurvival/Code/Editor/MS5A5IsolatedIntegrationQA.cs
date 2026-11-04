#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Fixture tests only. Uses Application.temporaryCachePath / random unique directory.
// NEVER calls MS5A1SaveService.TrySave against a real profile.
public static class MS5A5IsolatedIntegrationQA
{
    [Serializable] private sealed class Envelope
    {
        public string magic = "MONSTERSURVIVAL_SAVE_5A1";
        public int formatVersion = 1;
        public string sha256;
        public string payloadJson;
    }

    [MenuItem("Tools/MonsterSurvival/5A Save System/15 - RUN 5A.5 ISOLATED Integration QA (TEMP ONLY)")]
    public static void MenuRun()
    {
        int passed, total;
        string report = Run(out passed, out total);
        if (total > 0 && passed == total) Debug.Log("[5A.5] PASS " + passed + "/" + total + " fixture checks.\n" + report);
        else Debug.LogError("[5A.5] FAIL " + passed + "/" + total + " fixture checks.\n" + report);
        EditorUtility.DisplayDialog("5A.5 Isolated Integration QA", passed + "/" + total + " PASS\n\nSee Console for details. These fixtures do not prove real Gameplay tests.", "OK");
    }

    public static string Run(out int passed, out int total)
    {
        passed = 0; total = 0;
        StringBuilder report = new StringBuilder();
        if (EditorApplication.isPlaying)
        {
            report.AppendLine("BLOCKED: stop Play Mode first. Real save data untouched.");
            return report.ToString();
        }
        string folder = Path.Combine(Application.temporaryCachePath, "MS5A5_QA_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "qa_slot_01.json");
            string bak = path + ".bak";
            string expectedId = "qa_slot_01";
            MS5A1SaveData primary = Fixture(10, 450);
            MS5A1SaveData previous = Fixture(9, 400);
            Write(path, primary); Write(bak, previous);
            MS5A1SaveData read; string reason;
            Check("10. Valid primary SHA256 + profile ID", State(path, expectedId, out read, out reason) == MS5A4SaveRecovery.FileState.Valid && read != null && read.revision == 10 && read.bankGold == 450, ref passed, ref total, report);
            Check("20. Valid backup SHA256", State(bak, expectedId, out read, out reason) == MS5A4SaveRecovery.FileState.Valid && read != null && read.revision == 9, ref passed, ref total, report);
            Check("30. Snapshot 20 inventory + 3 equipped preserved", State(path, expectedId, out read, out reason) == MS5A4SaveRecovery.FileState.Valid && read != null && read.inventorySnapshotPresent && read.inventorySlots.Count == 20 && read.equippedItems.Count == 3 && read.lastCommittedRunId == "fixture_run_0001", ref passed, ref total, report);
            Check("40. Wrong profile ID refused", State(path, "slot_01", out read, out reason) == MS5A4SaveRecovery.FileState.Invalid, ref passed, ref total, report);

            string invalid = Path.Combine(folder, "sha_invalid.json");
            Write(invalid, primary);
            Envelope tampered = JsonUtility.FromJson<Envelope>(File.ReadAllText(invalid, Encoding.UTF8));
            string before = tampered.payloadJson;
            tampered.payloadJson = tampered.payloadJson.Replace("\"bankGold\":450", "\"bankGold\":451");
            File.WriteAllText(invalid, JsonUtility.ToJson(tampered, true), new UTF8Encoding(false));
            bool badSha = State(invalid, expectedId, out read, out reason) == MS5A4SaveRecovery.FileState.Invalid;
            Check("50. Tampered payload blocked by SHA256", before != tampered.payloadJson && badSha && reason.Contains("SHA256"), ref passed, ref total, report);

            MS5A1SaveData malformed = Fixture(2, 200);
            malformed.inventorySlots.RemoveAt(0);
            string malformedPath = Path.Combine(folder, "bad_shape.json");
            Write(malformedPath, malformed);
            Check("60. Invalid 19-slot snapshot rejected", State(malformedPath, expectedId, out read, out reason) == MS5A4SaveRecovery.FileState.Invalid, ref passed, ref total, report);

            MS5A1SaveData negative = Fixture(2, 100);
            negative.inventorySlots[0].quantity = -1;
            string negativePath = Path.Combine(folder, "negative_qty.json");
            Write(negativePath, negative);
            Check("70. Negative quantity rejected", State(negativePath, expectedId, out read, out reason) == MS5A4SaveRecovery.FileState.Invalid, ref passed, ref total, report);

            string otherFormat = Path.Combine(folder, "future_format.json");
            Write(otherFormat, primary, 99);
            Check("80. Future save format refused without downgrade", State(otherFormat, expectedId, out read, out reason) == MS5A4SaveRecovery.FileState.WrongVersion, ref passed, ref total, report);

            string message;
            bool restored = MS5A4SaveRecovery.TryRestoreVerifiedBackup(path, bak, expectedId, out message);
            bool restoredCorrectly = State(path, expectedId, out read, out reason) == MS5A4SaveRecovery.FileState.Valid && read != null && read.revision == 9 && read.bankGold == 400;
            Check("90. Verified backup restore rev10 -> rev9", restored && restoredCorrectly, ref passed, ref total, report);
            bool archived = Directory.Exists(Path.Combine(folder, "5A4_ManualHistory")) &&
                            Directory.GetFiles(Path.Combine(folder, "5A4_ManualHistory"), "*.json").Length > 0;
            Check("100. Previous primary archived before restore", archived, ref passed, ref total, report);
            Check("110. Original backup preserved", State(bak, expectedId, out read, out reason) == MS5A4SaveRecovery.FileState.Valid && read != null && read.revision == 9, ref passed, ref total, report);

            // Verify corrupted .bak never replaces valid primary.
            Write(path, Fixture(11, 500));
            File.WriteAllText(bak, "CORRUPTED-BACKUP", new UTF8Encoding(false));
            bool blocked = !MS5A4SaveRecovery.TryRestoreVerifiedBackup(path, bak, expectedId, out message);
            bool intact = State(path, expectedId, out read, out reason) == MS5A4SaveRecovery.FileState.Valid && read != null && read.revision == 11 && read.bankGold == 500;
            Check("120. Invalid backup cannot overwrite good primary", blocked && intact, ref passed, ref total, report);

            // Optional in-memory catalog check; the fixture is never sent to live Scene objects.
            var registry = AssetDatabase.LoadAssetAtPath<MS5A3ItemRegistry>("Assets/MonsterSurvival/Features/Progression/Save5A3/MS5A3ItemRegistry.asset");
            var guids = AssetDatabase.FindAssets("t:MS3DEquipmentCatalog");
            MS3DEquipmentCatalog catalog = guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<MS3DEquipmentCatalog>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
            if (registry && catalog)
            {
                var clean = Fixture(1, 0);
                MS3AItemData[] items, worn;
                int[] counts;
                string err;
                bool ok = MS5A3SnapshotCodec.TryResolve(clean, registry, catalog, out items, out counts, out worn, out err);
                Check("130. Registry resolves empty 20+3 in-memory snapshot", ok && items != null && items.Length == 20 && worn != null && worn.Length == 3, ref passed, ref total, report);
                clean.inventorySlots[0].itemId = "__missing_item_id_for_QA__";
                clean.inventorySlots[0].quantity = 1;
                bool refused = !MS5A3SnapshotCodec.TryResolve(clean, registry, catalog, out items, out counts, out worn, out err);
                Check("140. Registry blocks unknown Item ID on load", refused, ref passed, ref total, report);
            }
            else report.AppendLine("SKIP (not counted) — Registry/Catalog resolve checks; install 3D.1 + 5A.3 first.");
            report.AppendLine("PASS " + passed + "/" + total + ". Unique temporary directory was used.");
        }
        catch (Exception ex)
        {
            total++;
            report.AppendLine("FAIL — Exception: " + ex);
        }
        finally
        {
            try
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
                report.AppendLine("Temporary fixture directory removed. Real profiles untouched.");
            }
            catch (Exception ex) { report.AppendLine("WARNING: could not remove temp fixture directory: " + folder + " / " + ex.Message); }
        }
        return report.ToString();
    }

    private static MS5A1SaveData Fixture(int revision, int bankGold)
    {
        MS5A1SaveData d = MS5A1SaveService.CreateFresh(1, true);
        d.revision = revision;
        d.bankGold = bankGold;
        d.lastSavedUtc = DateTime.UtcNow.ToString("o");
        d.inventorySnapshotPresent = true;
        d.lastCommittedRunId = "fixture_run_0001";
        d.inventorySlots = new List<MS5A3SavedStack>();
        d.equippedItems = new List<MS5A3SavedEquipped>();
        for (int i = 0; i < 20; i++) d.inventorySlots.Add(new MS5A3SavedStack());
        for (int i = 0; i < 3; i++) d.equippedItems.Add(new MS5A3SavedEquipped { type = i });
        return d;
    }

    private static void Write(string path, MS5A1SaveData save, int version = 1)
    {
        string payload = JsonUtility.ToJson(save, false);
        Envelope env = new Envelope { formatVersion = version, payloadJson = payload };
        using (SHA256 sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(payload));
            StringBuilder hex = new StringBuilder();
            foreach (byte b in hash) hex.Append(b.ToString("x2"));
            env.sha256 = hex.ToString();
        }
        File.WriteAllText(path, JsonUtility.ToJson(env, true), new UTF8Encoding(false));
    }

    private static MS5A4SaveRecovery.FileState State(string path, string expectedId, out MS5A1SaveData data, out string reason)
    {
        return MS5A4SaveRecovery.InspectFile(path, expectedId, out data, out reason);
    }

    private static void Check(string name, bool pass, ref int passed, ref int total, StringBuilder result)
    {
        total++;
        if (pass) passed++;
        result.AppendLine((pass ? "PASS — " : "FAIL — ") + name);
    }
}
#endif
