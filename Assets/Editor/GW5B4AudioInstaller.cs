#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Add under Assets/Editor. Safe: changes ONLY selected GraveWardenBoss PREFAB asset.
public static class GW5B4AudioInstaller
{
    private const string Root = "Assets/MonsterSurvival/GraveWarden5B4/Audio/";

    [MenuItem("Tools/MonsterSurvival/5B.4 Install Grave Warden Audio (Selected Prefab)")]
    private static void InstallSelected()
    {
        string path;
        GameObject selected = GetSelectedPrefab(out path);
        if (selected == null) return;

        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (contents.GetComponent<GraveWardenBossAI>() == null)
            {
                EditorUtility.DisplayDialog("5B.4", "Prefab duoc chon khong co GraveWardenBossAI. Hay chon dung GraveWardenBoss.", "OK");
                return;
            }
            GraveWardenAudio5B4 audio = contents.GetComponent<GraveWardenAudio5B4>();
            if (audio == null) audio = contents.AddComponent<GraveWardenAudio5B4>();
            audio.boss = contents.GetComponent<GraveWardenBossAI>();
            audio.bossHealth = contents.GetComponent<Health>();
            Transform visual = contents.transform.Find("GW_Visual");
            audio.visualAnimator = visual != null ? visual.GetComponent<Animator>() : contents.GetComponentInChildren<Animator>(true);
            audio.spawnIntro = Sound("GW_Spawn_Intro");
            audio.tombWallPrepare = Sound("GW_TombWall_Prepare");
            audio.tombWallEmerge = Sound("GW_TombWall_Emerge");
            audio.wraithPrepare = Sound("GW_Wraith_Prepare");
            audio.wraithSummon = Sound("GW_Wraith_Summon");
            audio.reanimateCast = Sound("GW_Reanimate_Cast");
            audio.reanimateRise = Sound("GW_Reanimate_Rise");
            audio.spiritOrbitCast = Sound("GW_SpiritOrbit_Cast");
            audio.soulHarvestAbsorb = Sound("GW_SoulHarvest_Absorb");
            audio.empoweredCast = Sound("GW_Empowered_Cast");
            audio.phase2Transform = Sound("GW_Phase2_Transform");
            audio.hit = Sound("GW_Hit");
            audio.death = Sound("GW_Death");
            audio.phase1BattleMusic = Music("GW_Boss_Phase1_Loop");
            audio.phase2BattleMusic = Music("GW_Boss_Phase2_Loop");
            if (audio.visualAnimator == null)
                Debug.LogWarning("[GW 5B.4] Khong tim thay Animator con. Gan GW_Visual > Animator vao GraveWardenAudio5B4.");
            PrefabUtility.SaveAsPrefabAsset(contents, path);
            Debug.Log("[GW 5B.4] Installed 15 sounds on prefab: " + path);
            EditorUtility.DisplayDialog("MonsterSurvival 5B.4",
                "Da gan 13 SFX + 2 nhac vao Grave Warden prefab.\n\n" +
                "Kiem tra Inspector: GraveWardenAudio5B4 > Visual Animator la GW_Visual.\n" +
                "Neu nhac game cu dang phat, tat Play Boss Music hoac dung mixer nhac nen de tranh de nhac.", "OK");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    [MenuItem("Tools/MonsterSurvival/5B.4 Apply Recommended Boss Balance (Optional)")]
    private static void ApplyBalance()
    {
        string path;
        GameObject selected = GetSelectedPrefab(out path);
        if (selected == null) return;
        if (!EditorUtility.DisplayDialog("5B.4 - Optional Balance",
            "Ban muon ap dung preset de xuat cho Grave Warden?\n\n" +
            "Chi sua cac con so cooldown, so luong summon, sat thuong va wall/orbit. " +
            "Khong sua Prefab/AI/VFX va KHONG tu thay Health HP.\n" +
            "Hay backup truoc neu ban da can bang rieng.", "Apply", "Cancel")) return;

        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            GraveWardenBossAI boss = contents.GetComponent<GraveWardenBossAI>();
            GraveWardenSummons5B1 summons = contents.GetComponent<GraveWardenSummons5B1>();
            GraveWardenTombWall5B1 wall = contents.GetComponent<GraveWardenTombWall5B1>();
            GraveWardenSpiritOrbit5B1 orbit = contents.GetComponent<GraveWardenSpiritOrbit5B1>();
            if (boss == null || summons == null || wall == null || orbit == null)
            {
                EditorUtility.DisplayDialog("5B.4", "Prefab thieu mot so component 5B.1. Huy de tranh cap nhat thieu.", "OK");
                return;
            }
            boss.firstAttackDelay = 2.5f;
            boss.minimumTimeBetweenCasts = 1.5f;
            boss.summonCooldown = 12f;
            boss.tombWallCooldown = 14f;
            boss.spiritOrbitCooldown = 18f;
            boss.reanimateCooldown = 20f;
            boss.phaseTwoCooldownFactor = 0.83f;
            boss.phaseTwoThreshold = 0.5f;
            // Important: do NOT touch stopDuringXXX movement lock options.
            summons.phase1WraithCount = 2;
            summons.phase2WraithCount = 3;
            summons.maxLiveWraiths = 4;
            summons.reanimateCount = 2;
            summons.maxLiveWarriors = 2;
            wall.warningSeconds = 1f;
            wall.wallDurationPhase1 = 5f;
            wall.wallDurationPhase2 = 6f;
            wall.emergeDamage = 8f;
            wall.maxAliveWalls = 3;
            orbit.phase1Orbs = 3;
            orbit.phase2Orbs = 4;
            orbit.maxOrbs = 4;
            orbit.damage = 5f;
            orbit.hitCooldown = 1f;
            orbit.activeSeconds = 5f;
            PrefabUtility.SaveAsPrefabAsset(contents, path);
            Debug.Log("[GW 5B.4] Optional balance applied to " + path);
            EditorUtility.DisplayDialog("5B.4", "Da ap dung preset can bang (khong thay AI code / movement lock / HP).", "OK");
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    private static GameObject GetSelectedPrefab(out string path)
    {
        path = null;
        GameObject selected = Selection.activeObject as GameObject;
        if (selected == null || !PrefabUtility.IsPartOfPrefabAsset(selected))
        {
            EditorUtility.DisplayDialog("MonsterSurvival 5B.4",
                "Trong PROJECT hay click 1 lan vao asset prefab GraveWardenBoss (icon xanh).\n" +
                "KHONG click vao Boss o Hierarchy va KHONG dung trong Prefab Mode.", "OK");
            return null;
        }
        path = AssetDatabase.GetAssetPath(selected);
        return selected;
    }

    private static AudioClip Sound(string file)
    {
        string path = Root + "SFX/" + file + ".wav";
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null) Debug.LogError("[GW 5B.4] Missing WAV at: " + path);
        return clip;
    }

    private static AudioClip Music(string file)
    {
        string path = Root + "Music/" + file + ".wav";
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null) Debug.LogError("[GW 5B.4] Missing WAV at: " + path);
        return clip;
    }
}
#endif
