using UnityEngine;

// The burst is created only if the original pickup was actually collected.
// It never interacts with the Player, inventory counts or item drops.
public sealed class MS3CCollectBurst : MonoBehaviour
{
    private SpriteRenderer[] dots;
    private Vector3[] directions;
    private float age;
    private Color tint;
    private const float Duration = .42f;

    public static void Spawn(Vector3 at, MS3CRarity rarity)
    {
        GameObject obj = new GameObject("MS3C_CollectedFX");
        obj.transform.position = at;
        var burst = obj.AddComponent<MS3CCollectBurst>();
        burst.Initialize(rarity);
    }

    private void Initialize(MS3CRarity rarity)
    {
        tint = MS3CRarityCatalog.ColorOf(rarity);
        int count = rarity == MS3CRarity.Legendary ? 10 : rarity == MS3CRarity.Epic ? 8 : 5;
        dots = new SpriteRenderer[count];
        directions = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            float angle = (i + .15f) * Mathf.PI * 2f / count;
            directions[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            var point = MS3CVisualFX.CreateGlow(transform, "Burst_" + i, MS3CVisualFX.SoftDot, tint, 0, 40);
            point.transform.localScale = Vector3.one * .075f;
            dots[i] = point;
        }
    }

    private void Update()
    {
        age += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(age / Duration);
        if (dots != null)
        {
            for (int i = 0; i < dots.Length; i++)
            {
                if (dots[i] == null) continue;
                dots[i].transform.localPosition = directions[i] * (t * .48f);
                dots[i].transform.localScale = Vector3.one * (.05f + .045f * Mathf.Sin(t * Mathf.PI));
                Color c = tint; c.a = (1f - t) * .83f;
                dots[i].color = c;
            }
        }
        if (age >= Duration) Destroy(gameObject);
    }
}
