using System;
using System.Reflection;
using UnityEngine;
using TMPro;

/// <summary>
/// 7C.3.3 READ ONLY. Shared combo definitions and level-based progress.
/// Does not award skills, cause combat effects, write Save or change SkillManager.
/// </summary>
public static class MS7C33SynergyRules
{
    public sealed class Combo
    {
        public readonly string Title, First, Second, Short;
        public Combo(string title, string first, string second, string shortName)
        { Title = title; First = first; Second = second; Short = shortName; }
    }

    public static readonly Combo[] Combos = {
        new Combo("FROZEN BURST", "SlowShot", "ExplosiveShot", "FB"),
        new Combo("PHANTOM CHAIN", "PiercingShot", "RicochetShot", "PC"),
        new Combo("RAPID BLAST", "AttackSpeed", "ExplosiveShot", "RB"),
        new Combo("WILDFIRE CONDUIT", "FireAmmo", "LightningAmmo", "WC"),
        new Combo("GLACIAL REBOUND", "IceAmmo", "RicochetShot", "GR"),
        new Combo("THUNDER PRECISION", "LightningAmmo", "CriticalHit", "TP")
    };

    public static bool Owned(SkillManager manager, string canonical)
    {
        if (manager == null || manager.allSkills == null) return false;
        // 7B.2 deliberately models Fire/Ice/Lightning as SkillType.Heal.
        // Resolve them by EXACT coordinator SkillData reference, not enum Heal.
        SkillData elemental = ElementalAsset(manager, canonical);
        if (elemental != null) return manager.GetSkillLevel(elemental) > 0;
        for (int i = 0; i < manager.allSkills.Count; i++)
        {
            SkillData skill = manager.allSkills[i];
            if (skill != null && Matches(skill, canonical) && manager.GetSkillLevel(skill) > 0)
                return true;
        }
        return false;
    }

    public static SkillData ElementalAsset(SkillManager manager, string canonical)
    {
        if (manager == null) return null;
        MS7B2ElementalSkillCoordinator c = manager.GetComponent<MS7B2ElementalSkillCoordinator>();
        if (c == null) return null;
        string normalized = Normalize(canonical);
        if (normalized == "FIREAMMO") return c.fireSkill;
        if (normalized == "ICEAMMO") return c.iceSkill;
        if (normalized == "LIGHTNINGAMMO") return c.lightningSkill;
        return null;
    }

    public static int OwnedLevel(SkillManager manager, string canonical)
    {
        if (manager == null || manager.allSkills == null) return 0;
        SkillData elemental = ElementalAsset(manager, canonical);
        if (elemental != null) return manager.GetSkillLevel(elemental);
        int result = 0;
        foreach (SkillData skill in manager.allSkills)
            if (skill != null && Matches(skill, canonical))
                result = Mathf.Max(result, manager.GetSkillLevel(skill));
        return result;
    }

    public static bool MatchesOffered(SkillManager manager, SkillData offered, string canonical)
    {
        if (offered == null) return false;
        SkillData elemental = ElementalAsset(manager, canonical);
        if (elemental != null) return offered == elemental;
        return Matches(offered, canonical);
    }

    public static bool Matches(SkillData skill, string canonical)
    {
        if (skill == null) return false;
        string type = Normalize(skill.skillType.ToString());
        string wanted = Normalize(canonical);
        if (type == wanted) return true;
        // Known aliases from the earlier skill and element versions; do NOT
        // treat generic Damage, Critical or Slow as an element accidentally.
        if (wanted == "SLOWSHOT" && type == "FROSTSHOT") return true;
        if (wanted == "CRITICALHIT" && type == "CRITICALSTRIKE") return true;
        if (wanted == "FIREAMMO" && (type == "FIREBULLET" || type == "FIRESHOT")) return true;
        if (wanted == "ICEAMMO" && (type == "ICEBULLET" || type == "ICESHOT")) return true;
        if (wanted == "LIGHTNINGAMMO" && (type == "LIGHTNINGBULLET" || type == "LIGHTNINGSHOT")) return true;
        // Only when a current enum's name is unrecognized: match a skill's
        // actual skillName, never substring-match arbitrary unrelated words.
        string display = Normalize(skill.skillName);
        if (display == wanted) return true;
        if (wanted == "SLOWSHOT" && (display == "FROSTSHOT" || display == "FROST")) return true;
        if (wanted == "CRITICALHIT" && (display == "CRITICALSTRIKE" || display == "CRIT")) return true;
        if (wanted == "FIREAMMO" && (display == "FIREBULLET" || display == "FIRESHOT")) return true;
        if (wanted == "ICEAMMO" && (display == "ICEBULLET" || display == "ICESHOT")) return true;
        if (wanted == "LIGHTNINGAMMO" && (display == "LIGHTNINGBULLET" || display == "LIGHTNINGSHOT")) return true;
        return false;
    }

