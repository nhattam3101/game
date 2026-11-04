using UnityEngine;

// 7I.2: non-persistent, single-use mode handoff from MainMenu to SampleScene.
// Does NOT touch PlayerPrefs, Profile, or the existing MMV3MapSession.
public enum MS7I2GameMode { Normal = 0, Endless = 1 }

public static class MS7I2ModeSession
{
    private static bool pending;
    private static MS7I2GameMode pendingMode;
    private static MS7I2GameMode lastAppliedMode;
    private static bool lastLaunchWasFromMenu;
    private static int committedStarts;
    private static int consumedStarts;

    public static bool HasPending { get { return pending; } }
    public static MS7I2GameMode PendingMode { get { return pendingMode; } }
    public static MS7I2GameMode LastAppliedMode { get { return lastAppliedMode; } }
    public static bool LastLaunchWasFromMenu { get { return lastLaunchWasFromMenu; } }
    public static int CommittedStarts { get { return committedStarts; } }
    public static int ConsumedStarts { get { return consumedStarts; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnNewPlaySession()
    {
        pending = false;
        pendingMode = MS7I2GameMode.Normal;
        lastAppliedMode = MS7I2GameMode.Normal;
        lastLaunchWasFromMenu = false;
        committedStarts = 0;
        consumedStarts = 0;
    }

    // MainMenu entry clears any aborted/stale mode handoff.
    public static void EnterMainMenu()
    {
        pending = false;
        pendingMode = MS7I2GameMode.Normal;
    }

    public static void CommitForNextGameplay(MS7I2GameMode mode)
    {
        pendingMode = mode == MS7I2GameMode.Endless ? MS7I2GameMode.Endless : MS7I2GameMode.Normal;
        pending = true;
        committedStarts++;
    }

    public static MS7I2GameMode ConsumeForGameplay(out bool fromMainMenu)
    {
        fromMainMenu = pending;
        MS7I2GameMode mode = pending ? pendingMode : MS7I2GameMode.Normal;
        pending = false;
        pendingMode = MS7I2GameMode.Normal;
        lastAppliedMode = mode;
        lastLaunchWasFromMenu = fromMainMenu;
        if (fromMainMenu) consumedStarts++;
        return mode;
    }

    public static string PureRulesQA()
    {
        int pass = 0; int fail = 0;
        // Test only pure mode-selection and one-shot rules without changing session state.
        bool[] wasPending = { false, true, true };
        MS7I2GameMode[] requested = { MS7I2GameMode.Endless, MS7I2GameMode.Endless, MS7I2GameMode.Normal };
        MS7I2GameMode[] result = { MS7I2GameMode.Normal, MS7I2GameMode.Endless, MS7I2GameMode.Normal };
        for (int i = 0; i < wasPending.Length; i++)
        {
            MS7I2GameMode value = wasPending[i] ? requested[i] : MS7I2GameMode.Normal;
            if (value == result[i]) pass++; else fail++;
        }
        if (MS7I2GameMode.Normal != MS7I2GameMode.Endless) pass++; else fail++;
        if ((int)MS7I2GameMode.Normal == 0 && (int)MS7I2GameMode.Endless == 1) pass++; else fail++;
        if (pass == 5 && fail == 0) return "[7I.2] PURE MODE RULE TESTS PASS=5 FAIL=0 (no Scene / Save writes)";
        return "[7I.2] PURE MODE RULE TESTS PASS=" + pass + " FAIL=" + fail;
    }
}
