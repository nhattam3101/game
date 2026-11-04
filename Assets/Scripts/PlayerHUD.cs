
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHUD : MonoBehaviour
{
    [Header("Health UI")]
    public Slider healthBar;
    public TMP_Text healthText;

    [Header("EXP and Gold UI")]
    public TMP_Text expText;
    public TMP_Text goldText;

    private Health playerHealth;
    private PlayerProgression progression;

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
        {
            Debug.LogError("PlayerHUD: Không tìm thấy Player!");
            return;
        }

        playerHealth = player.GetComponent<Health>();
        progression = player.GetComponent<PlayerProgression>();

        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += UpdateHealthUI;

            UpdateHealthUI(
                playerHealth.CurrentHealth,
                playerHealth.maxHealth
            );
        }
        else
        {
            Debug.LogError("Player thiếu component Health!");
        }

        if (progression != null)
        {
            progression.OnProgressChanged += UpdateProgressUI;
            UpdateProgressUI();
        }
        else
        {
            Debug.LogError("Player thiếu component PlayerProgression!");
        }
    }

    private void UpdateHealthUI(float currentHealth, float maxHealth)
    {
        if (healthBar != null)
        {
            healthBar.maxValue = maxHealth;
            healthBar.value = currentHealth;
        }

        if (healthText != null)
        {
            healthText.text =
                Mathf.CeilToInt(currentHealth) + " / " +
                Mathf.CeilToInt(maxHealth);
        }
    }

    private void UpdateProgressUI()
    {
        if (progression == null)
            return;

        if (expText != null)
        {
            expText.text =
                "Cấp " + progression.level +
                "  |  EXP: " + progression.currentExp +
                " / " + progression.expToNextLevel;
        }

        if (goldText != null)
        {
            goldText.text = "Vàng: " + progression.coins;
        }
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
            playerHealth.OnHealthChanged -= UpdateHealthUI;

        if (progression != null)
            progression.OnProgressChanged -= UpdateProgressUI;
    }
}
