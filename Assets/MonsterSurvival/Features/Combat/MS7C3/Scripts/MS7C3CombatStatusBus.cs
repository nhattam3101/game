using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Optional bridge for an existing 7C.2 combat system. This ONLY reports UI status;
/// it never applies damage, changes an unlock, or modifies PlayerStats.
/// Call Publish("7c1.wildfire_conduit", true/false) from an authoritative 7C.2 system.
/// </summary>
public static class MS7C3CombatStatusBus
{
    private struct StatusValue
    {
        public bool active;
        public SceneHandle sceneHandle;
    }

    private static readonly Dictionary<string, StatusValue> States =
        new Dictionary<string, StatusValue>(StringComparer.OrdinalIgnoreCase);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        States.Clear();
    }

    public static void Publish(string comboId, bool active)
    {
        if (string.IsNullOrEmpty(comboId)) return;
        States[comboId] = new StatusValue
        {
            active = active,
            sceneHandle = SceneManager.GetActiveScene().handle
        };
    }

    public static bool TryGet(string comboId, out bool active)
    {
        StatusValue value;
        if (States.TryGetValue(comboId, out value) &&
            value.sceneHandle == SceneManager.GetActiveScene().handle)
        {
            active = value.active;
            return true;
        }
        active = false;
        return false;
    }

    public static void Clear()
    {
        States.Clear();
    }
}
