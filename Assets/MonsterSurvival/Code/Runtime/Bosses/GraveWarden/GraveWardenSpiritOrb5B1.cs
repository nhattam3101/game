using UnityEngine;

[DisallowMultipleComponent]
public class GraveWardenSpiritOrb5B1 : MonoBehaviour
{
    private GraveWardenSpiritOrbit5B1 owner;
    private Transform center;
    private Rigidbody2D body;
    private int index, total;
    private float orbitRadius, turnsPerSecond, angle;

    public void Initialize(GraveWardenSpiritOrbit5B1 source, Transform boss, Rigidbody2D rb,
        int orbIndex, int orbCount, float radius, float speed)
    {
        owner = source; center = boss; body = rb;
        index = orbIndex; total = orbCount;
        orbitRadius = radius; turnsPerSecond = speed;
        angle = Mathf.PI * 2f * index / Mathf.Max(1, total);
    }

    private void FixedUpdate()
    {
        if (center == null || owner == null) return;
        angle += turnsPerSecond * Mathf.PI * 2f * Time.fixedDeltaTime;
        Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * orbitRadius;
        body.MovePosition((Vector2)center.position + offset);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (owner != null) owner.TryHit(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (owner != null) owner.TryHit(other);
    }
}
