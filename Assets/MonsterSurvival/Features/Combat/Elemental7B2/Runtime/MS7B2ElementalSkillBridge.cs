using UnityEngine;

// Two tiny hooks added to the EXISTING SkillManager.cs by the 7B.2 installer.
public static class MS7B2ElementalSkillBridge
{
    public static bool TryApply(SkillManager manager, PlayerStats player, SkillData skill, int newLevel)
    {
        if (manager == null || skill == null) return false;
        MS7B2ElementalSkillCoordinator addon = manager.GetComponent<MS7B2ElementalSkillCoordinator>();
        return addon != null && addon.TryApply(skill, newLevel);
    }

    public static void DecorateOffer(SkillManager manager, SkillData skill, int index, int currentLevel, int nextLevel)
    {
        if (manager == null) return;
        MS7B2ElementalSkillCoordinator addon = manager.GetComponent<MS7B2ElementalSkillCoordinator>();
        if (addon != null) addon.DecorateOffer(manager, skill, index, currentLevel, nextLevel);
    }
}
