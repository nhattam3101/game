using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One component on an ACTIVE child of the HUD Canvas (scene object, NOT prefab).
// Builds the selection menu only once at scene start; it does NOT draw map textures.
// WaveManager and EnemySpawner are disabled until the Start button is pressed.
[DefaultExecutionOrder(500)]
[DisallowMultipleComponent]
public class MapSelectionScreen4C : MonoBehaviour
{
    [Header("Scene references - drag objects from Hierarchy")]
    public WaveManager waveManager;
    public EnemySpawner enemySpawner;
    public WaveMapSwitcher mapSwitcher;
    public MapBiomeRules4C biomeRules;

    [Header("Preview Sprite - drag existing 4B PNG from Project")]
    public Sprite meadowPreview;
    public Sprite desertPreview;
    public Sprite ruinsPreview;

    [Header("Settings")]
    public bool showMenuOnStart = true;
    public bool logSelection = true;

    private readonly string[] names = { "GREEN MEADOW", "SAND DUNES", "ANCIENT RUINS" };
    private readonly string[] difficulties = { "DE • NHO", "TRUNG BINH", "KHO" };
    private readonly string[] features =
    {
        "Hoi 2 HP moi 12 giay",
        "Quai xuat hien nhanh hon 20%\nToi da +2 quai",
        "Toi da +8 quai\nThoi gian nghi giua Wave ngan hon 20%"
    };
    private readonly Color[] accents =
    {
        new Color(0.36f, 0.82f, 0.56f),
        new Color(1f, 0.76f, 0.37f),
        new Color(0.72f, 0.56f, 1f)
    };
    private readonly Color paper = new Color(0.93f, 0.96f, 1f);
    private readonly Color hint = new Color(0.67f, 0.74f, 0.85f);

    private GameObject screen;
    private Image[] cardBackgrounds = new Image[3];
    private TMP_Text[] chooseLabels = new TMP_Text[3];
    private Button startButton;
    private TMP_Text startLabel;
    private int selected = 0;
    private bool matchStarted;

    private void Awake()
    {
        if (!showMenuOnStart) { enabled = false; return; }
        if (waveManager == null) waveManager = FindAnyObjectByType<WaveManager>();
        if (enemySpawner == null && waveManager != null) enemySpawner = waveManager.enemySpawner;
        if (mapSwitcher == null && waveManager != null) mapSwitcher = waveManager.GetComponent<WaveMapSwitcher>();
        if (biomeRules == null && waveManager != null) biomeRules = waveManager.GetComponent<MapBiomeRules4C>();

        if (waveManager == null || enemySpawner == null || mapSwitcher == null || biomeRules == null ||
            mapSwitcher.maps == null || mapSwitcher.maps.Count < 3)
        {
            Debug.LogError("MapSelectionScreen4C: Missing WaveManager / EnemySpawner / WaveMapSwitcher (3 maps) / MapBiomeRules4C. Check Inspector!", this);
            enabled = false;
            return;
        }
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("MapSelectionScreen4C: Put this component under the existing Canvas in the scene.", this);
            enabled = false;
            return;
        }

