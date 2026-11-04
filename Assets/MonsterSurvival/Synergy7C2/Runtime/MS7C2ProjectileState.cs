using UnityEngine;

// Runtime-only information for a single Bullet; destroyed automatically with that Bullet.
// Never save or attach to a prefab asset in Edit Mode.
[DisallowMultipleComponent]
public sealed class MS7C2ProjectileState : MonoBehaviour
{
    public bool IsCritical { get; private set; }
    public void SetCritical(bool value) { IsCritical = value; }
}
