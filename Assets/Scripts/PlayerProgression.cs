
using UnityEngine;

public class PlayerProgression : MonoBehaviour
{
    [Header("Level")]
    public int level = 1;
    public int currentExp = 0;
    public int expToNextLevel = 10;

    [Header("Currency")]
    public int coins = 0;

    public System.Action OnProgressChanged;

    private void Start()
    {
        OnProgressChanged?.Invoke();
    }

    public void AddExp(int amount)
    {
        if (amount <= 0)
            return;

        currentExp += amount;

        while (currentExp >= expToNextLevel)
        {
            currentExp -= expToNextLevel;
            level++;

            expToNextLevel = Mathf.CeilToInt(
                expToNextLevel * 1.3f
            );

            SkillManager skillManager =
                FindFirstObjectByType<SkillManager>();

            if (skillManager != null)
                skillManager.OpenSkillSelection();
        }

        OnProgressChanged?.Invoke();
    }

    public void AddCoins(int amount)
    {
        if (amount <= 0)
            return;

        coins += amount;
        OnProgressChanged?.Invoke();
    }

    public bool SpendCoins(int amount)
    {
        if (amount <= 0 || coins < amount)
            return false;

        coins -= amount;
        OnProgressChanged?.Invoke();
        return true;
    }
}
