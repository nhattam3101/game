using UnityEngine;

// Add to Player: deliberately separate from PlayerStats / SkillManager / Save.
[DisallowMultipleComponent]
public sealed class MS7B1ElementLoadout : MonoBehaviour
{
    [Header("Test selection only; Skill Selection integration belongs to 7B.2")]
    public MS7B1ElementType selectedElement = MS7B1ElementType.None;
    public MS7B1ElementData elementData;

    [Header("Optional debug shortcuts (disabled by default)")]
    public bool allowDebugHotkeys = false;

    private void Update()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        if (!allowDebugHotkeys || Time.timeScale <= 0f) return;
        if (Input.GetKeyDown(KeyCode.F6)) selectedElement = MS7B1ElementType.Fire;
        if (Input.GetKeyDown(KeyCode.F7)) selectedElement = MS7B1ElementType.Ice;
        if (Input.GetKeyDown(KeyCode.F8)) selectedElement = MS7B1ElementType.Lightning;
        if (Input.GetKeyDown(KeyCode.F9)) selectedElement = MS7B1ElementType.None;
#endif
    }
}
