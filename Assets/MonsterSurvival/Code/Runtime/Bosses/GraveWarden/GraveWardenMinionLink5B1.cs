using UnityEngine;

[DisallowMultipleComponent]
public class GraveWardenMinionLink5B1 : MonoBehaviour
{
    private GraveWardenSummons5B1 manager;
    private Health health;
    private bool wasWraith;
    private bool reported;

    public void Bind(GraveWardenSummons5B1 source, bool wraith)
    {
        if (health != null) health.OnDied -= Died;
        manager = source;
        wasWraith = wraith;
        health = GetComponent<Health>();
        if (health != null) health.OnDied += Died;
    }

    private void Died()
    {
        if (reported) return;
        reported = true;
        if (manager != null) manager.OnControlledMinionDied(transform.position, wasWraith);
    }

    private void OnDestroy()
    {
        if (health != null) health.OnDied -= Died;
    }
}
