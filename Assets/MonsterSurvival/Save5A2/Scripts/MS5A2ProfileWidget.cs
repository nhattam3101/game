using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI-only profile card. Lives under MainMenu V3 PAGE_Home; does not change menu navigation.
/// Keeps pointer targets limited to the two slot buttons.
/// </summary>
[DisallowMultipleComponent]
public sealed class MS5A2ProfileWidget : MonoBehaviour
{
    [Header("Created by Installer")]
    public TMP_Text slotTitle;
    public TMP_Text bankText;
    public TMP_Text statusText;
    public Button previousButton;
    public Button nextButton;

    private void Awake()
    {
        if (previousButton) previousButton.onClick.AddListener(Previous);
        if (nextButton) nextButton.onClick.AddListener(Next);
    }

    private void OnEnable()
    {
        Refresh();
    }

    public void Previous()
    {
        int slot = MS5A2GoldBank.SelectedSlot;
        MS5A2GoldBank.SelectSlot(slot == 1 ? 3 : slot - 1);
        Refresh();
    }

    public void Next()
    {
        int slot = MS5A2GoldBank.SelectedSlot;
        MS5A2GoldBank.SelectSlot(slot == 3 ? 1 : slot + 1);
        Refresh();
    }

    public void Refresh()
    {
        int slot = MS5A2GoldBank.SelectedSlot;
        int gold, revision;
        string status;
        bool ok = MS5A2GoldBank.TryGetBalance(slot, out gold, out revision, out status);
        if (slotTitle) slotTitle.text = "PROFILE  " + slot.ToString("00") + "  /  03";
        if (bankText) bankText.text = ok ? "BANK GOLD    " + gold.ToString("N0") : "BANK GOLD    --";
        if (statusText)
        {
            statusText.text = ok ? status + "  ·  REV " + revision : "SAVE ERROR — CHECK CONSOLE";
            statusText.color = ok ? new Color(.73f, .70f, .80f, 1f) : new Color(1f, .47f, .45f, 1f);
        }
        if (!ok) Debug.LogWarning("[Save 5A.2] Profile " + slot + ": " + status, this);
    }
}