    private static string Normalize(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        char[] array = text.ToUpperInvariant().ToCharArray();
        System.Text.StringBuilder sb = new System.Text.StringBuilder(array.Length);
        for (int i = 0; i < array.Length; i++)
            if (char.IsLetterOrDigit(array[i])) sb.Append(array[i]);
        return sb.ToString();
    }

    public static int Progress(SkillManager manager, int index)
    {
        if (index < 0 || index >= Combos.Length) return 0;
        Combo c = Combos[index];
        return (Owned(manager, c.First) ? 1 : 0) + (Owned(manager, c.Second) ? 1 : 0);
    }

    public static bool IsActualLegacyUnlocked(PlayerStats stats, int index)
    {
        if (stats == null) return false;
        switch (index)
        {
            case 0: return stats.FrozenBurstUnlocked;
            case 1: return stats.PhantomChainUnlocked;
            case 2: return stats.RapidBlastUnlocked;
            default: return false;
        }
    }

    public static bool CountsAsUnlocked(SkillManager manager, PlayerStats stats, int index)
    {
        if (index < 3) return IsActualLegacyUnlocked(stats, index);
        // The 7C.1 Core's runtime flags are the only authoritative unlock result.
        MS7C1SynergyCore core = MS7C42SynergyStatus.FindCore(stats);
        if (core == null || !core.isActiveAndEnabled) return false;
        core.RefreshNow(false);
        if (index == 3) return core.WildfireConduitUnlocked;
        if (index == 4) return core.GlacialReboundUnlocked;
        if (index == 5) return core.ThunderPrecisionUnlocked;
        return false;
    }

    public static bool OfferedHelps(SkillManager manager, SkillData offered, int index, out bool completes)
    {
        completes = false;
        if (offered == null || index < 0 || index >= Combos.Length) return false;
        Combo c = Combos[index];
        bool first = Owned(manager, c.First), second = Owned(manager, c.Second);
        bool chooseFirst = !first && MatchesOffered(manager, offered, c.First);
        bool chooseSecond = !second && MatchesOffered(manager, offered, c.Second);
        if (!chooseFirst && !chooseSecond) return false;
        completes = (chooseFirst && second) || (chooseSecond && first);
        return true;
    }

    // The SkillManager in this project keeps its chosen 3 cards as a private
    // SkillData[] offeredSkills. Reflection READS only. Name-based fallback
    // covers future editions without editing existing SkillManager.cs.
    private const BindingFlags OfferedFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    public static SkillData GetOffered(SkillManager manager, int slot)
    {
        if (manager == null || slot < 0 || slot >= 3) return null;
        try
        {
            FieldInfo field = manager.GetType().GetField("offeredSkills", OfferedFlags);
            if (field != null)
            {
                SkillData[] array = field.GetValue(manager) as SkillData[];
                if (array != null && slot < array.Length) return array[slot];
            }
        }
        catch (Exception) { }
        if (manager.skillNames == null || slot >= manager.skillNames.Length ||
            manager.skillNames[slot] == null || manager.allSkills == null) return null;
        string shown = manager.skillNames[slot].text;
        if (string.IsNullOrEmpty(shown)) return null;
        int levelTag = shown.IndexOf(" (Lv.", StringComparison.OrdinalIgnoreCase);
        if (levelTag >= 0) shown = shown.Substring(0, levelTag);
        for (int i = 0; i < manager.allSkills.Count; ++i)
        {
            SkillData skill = manager.allSkills[i];
            if (skill != null && string.Equals(skill.skillName, shown, StringComparison.OrdinalIgnoreCase)) return skill;
        }
        return null;
    }
}
