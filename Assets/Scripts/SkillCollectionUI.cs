
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkillCollectionUI : MonoBehaviour
{
    [Header("Skill System")]
    public SkillManager skillManager;

    [Header("Collection UI")]
    public GameObject collectionPanel;
    public TMP_Text collectionText;

    [Header("Skill Cards")]
    public Transform cardsContainer;
    public SkillCollectionCardUI cardPrefab;

    [Header("Buttons")]
    public Button openButton;
    public Button closeButton;

    [Header("Settings")]
    public KeyCode toggleKey = KeyCode.K;

    private List<SkillCollectionCardUI> cards =
        new List<SkillCollectionCardUI>();

    private void Start()
    {
        if (openButton != null)
        {
            openButton.onClick.AddListener(
                ToggleCollection
            );
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(
                CloseCollection
            );
        }

        if (collectionPanel != null)
        {
            collectionPanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleCollection();
        }

        if (collectionPanel != null &&
            collectionPanel.activeSelf &&
            IsLevelUpPanelOpen())
        {
            CloseCollection();
        }
    }

    private bool IsLevelUpPanelOpen()
    {
        return skillManager != null &&
               skillManager.skillPanel != null &&
               skillManager.skillPanel.activeInHierarchy;
    }

    public void ToggleCollection()
    {
        if (collectionPanel == null)
            return;

        if (collectionPanel.activeSelf)
        {
            CloseCollection();
        }
        else
        {
            OpenCollection();
        }
    }

    public void OpenCollection()
    {
        if (collectionPanel == null)
        {
            Debug.LogError(
                "Chua gan Collection Panel!"
            );
            return;
        }

        if (skillManager == null)
        {
            Debug.LogError(
                "Chua gan Skill Manager!"
            );
            return;
        }

        if (cardsContainer == null ||
            cardPrefab == null)
        {
            Debug.LogError(
                "Chua gan Cards Container hoac Card Prefab!"
            );
            return;
        }

        if (IsLevelUpPanelOpen())
            return;

        RefreshCollection();

        collectionPanel.SetActive(true);
    }

    public void CloseCollection()
    {
        if (collectionPanel != null)
        {
            collectionPanel.SetActive(false);
        }
    }

    public void RefreshCollection()
    {
        if (skillManager == null ||
            skillManager.allSkills == null ||
            cardsContainer == null ||
            cardPrefab == null)
        {
            return;
        }

        HashSet<SkillData> visited =
            new HashSet<SkillData>();

        int totalSkills = 0;
        int ownedSkills = 0;
        int cardIndex = 0;

        foreach (SkillData skill in skillManager.allSkills)
        {
            if (skill == null || !visited.Add(skill))
                continue;

            totalSkills++;

            int level =
                skillManager.GetSkillLevel(skill);

            if (level <= 0)
                continue;

            ownedSkills++;

            SkillCollectionCardUI card;

            if (cardIndex < cards.Count)
            {
                card = cards[cardIndex];
            }
            else
            {
                card = Instantiate(
                    cardPrefab,
                    cardsContainer
                );

                cards.Add(card);
            }

            card.gameObject.SetActive(true);

            card.Setup(skill, level);

            cardIndex++;
        }

        for (int i = cardIndex; i < cards.Count; i++)
        {
            if (cards[i] != null)
            {
                cards[i].gameObject.SetActive(false);
            }
        }

        if (collectionText != null)
        {
            if (ownedSkills == 0)
            {
                collectionText.text =
                    "Ban chua so huu ky nang nao!";
            }
            else
            {
                collectionText.text =
                    "Da so huu: " +
                    ownedSkills + "/" + totalSkills;
            }
        }
    }

    private void OnDestroy()
    {
        if (openButton != null)
        {
            openButton.onClick.RemoveListener(
                ToggleCollection
            );
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(
                CloseCollection
            );
        }
    }
}
