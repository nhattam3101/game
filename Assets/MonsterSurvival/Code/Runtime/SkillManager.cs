
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkillManager : MonoBehaviour
{
    [Header("Skill Database")]
    public List<SkillData> allSkills =
        new List<SkillData>();

    [Header("UI")]
    public GameObject skillPanel;
    public Button[] skillButtons;
    public TMP_Text[] skillNames;
    public TMP_Text[] skillDescriptions;

    [Header("Skill Level UI")]
    public TMP_Text[] skillLevelTexts = new TMP_Text[3];

    [Header("Rarity Weights")]
    public float commonWeight = 60f;
    public float rareWeight = 25f;
    public float epicWeight = 12f;
    public float legendaryWeight = 3f;

    // Ba ky nang duoc hien trong mot lan len cap
    private SkillData[] offeredSkills =
        new SkillData[3];

    // Cap do hien tai cua tung ky nang
    private Dictionary<SkillData, int> skillLevels =
        new Dictionary<SkillData, int>();

    // Pool rut ky nang
    private List<SkillData> remainingSkills =
        new List<SkillData>();

    private bool isSelecting = false;


    private void Start()
    {
        if (skillPanel != null)
        {
            skillPanel.SetActive(false);
        }

        RefillSkillPool();
    }


    public int GetSkillLevel(SkillData skill)
    {
        if (skill == null)
            return 0;

        if (skillLevels.TryGetValue(
            skill,
            out int currentLevel))
        {
            return currentLevel;
        }

        return 0;
    }


    private int GetMaxLevel(SkillData skill)
    {
        if (skill == null)
            return 1;

        return Mathf.Max(1, skill.maxLevel);
    }


    private bool IsMaxLevel(SkillData skill)
    {
        if (skill == null)
            return true;

        return GetSkillLevel(skill) >= GetMaxLevel(skill);
    }


    private int CountAvailableSkills()
    {
        HashSet<SkillData> uniqueSkills =
            new HashSet<SkillData>();

        if (allSkills == null)
            return 0;

        foreach (SkillData skill in allSkills)
        {
            if (skill != null && !IsMaxLevel(skill))
            {
                uniqueSkills.Add(skill);
            }
        }

        return uniqueSkills.Count;
    }


    private void RefillSkillPool()
    {
        remainingSkills.Clear();

        if (allSkills == null)
            return;

        foreach (SkillData skill in allSkills)
        {
            if (skill == null || IsMaxLevel(skill))
                continue;

            if (!remainingSkills.Contains(skill))
            {
                remainingSkills.Add(skill);
            }
        }
    }


    private float GetRarityWeight(SkillRarity rarity)
    {
        switch (rarity)
        {
            case SkillRarity.Common:
                return Mathf.Max(0f, commonWeight);

            case SkillRarity.Rare:
                return Mathf.Max(0f, rareWeight);

            case SkillRarity.Epic:
                return Mathf.Max(0f, epicWeight);

            case SkillRarity.Legendary:
                return Mathf.Max(0f, legendaryWeight);

            default:
                return 1f;
        }
    }


    private Color GetRarityColor(SkillRarity rarity)
    {
        switch (rarity)
        {
            case SkillRarity.Common:
                return Color.white;

            case SkillRarity.Rare:
                return new Color(0.3f, 0.8f, 1f);

            case SkillRarity.Epic:
                return new Color(0.75f, 0.45f, 1f);

            case SkillRarity.Legendary:
                return new Color(1f, 0.78f, 0.2f);

            default:
                return Color.white;
        }
    }


    private SkillData PickWeightedSkill(
        HashSet<SkillData> excludedSkills)
    {
        List<SkillData> candidates =
            new List<SkillData>();

        float totalWeight = 0f;

        foreach (SkillData skill in remainingSkills)
        {
            if (skill == null)
                continue;

            if (IsMaxLevel(skill))
                continue;

            if (excludedSkills.Contains(skill))
                continue;

            candidates.Add(skill);

            totalWeight += GetRarityWeight(
                skill.rarity
            );
        }

        if (candidates.Count == 0)
            return null;

        SkillData selected = null;

        if (totalWeight <= 0f)
        {
            selected = candidates[
                Random.Range(0, candidates.Count)
            ];
        }
        else
        {
            float randomValue =
                Random.Range(0f, totalWeight);

            foreach (SkillData skill in candidates)
            {
                randomValue -= GetRarityWeight(
                    skill.rarity
                );

                if (randomValue < 0f)
                {
                    selected = skill;
                    break;
                }
            }

            if (selected == null)
            {
                selected =
                    candidates[candidates.Count - 1];
            }
        }

        remainingSkills.Remove(selected);

        return selected;
    }


    private SkillData GetNextSkill(
        HashSet<SkillData> alreadyOffered)
    {
        if (remainingSkills.Count == 0)
        {
            RefillSkillPool();
        }

        SkillData skill =
            PickWeightedSkill(alreadyOffered);

        if (skill != null)
            return skill;

        RefillSkillPool();

        return PickWeightedSkill(alreadyOffered);
    }


    private bool ValidateUI()
    {
        if (skillPanel == null ||
            skillButtons == null ||
            skillNames == null ||
            skillDescriptions == null)
        {
            Debug.LogError(
                "SkillManager: Chua gan UI!"
            );

            return false;
        }

        if (skillButtons.Length < 3 ||
            skillNames.Length < 3 ||
            skillDescriptions.Length < 3)
        {
            Debug.LogError(
                "SkillManager: Can du 3 Buttons, " +
                "3 Names va 3 Descriptions!"
            );

            return false;
        }

        for (int i = 0; i < 3; i++)
        {
            if (skillButtons[i] == null ||
                skillNames[i] == null ||
                skillDescriptions[i] == null)
            {
                Debug.LogError(
                    "SkillManager: Thieu UI o vi tri " + i
                );

                return false;
            }
        }

        return true;
    }


    // Mo bang chon ky nang
    public void OpenSkillSelection()
    {
        if (isSelecting)
            return;

        if (!ValidateUI())
            return;

        if (CountAvailableSkills() == 0)
        {
            Debug.Log(
                "Tat ca ky nang da dat cap toi da!"
            );

            return;
        }

        if (remainingSkills.Count == 0)
        {
            RefillSkillPool();
        }

        isSelecting = true;

        // Tam dung game
        Time.timeScale = 0f;

        skillPanel.SetActive(true);

        HashSet<SkillData> alreadyOffered =
            new HashSet<SkillData>();

        for (int i = 0; i < 3; i++)
        {
            skillButtons[i].onClick.RemoveAllListeners();

            SkillData skill =
                GetNextSkill(alreadyOffered);

            offeredSkills[i] = skill;

            if (skill == null)
            {
                skillButtons[i].gameObject.SetActive(false);
                continue;
            }

            alreadyOffered.Add(skill);

            skillButtons[i].gameObject.SetActive(true);

            int currentLevel =
                GetSkillLevel(skill);

            int nextLevel =
                currentLevel + 1;

            int maxLevel =
                GetMaxLevel(skill);

            // Ten ky nang khong kem cap do
            skillNames[i].text = skill.skillName;

            // Mau ten theo do hiem
            skillNames[i].color =
                GetRarityColor(skill.rarity);

            // Chi hien thi mo ta ky nang
            skillDescriptions[i].text = skill.description;

            // Cap do nam trong Text rieng
            if (skillLevelTexts != null &&
                i < skillLevelTexts.Length &&
                skillLevelTexts[i] != null)
            {
                skillLevelTexts[i].text =
                    "Lv. " + currentLevel +
                    " -> " + nextLevel +
                    " / " + maxLevel;
            }

            // =====================================
            // MOI: CAP NHAT GIAO DIEN THE KY NANG
            // =====================================

            SkillChoiceCardVisual cardVisual =
                skillButtons[i].GetComponent<
                    SkillChoiceCardVisual
                >();

            if (cardVisual != null)
            {
                cardVisual.SetSkill(skill);
            }

            // =====================================
            // GIU NGUYEN CO CHE CHON KY NANG
            // =====================================

            MS7B2ElementalSkillBridge.DecorateOffer(this, skill, i, currentLevel, nextLevel);
            int buttonIndex = i;

            skillButtons[i].onClick.AddListener(
                () => ChooseSkill(buttonIndex)
            );
        }
    }


    // Xu ly khi chon ky nang
    public void ChooseSkill(int index)
    {
        if (!isSelecting)
            return;

        if (index < 0 ||
            index >= offeredSkills.Length)
        {
            return;
        }

        SkillData selected =
            offeredSkills[index];

        if (selected == null)
            return;

        if (IsMaxLevel(selected))
            return;

        PlayerStats stats =
            FindFirstObjectByType<PlayerStats>();

        if (stats == null)
        {
            Debug.LogError(
                "SkillManager: Khong tim thay PlayerStats!"
            );

            return;
        }

        int newLevel =
            GetSkillLevel(selected) + 1;

        // Ap dung ky nang
        if (!MS7B2ElementalSkillBridge.TryApply(this, stats, selected, newLevel))
            stats.ApplySkill(selected);

        // Luu cap do moi
        skillLevels[selected] = newLevel;

        // Hien thong bao ben canh Player
        SkillPopup.Show(
            stats.transform,
            selected.skillName +
            " Lv." + newLevel
        );

        Debug.Log(
            "Da nang cap " +
            selected.skillName +
            " len Lv." + newLevel
        );

        // Dong bang chon
        isSelecting = false;

        skillPanel.SetActive(false);

        // Tiep tuc game
        Time.timeScale = 1f;
    }
}
