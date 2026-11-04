using UnityEngine;
using UnityEngine.UI;

// 7B.2: keeps elemental skill configuration independent of PlayerStats/Save.
// One per SkillManager; the existing SkillManager still owns the 3-card roll and levels.
[DisallowMultipleComponent]
public sealed class MS7B2ElementalSkillCoordinator : MonoBehaviour
{
    [Header("7B.2 SkillData assets (created by Installer)")]
    public SkillData fireSkill;
    public SkillData iceSkill;
    public SkillData lightningSkill;

    [Header("7B.1 Player loadout and base settings")]
    public MS7B1ElementLoadout playerLoadout;
    public MS7B1ElementData baseElementData;

    [Header("Optional 3 icon overlays (created on skill buttons)")]
    public Image[] cardIconImages = new Image[3];

    [Header("Tuning per owned level. Each element max level=3")]
    [Range(0f, 1f)] public float fireDpsBonusPerLevel = 0.30f;
    [Range(0f, 0.5f)] public float iceSlowBonusPerLevel = 0.10f;
    [Range(0f, 1f)] public float lightningRatioBonusPerLevel = 0.10f;
    [Range(0, 3)] public int lightningExtraTargetsPerLevel = 1;

    private MS7B1ElementData runtimeCopy;
    private readonly System.Collections.Generic.List<MS7B1ElementData> runtimeCopies =
        new System.Collections.Generic.List<MS7B1ElementData>();

    public bool IsElemental(SkillData skill)
    {
        return skill != null && (skill == fireSkill || skill == iceSkill || skill == lightningSkill);
    }

    public bool TryApply(SkillData selected, int newLevel)
    {
        if (!IsElemental(selected)) return false;
        if (playerLoadout == null) playerLoadout = FindFirstObjectByType<MS7B1ElementLoadout>();
        if (playerLoadout == null)
        {
            Debug.LogError("[7B.2] Missing 7B.1 MS7B1ElementLoadout on Player. Elemental skill not activated.", this);
            return true; // Do NOT call PlayerStats.ApplySkill on a special skill.
        }
        if (baseElementData == null) baseElementData = playerLoadout.elementData;
        if (baseElementData == null)
        {
            Debug.LogError("[7B.2] Missing base 7B.1 Element Data. Check Installer/Audit.", this);
            return true;
        }

        // Always rebuild from the ORIGINAL base asset; never multiply an already upgraded clone.
        runtimeCopy = Instantiate(baseElementData);
        runtimeCopies.Add(runtimeCopy);
        runtimeCopy.name = "MS7B2_RuntimeElementData";
        runtimeCopy.hideFlags = HideFlags.DontSave;
        int extra = Mathf.Max(0, newLevel - 1);

        if (selected == fireSkill)
        {
            playerLoadout.selectedElement = MS7B1ElementType.Fire;
            runtimeCopy.burnDamagePerSecond = baseElementData.burnDamagePerSecond * (1f + fireDpsBonusPerLevel * extra);
        }
        else if (selected == iceSkill)
        {
            playerLoadout.selectedElement = MS7B1ElementType.Ice;
            runtimeCopy.slowPercent = Mathf.Clamp(baseElementData.slowPercent + iceSlowBonusPerLevel * extra, 0f, 0.9f);
        }
        else
        {
            playerLoadout.selectedElement = MS7B1ElementType.Lightning;
            runtimeCopy.maxChainTargets = Mathf.Clamp(baseElementData.maxChainTargets + lightningExtraTargetsPerLevel * extra, 0, 8);
            runtimeCopy.chainDamageRatio = Mathf.Clamp(baseElementData.chainDamageRatio + lightningRatioBonusPerLevel * extra, 0f, 2f);
        }
        playerLoadout.elementData = runtimeCopy;
        Debug.Log("[7B.2] Equipped " + playerLoadout.selectedElement + " Lv." + newLevel + " (run-only).", this);
        return true;
    }

    public void DecorateOffer(SkillManager manager, SkillData skill, int index, int previousLevel, int nextLevel)
    {
        // Never leave an elemental icon on a reused normal skill button.
        if (cardIconImages != null && index >= 0 && index < cardIconImages.Length && cardIconImages[index] != null)
        {
            Image image = cardIconImages[index];
            bool visible = IsElemental(skill) && skill.icon != null;
            image.gameObject.SetActive(visible);
            if (visible) image.sprite = skill.icon;
        }
        if (!IsElemental(skill) || manager == null || manager.skillDescriptions == null ||
            index < 0 || index >= manager.skillDescriptions.Length || manager.skillDescriptions[index] == null)
            return;

        int extra = Mathf.Max(0, nextLevel - 1);
        string effect;
        MS7B1ElementData data = baseElementData != null ? baseElementData :
            playerLoadout != null ? playerLoadout.elementData : null;
        if (skill == fireSkill)
        {
            float dps = data != null ? data.burnDamagePerSecond * (1f + fireDpsBonusPerLevel * extra) : 0f;
            effect = "BURN  |  " + (data != null ? dps.ToString("0.#") + " HP/s" : "kiem tra Data") + "\nLam moi thoi gian chay; khong stack vo han";
        }
        else if (skill == iceSkill)
        {
            float slow = data != null ? Mathf.Clamp(data.slowPercent + iceSlowBonusPerLevel * extra, 0f, 0.9f) : 0f;
            effect = "SLOW  |  " + (data != null ? (slow * 100f).ToString("0") + "%" : "kiem tra Data") + "\nCan EnemySlowEffect tren Enemy Prefab";
        }
        else
        {
            int targets = data != null ? Mathf.Clamp(data.maxChainTargets + lightningExtraTargetsPerLevel * extra, 0, 8) : 0;
            float ratio = data != null ? Mathf.Clamp(data.chainDamageRatio + lightningRatioBonusPerLevel * extra, 0f, 2f) : 0f;
            effect = "CHAIN  |  " + (data != null ? targets + " muc tieu, " + (ratio * 100f).ToString("0") + "% dmg" : "kiem tra Data") + "\nKhong danh trung mot quai 2 lan / chuoi";
        }
        manager.skillDescriptions[index].text = effect + "\nLv. " + previousLevel + " -> " + nextLevel + "/" + skill.maxLevel + " | " + skill.rarity;
    }

    private void OnDestroy()
    {
        if (playerLoadout != null && runtimeCopy != null && playerLoadout.elementData == runtimeCopy)
            playerLoadout.elementData = baseElementData;
        foreach (MS7B1ElementData instance in runtimeCopies)
            if (instance != null) Destroy(instance);
        runtimeCopies.Clear();
    }
}
