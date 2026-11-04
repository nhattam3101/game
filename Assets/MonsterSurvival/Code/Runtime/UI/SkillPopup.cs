using System.Collections;
using TMPro;
using UnityEngine;

public class SkillPopup : MonoBehaviour
{
    public static void Show(Transform player, string message)
    {
        if (player == null) return;

        GameObject popupObject = new GameObject("SkillPopup");
        popupObject.transform.position = player.position + new Vector3(1.2f, 0.8f, 0f);
        popupObject.transform.localScale = Vector3.one * 0.5f;

        TextMeshPro label = popupObject.AddComponent<TextMeshPro>();
        label.text = message;
        label.fontSize = 3f;
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.yellow;
        label.sortingOrder = 100;

        popupObject.AddComponent<SkillPopup>().StartCoroutine(
            Animate(popupObject.transform, label));
    }

    private static IEnumerator Animate(Transform target, TextMeshPro label)
    {
        const float duration = 1.5f;
        const float riseSpeed = 0.8f;
        Vector3 origin = target.position;
        Color initialColor = label.color;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            target.position = origin + Vector3.up * (riseSpeed * elapsed);

            Color color = initialColor;
            color.a = 1f - progress;
            label.color = color;
            yield return null;
        }

        if (target != null) Destroy(target.gameObject);
    }
}
