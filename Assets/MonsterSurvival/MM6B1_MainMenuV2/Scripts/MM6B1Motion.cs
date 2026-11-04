using UnityEngine;

namespace MonsterSurvival.Menu6B1
{
    [DisallowMultipleComponent]
    public sealed class MM6B1Motion : MonoBehaviour
    {
        public RectTransform target;
        [Range(0f, 20f)] public float floatAmplitude = 7f;
        [Range(0f, 5f)] public float floatSpeed = 0.65f;
        [Range(0f, 15f)] public float rotateDegreesPerSecond = 2f;

        Vector2 basePosition;

        void Start()
        {
            if (target == null) target = transform as RectTransform;
            if (target != null) basePosition = target.anchoredPosition;
        }

        void Update()
        {
            if (target == null) return;
            float t = Time.unscaledTime;
            target.anchoredPosition = basePosition + Vector2.up * (Mathf.Sin(t * floatSpeed) * floatAmplitude);
            target.localRotation = Quaternion.Euler(0f, 0f, t * rotateDegreesPerSecond);
        }
    }
}
