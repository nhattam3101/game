
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshPro))]
public class DamageNumberPopup : MonoBehaviour
{
    [Header("Animation Settings")]
    public float lifetime = 0.8f;

    public float floatSpeed = 1.2f;

    public float horizontalRandomOffset = 0.25f;

    [Header("Normal Damage")]
    public Color normalColor = Color.white;

    [Header("Critical Damage")]
    public Color criticalColor = Color.yellow;

    public float criticalScaleMultiplier = 1.25f;

    [Header("Render Settings")]
    public int sortingOrder = 100;

    private TextMeshPro damageText;

    private float timer;

    private Color currentColor;

    private bool initialized = false;

    private void Awake()
    {
        damageText = GetComponent<TextMeshPro>();

        // Hien chu phia tren cac Sprite quái
        damageText.sortingOrder = sortingOrder;

        damageText.alignment =
            TextAlignmentOptions.Center;

        damageText.textWrappingMode = TextWrappingModes.NoWrap;
    }

    // Goi khi dan gay sat thuong
    public void Initialize(
        float damage,
        bool isCritical)
    {
        if (damageText == null)
        {
            damageText = GetComponent<TextMeshPro>();
        }

        timer = 0f;
        initialized = true;

        // So sat thuong hien thi
        string damageString =
            Mathf.Max(0f, damage).ToString("0.#");

        if (isCritical)
        {
            damageText.text =
                "CRIT! " + damageString;

            currentColor = criticalColor;

            damageText.fontStyle =
                FontStyles.Bold;

            transform.localScale *=
                criticalScaleMultiplier;
        }
        else
        {
            damageText.text = damageString;

            currentColor = normalColor;

            damageText.fontStyle =
                FontStyles.Normal;
        }

        damageText.color = currentColor;

        // Moi con so lech ngang mot chut
        float offset = Random.Range(
            -horizontalRandomOffset,
            horizontalRandomOffset
        );

        transform.position += new Vector3(
            offset,
            0f,
            0f
        );
    }

    private void Update()
    {
        if (!initialized)
            return;

        float deltaTime = Time.deltaTime;

        timer += deltaTime;

        // Bay len tren
        transform.position += Vector3.up *
            floatSpeed *
            deltaTime;

        // Mo dan theo thoi gian
        float progress = Mathf.Clamp01(
            timer / Mathf.Max(0.01f, lifetime)
        );

        currentColor.a = 1f - progress;

        damageText.color = currentColor;

        // Bien mat khi hoan thanh
        if (timer >= lifetime)
        {
            Destroy(gameObject);
        }
    }

    // Kiem tra Popup ma chua can sua Bullet
    [ContextMenu("Test Damage Popup")]
    private void TestDamagePopup()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "Hay vao Play Mode de test popup."
            );

            return;
        }

        Initialize(25f, false);
    }

    [ContextMenu("Test Critical Popup")]
    private void TestCriticalPopup()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "Hay vao Play Mode de test popup."
            );

            return;
        }

        Initialize(50f, true);
    }
}
