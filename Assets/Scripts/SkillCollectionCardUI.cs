
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkillCollectionCardUI : MonoBehaviour
{
    [Header("Card Background")]
    public Image cardBackground;
    public Outline rarityOutline;

    [Header("Icon")]
    public Image iconImage;

    [Header("Texts")]
    public TMP_Text skillNameText;
    public TMP_Text rarityText;
    public TMP_Text levelText;
    public TMP_Text descriptionText;

    [Header("Level Progress")]
    public Image progressBackground;
    public Image progressFill;

    [Header("Glow Animation")]
    public bool animateGlow = true;
    public float glowSpeed = 3f;

    private Color rarityColor = Color.white;
    private bool shouldPulse = false;

    private void Update()
    {
        if (!animateGlow ||
            !shouldPulse ||
            rarityOutline == null)
        {
            return;
        }

        float pulse = (
            Mathf.Sin(
                Time.unscaledTime * glowSpeed
            ) + 1f
        ) * 0.5f;

        Color glow = rarityColor;

        glow.a = Mathf.Lerp(
            0.2f,
            0.8f,
            pulse
        );

        rarityOutline.effectColor = glow;
    }

    // Hien thi thong tin ky nang tren the
    public void Setup(SkillData skill, int level)
    {
        if (skill == null)
            return;

        int maxLevel = Mathf.Max(
            1,
            skill.maxLevel
        );

        level = Mathf.Clamp(
            level,
            0,
            maxLevel
        );

        rarityColor = GetRarityColor(
            skill.rarity
        );

        // Ten ky nang
        if (skillNameText != null)
        {
            skillNameText.text =
                string.IsNullOrEmpty(skill.skillName)
                ? skill.name
                : skill.skillName;

            skillNameText.color = rarityColor;
        }

        // Do hiem
        if (rarityText != null)
        {
            rarityText.text =
                GetRarityName(skill.rarity);

            rarityText.color = rarityColor;
        }

        // Cap do
        if (levelText != null)
        {
            if (level >= maxLevel)
            {
                levelText.text = "MAX LEVEL";
                levelText.color = Color.yellow;
            }
            else
            {
                levelText.text =
                    "Lv." + level + "/" + maxLevel;

                levelText.color = Color.white;
            }
        }

        // Mo ta
        if (descriptionText != null)
        {
            descriptionText.text =
                skill.description;
        }

        // Bieu tuong
        if (iconImage != null)
        {
            if (skill.icon != null)
            {
                iconImage.sprite = skill.icon;
            }

            iconImage.color = rarityColor;
        }

        // Mau nen the
        if (cardBackground != null)
        {
            Color baseColor = new Color(
                0.08f,
                0.12f,
                0.20f,
                0.96f
            );

            cardBackground.color = Color.Lerp(
                baseColor,
                rarityColor,
                0.10f
            );
        }

        // Vien theo do hiem
        if (rarityOutline != null)
        {
            rarityOutline.enabled = true;

            rarityOutline.effectDistance =
                new Vector2(2f, 2f);

            rarityOutline.useGraphicAlpha = false;

            Color outlineColor = rarityColor;
            outlineColor.a = 0.4f;

            rarityOutline.effectColor =
                outlineColor;
        }

        // Nen thanh cap do
        if (progressBackground != null)
        {
            progressBackground.color =
                new Color(
                    0.17f,
                    0.23f,
                    0.33f,
                    1f
                );
        }

        // Thanh tien trinh
        if (progressFill != null)
        {
            progressFill.type = Image.Type.Filled;

            progressFill.fillMethod =
                Image.FillMethod.Horizontal;

            progressFill.fillOrigin = 0;

            progressFill.fillAmount =
                (float)level / maxLevel;

            progressFill.color = rarityColor;
        }

        // Chi Epic va Legendary nhap nhay
        shouldPulse =
            skill.rarity == SkillRarity.Epic ||
            skill.rarity == SkillRarity.Legendary;
    }

    private Color GetRarityColor(
        SkillRarity rarity
    )
    {
        switch (rarity)
        {
            case SkillRarity.Common:
                return new Color(
                    0.88f, 0.91f, 0.95f
                );

            case SkillRarity.Rare:
                return new Color(
                    0.22f, 0.74f, 0.97f
                );

            case SkillRarity.Epic:
                return new Color(
                    0.75f, 0.45f, 1f
                );

            case SkillRarity.Legendary:
                return new Color(
                    1f, 0.76f, 0.15f
                );

            default:
                return Color.white;
        }
    }

    private string GetRarityName(
        SkillRarity rarity
    )
    {
        switch (rarity)
        {
            case SkillRarity.Common:
                return "COMMON";

            case SkillRarity.Rare:
                return "RARE";

            case SkillRarity.Epic:
                return "EPIC";

            case SkillRarity.Legendary:
                return "LEGENDARY";

            default:
                return "UNKNOWN";
        }
    }
}
