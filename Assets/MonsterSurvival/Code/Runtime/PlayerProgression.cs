
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
    public System.Action<int> OnLevelUp;

    private SkillManager skillManager;

    private void Start()
    {
        skillManager = FindFirstObjectByType<SkillManager>();
        OnProgressChanged?.Invoke();
    }

    public void AddExp(int amount)
    {
        if (amount <= 0)
            return;

        currentExp += amount;

        expToNextLevel = Mathf.Max(1, expToNextLevel);

        while (currentExp >= expToNextLevel)
        {
            currentExp -= expToNextLevel;
            level++;

            expToNextLevel = Mathf.CeilToInt(
                expToNextLevel * 1.3f
            );

            OnLevelUp?.Invoke(level);

            if (skillManager == null)
                skillManager = FindFirstObjectByType<SkillManager>();
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

    private void OnValidate()
    {
        level = Mathf.Max(1, level);
        currentExp = Mathf.Max(0, currentExp);
        expToNextLevel = Mathf.Max(1, expToNextLevel);
        coins = Mathf.Max(0, coins);
    }
}
