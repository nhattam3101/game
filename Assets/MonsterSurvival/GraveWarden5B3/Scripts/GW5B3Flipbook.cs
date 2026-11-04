using UnityEngine;

// VFX-only sprite animation; never touches Health, AI, physics or gameplay state.
[DisallowMultipleComponent]
public sealed class GW5B3Flipbook : MonoBehaviour
{
    private SpriteRenderer target;
    private Sprite[] frames;
    private Sprite original;
    private float frameDuration;
    private float accumulator;
    private int index;
    private bool loop;
    private bool destroyObject;
    private bool restore;
    private bool finished;

    public void Initialize(SpriteRenderer renderer, Sprite[] sequence, float framesPerSecond,
                           bool looping, bool destroyOnFinish, bool restoreOriginal = false)
    {
        target = renderer;
        frames = sequence;
        loop = looping;
        destroyObject = destroyOnFinish;
        restore = restoreOriginal;
        frameDuration = 1f / Mathf.Max(1f, framesPerSecond);
        accumulator = 0f;
        index = 0;
        finished = false;
        if (target != null)
        {
            original = target.sprite;
            if (frames != null && frames.Length > 0) target.sprite = frames[0];
        }
    }

    private void Update()
    {
        if (finished || target == null || frames == null || frames.Length == 0) return;
        if (Time.timeScale <= 0f) return;
        accumulator += Time.deltaTime;
        while (accumulator >= frameDuration)
        {
            accumulator -= frameDuration;
            index++;
            if (index >= frames.Length)
            {
                if (loop) index = 0;
                else
                {
                    finished = true;
                    if (restore) target.sprite = original;
                    else target.sprite = frames[frames.Length - 1];
                    if (destroyObject) Destroy(gameObject);
                    return;
                }
            }
            target.sprite = frames[index];
        }
    }
}