        // Awake of every component runs before Start of any component.
        waveManager.enabled = false;
        enemySpawner.enabled = false;
        Time.timeScale = 0f;
        BuildMenu(canvas.transform);
    }

    private void Start()
    {
        // Some existing GameManager versions restore timeScale=1 in Start().
        if (enabled && !matchStarted) Time.timeScale = 0f;
    }

    private void LateUpdate()
    {
        if (!matchStarted && Time.timeScale != 0f)
            Time.timeScale = 0f;
    }

    private void OnDisable()
    {
        if (!matchStarted && screen != null)
            Debug.LogWarning("MapSelectionScreen4C was disabled before a map was selected. Re-enable to start waves.", this);
    }

    public void SelectMap(int index)
    {
        if (matchStarted || index < 0 || index >= 3) return;
        if (mapSwitcher.maps[index] == null || mapSwitcher.maps[index].mapRoot == null)
        {
            Debug.LogWarning("MapSelectionScreen4C: Map Root missing for " + names[index], this);
            return;
        }
        selected = index;
        for (int i = 0; i < 3; ++i)
        {
            cardBackgrounds[i].color = i == selected ?
                new Color(0.18f, 0.26f, 0.39f, 1f) : new Color(0.09f, 0.13f, 0.21f, 1f);
            chooseLabels[i].text = i == selected ? "DA CHON" : "CHON MAP";
        }
        startLabel.text = "BAT DAU  ·  " + names[index];
    }

    public void StartMatch()
    {
        if (matchStarted || mapSwitcher == null || waveManager == null || biomeRules == null) return;
        if (!mapSwitcher.SelectMapForMatch(selected)) return;

        biomeRules.StartSelectedMap(selected);
        matchStarted = true;
        if (screen != null) screen.SetActive(false);

        // WaveManager.Start() now runs for the first time and starts Wave 1 as before.
        // EnemySpawner must remain disabled until WaveManager enables it.
        if (enemySpawner != null) enemySpawner.enabled = false;
        waveManager.enabled = true;
        Time.timeScale = 1f;
        if (logSelection) Debug.Log("[4C] Starting match on " + names[selected]);
    }

    private void BuildMenu(Transform canvas)
    {
        screen = Panel("MapSelection_4C_Runtime", canvas,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0.025f, 0.045f, 0.085f, 0.985f)).gameObject;
        screen.transform.SetAsLastSibling();

        Text(screen.transform, "CHON BAN DO", 42, paper,
            new Vector2(0f, 0.86f), new Vector2(1f, 0.98f));
        Text(screen.transform, "Mot ban do cho ca 5 Wave   ·   Shadow Titan xuat hien tai Wave 5", 19, hint,
            new Vector2(0f, 0.81f), new Vector2(1f, 0.87f));

        Sprite[] previews = { meadowPreview, desertPreview, ruinsPreview };
        for (int i = 0; i < 3; i++)
        {
            float left = 0.036f + i * 0.321f;
            RectTransform card = Panel("MapCard_" + (i + 1), screen.transform,
                new Vector2(left, 0.20f), new Vector2(left + 0.285f, 0.79f),
                Vector2.zero, Vector2.zero, new Color(0.09f, 0.13f, 0.21f));
            cardBackgrounds[i] = card.GetComponent<Image>();

            RectTransform imageRect = Rect("Preview", card, new Vector2(0.045f, 0.34f),
                new Vector2(0.955f, 0.98f), Vector2.zero, Vector2.zero);
            Image preview = imageRect.gameObject.AddComponent<Image>();
            preview.color = previews[i] != null ? Color.white : accents[i];
            preview.sprite = previews[i];
            preview.type = Image.Type.Simple;
            preview.preserveAspect = true;
            preview.raycastTarget = false;

            Text(card, names[i], 24, paper, new Vector2(0.03f, 0.265f), new Vector2(0.97f, 0.34f));
            Text(card, difficulties[i], 17, accents[i], new Vector2(0.03f, 0.215f), new Vector2(0.97f, 0.28f));
            Text(card, features[i], 15, hint, new Vector2(0.03f, 0.09f), new Vector2(0.97f, 0.22f));
            int capture = i;
            Button choose = Button("Choose", card, "CHON MAP", 16,
                new Vector2(0.14f, 0.01f), new Vector2(0.86f, 0.09f), accents[i]);
            choose.onClick.AddListener(() => SelectMap(capture));
            chooseLabels[i] = choose.GetComponentInChildren<TMP_Text>();
        }

        startButton = Button("StartMatch", screen.transform, "BAT DAU", 25,
            new Vector2(0.34f, 0.064f), new Vector2(0.66f, 0.16f),
            new Color(0.20f, 0.75f, 0.56f));
        startButton.onClick.AddListener(StartMatch);
        startLabel = startButton.GetComponentInChildren<TMP_Text>();
        SelectMap(0);
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        return rt;
    }

    private static RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max,
        Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        RectTransform rt = Rect(name, parent, min, max, offsetMin, offsetMax);
        Image image = rt.gameObject.AddComponent<Image>();
        image.color = color;
        return rt;
    }

    private static TMP_Text Text(Transform parent, string message, int size, Color color,
        Vector2 min, Vector2 max)
    {
        RectTransform rt = Rect("Text", parent, min, max, Vector2.zero, Vector2.zero);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = message;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        t.enableAutoSizing = true;
        t.fontSizeMin = 9;
        t.fontSizeMax = size;
        return t;
    }

    private static Button Button(string name, Transform parent, string label, int fontSize,
        Vector2 min, Vector2 max, Color color)
    {
        RectTransform rt = Panel(name, parent, min, max, Vector2.zero, Vector2.zero, color);
        Button button = rt.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.85f, 0.93f, 1f);
        colors.pressedColor = new Color(0.64f, 0.79f, 0.92f);
        colors.selectedColor = Color.white;
        button.colors = colors;
        Text(rt, label, fontSize, new Color(0.045f, 0.09f, 0.12f), Vector2.zero, Vector2.one);
        return button;
    }
}
