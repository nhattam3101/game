#if UNITY_EDITOR
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MSCoreGameplayAudit
{
    private const string GameplayScene = "Assets/MonsterSurvival/Content/Scenes/SampleScene.unity";
    private const string MainMenuScene = "Assets/MonsterSurvival/Content/Scenes/MainMenu.unity";
    private const string PlayerHubScene = "Assets/MonsterSurvival/Content/Scenes/PlayerHub.unity";

    [MenuItem("Tools/MonsterSurvival/QA/Core Gameplay Audit (Read Only)")]
    public static void Run()
    {
        var report = new StringBuilder("MONSTERSURVIVAL CORE GAMEPLAY AUDIT\n");
        report.AppendLine("Unity=" + Application.unityVersion + " Scene=" + SceneManager.GetActiveScene().path);

        CheckScene(report, MainMenuScene);
        CheckScene(report, GameplayScene);
        CheckScene(report, PlayerHubScene);

        GameManager game = Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        WaveManager waves = Object.FindFirstObjectByType<WaveManager>(FindObjectsInactive.Include);
        EnemySpawner spawner = Object.FindFirstObjectByType<EnemySpawner>(FindObjectsInactive.Include);
        SkillManager skills = Object.FindFirstObjectByType<SkillManager>(FindObjectsInactive.Include);
        PlayerController player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        Health playerHealth = player != null ? player.GetComponent<Health>() : null;

        Pass(report, game != null, "GameManager present");
        Pass(report, game != null && game.mainMenuScenePath == MainMenuScene, "GameManager MainMenu path");
        Pass(report, waves != null && waves.enemySpawner != null, "WaveManager wired to EnemySpawner");
        Pass(report, waves != null && waves.enableCoreEnemyScaling, "Core wave scaling enabled");
        Pass(report, spawner != null && spawner.spawnFromViewportEdge, "Edge spawning enabled");
        Pass(report, player != null && player.enableDash, "Player dash enabled");
        Pass(report, playerHealth != null && playerHealth.playerHitInvulnerability > 0f, "Player hit protection enabled");
        Pass(report, skills != null && skills.allSkills != null && skills.allSkills.Count >= 3, "Skill pool has at least three entries");

        report.AppendLine(EditorApplication.isPlaying
            ? "[INFO] Runtime state: wave=" + (waves != null ? waves.CurrentWave : 0) +
              " pendingSkillChoices=" + (skills != null ? skills.PendingSelections : 0)
            : "[PENDING] Play Mode: test movement, Space dash, multi-level skill queue, five waves, Victory/Defeat and return flow.");

        Debug.Log(report.ToString());
    }

    private static void CheckScene(StringBuilder report, string path)
    {
        bool exists = AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null;
        bool enabled = EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path == path);
        Pass(report, exists && enabled, path + " exists and is enabled");
    }

    private static void Pass(StringBuilder report, bool success, string message)
    {
        report.AppendLine((success ? "[PASS] " : "[ERROR] ") + message);
    }
}
#endif
