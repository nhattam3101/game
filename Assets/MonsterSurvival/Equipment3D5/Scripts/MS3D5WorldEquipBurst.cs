using System.Collections.Generic;
using UnityEngine;

// 3D.5: cosmetic world pulses only. Never modifies Player movement, colliders, stats or inventory.
[DisallowMultipleComponent]
public sealed class MS3D5WorldEquipBurst : MonoBehaviour
{
    public Sprite ringSprite;
    [Range(0.1f, 1.5f)] public float lifetime = 0.57f;
    [Range(0.1f, 3f)] public float finalScale = 1.65f;
    public int sortingOrder = 55;

    private sealed class Ring
    {
        public Transform transform;
        public SpriteRenderer sprite;
        public float age;
        public float delay;
        public Color baseColor;
        public float scale;
    }
    private readonly List<Ring> active = new List<Ring>();

    public void Play(Color rarityColor, bool equipping, int count)
    {
        if (!Application.isPlaying || ringSprite == null) return;
        count = Mathf.Clamp(count, 1, 2);
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("MS3D5_TransientEquipPulse");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = ringSprite;
            renderer.sortingOrder = sortingOrder;
            Color c = rarityColor;
            c.a = equipping ? 0.72f : 0.45f;
            renderer.color = c;
            active.Add(new Ring {
                transform = go.transform, sprite = renderer, baseColor = c,
                age = 0f, delay = i * 0.10f, scale = equipping ? 0.35f : 1f
            });
        }
    }

    private void Update()
    {
        if (active.Count == 0) return;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        for (int i = active.Count - 1; i >= 0; i--)
        {
            Ring r = active[i];
            if (r.sprite == null) { active.RemoveAt(i); continue; }
            r.age += dt;
            float t = (r.age - r.delay) / Mathf.Max(0.05f, lifetime);
            if (t < 0f) { r.sprite.enabled = false; continue; }
            r.sprite.enabled = true;
            if (t >= 1f)
            {
                Destroy(r.transform.gameObject);
                active.RemoveAt(i);
                continue;
            }
            // Ease-out, no physics interactions and no Time.timeScale dependency.
            float eased = 1f - (1f - t) * (1f - t);
            float s = Mathf.Lerp(r.scale, finalScale, eased);
            r.transform.localScale = new Vector3(s, s, 1f);
            Color c = r.baseColor;
            c.a *= (1f - t) * (1f - t);
            r.sprite.color = c;
        }
    }

    private void OnDisable()
    {
        for (int i = 0; i < active.Count; i++)
            if (active[i].transform != null) Destroy(active[i].transform.gameObject);
        active.Clear();
    }
}
