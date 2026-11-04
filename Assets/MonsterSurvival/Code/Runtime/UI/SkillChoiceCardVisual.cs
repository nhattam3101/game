
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class SkillChoiceCardVisual : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [Header("UI References")]
    public Image cardBackground;
    public Outline rarityOutline;
    public Image iconImage;
    public TMP_Text rarityText;

    [Header("Preview Only")]
    public SkillData previewSkill;

    [Header("Hover Settings")]
    public float hoverScale = 1.04f;
    public float animationSpeed = 10f;

    public Color normalBackground =
        new Color(0.149f, 0.192f, 0.282f, 1f);

    public Color hoverBackground =
        new Color(0.22f, 0.28f, 0.40f, 1f);

    private RectTransform rectTransform;
    private bool isHovered;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        if (cardBackground == null)
        {
            cardBackground = GetComponent<Image>();
        }

        if (rarityOutline == null)
        {
            rarityOutline = GetComponent<Outline>();
        }
    }

    private void OnEnable()
    {
        isHovered = false;

        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        rectTransform.localScale = Vector3.one;

        if (cardBackground != null)
        {
            cardBackground.color = normalBackground;
        }

        // Chi dung de xem thu trong Inspector
        if (previewSkill != null)
        {
            SetSkill(previewSkill);
        }
    }

    private void Update()
    {
        if (rectTransform == null)
            return;

        // Su dung unscaledDeltaTime de Hover van
        // hoat dong khi game tam dung luc len cap.
        float delta = Time.unscaledDeltaTime;

        float targetScale =
            isHovered ? hoverScale : 1f;

        Vector3 target = new Vector3(
            targetScale,
            targetScale,
            1f
        );

        rectTransform.localScale = Vector3.Lerp(
            rectTransform.localScale,
            target,
            Mathf.Clamp01(animationSpeed * delta)
        );

        if (cardBackground != null)
        {
            Color targetColor =
                isHovered
                    ? hoverBackground
                    : normalBackground;

            cardBackground.color = Color.Lerp(
                cardBackground.color,
                targetColor,
                Mathf.Clamp01(animationSpeed * delta)
            );
        }
    }

    public void SetSkill(SkillData skill)
    {
        if (skill == null)
            return;

        Color rarityColor =
            GetRarityColor(skill.rarity);

        // Mau vien theo do hiem
        if (rarityOutline != null)
        {
            rarityOutline.effectColor = rarityColor;
            rarityOutline.effectDistance =
                new Vector2(2f, -2f);
        }

        // Ten do hiem
        if (rarityText != null)
        {
            rarityText.text =
                skill.rarity.ToString().ToUpperInvariant();

            rarityText.color = rarityColor;
        }

        // Bieu tuong ky nang
        if (iconImage != null)
        {
            iconImage.sprite = skill.icon;
            iconImage.enabled = skill.icon != null;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
        }
    }

    private Color GetRarityColor(SkillRarity rarity)
    {
        switch (rarity)
        {
            case SkillRarity.Common:
                return new Color(
                    0.58f, 0.64f, 0.70f, 1f
                );

            case SkillRarity.Rare:
                return new Color(
                    0.22f, 0.74f, 0.97f, 1f
                );

            case SkillRarity.Epic:
                return new Color(
                    0.75f, 0.43f, 0.97f, 1f
                );

            case SkillRarity.Legendary:
                return new Color(
                    1f, 0.75f, 0.18f, 1f
                );

            default:
                return Color.white;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
    }

    private void OnDisable()
    {
        isHovered = false;

        if (rectTransform != null)
        {
            rectTransform.localScale = Vector3.one;
        }
    }
}
