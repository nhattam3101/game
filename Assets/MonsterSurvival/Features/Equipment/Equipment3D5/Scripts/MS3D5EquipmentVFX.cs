using UnityEngine;
using UnityEngine.UI;

// Visual-only effects for the 3D.1 Equipment Window. Uses existing EquipmentChanged event.
// Does NOT add button listeners, alter inventory contents, stats, UI interactability or Time.timeScale.
[DisallowMultipleComponent]
public sealed class MS3D5EquipmentVFX : MonoBehaviour
{
    public MS3DEquipmentUI equipmentUI;
    public MS3DPlayerEquipment equipment;
    public MS3D5WorldEquipBurst playerWorldFX;
    public Image[] rarityGlows = new Image[3];
    public Image[] pulseRings = new Image[3];
    [Range(0f, 1f)] public float idleGlowAlpha = 0.18f;
    [Range(0.1f, 2f)] public float pulseDuration = 0.48f;
    [Range(0f, 1f)] public float pulseAlpha = 0.82f;
    [Range(0f, 1f)] public float breathingAmount = 0.24f;

    private readonly MS3AItemData[] lastItems = new MS3AItemData[3];
    private readonly float[] pulseTimes = new float[3];
    private readonly Color[] flashColors = new Color[3];
    private bool subscribed;
    private float clock;

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        if (!equipmentUI) equipmentUI = GetComponent<MS3DEquipmentUI>();
        if (!equipment && equipmentUI) equipment = equipmentUI.equipment;
        if (equipment && !subscribed)
        {
            equipment.EquipmentChanged += OnEquipmentChanged;
            subscribed = true;
        }
        // No burst on opening Inventory: remember what's already worn.
        for (int i = 0; i < 3; i++)
        {
            lastItems[i] = equipment ? equipment.GetEquipped((MS3DEquipmentType)i) : null;
            pulseTimes[i] = 0f;
        }
        RefreshVisuals();
    }

    private void OnDisable()
    {
        if (subscribed && equipment) equipment.EquipmentChanged -= OnEquipmentChanged;
        subscribed = false;
        for (int i = 0; i < 3; i++)
        {
            if (pulseRings != null && i < pulseRings.Length && pulseRings[i])
                pulseRings[i].color = Color.clear;
        }
    }

    private void OnEquipmentChanged()
    {
        if (!equipment) return;
        for (int i = 0; i < 3; i++)
        {
            MS3AItemData now = equipment.GetEquipped((MS3DEquipmentType)i);
            MS3AItemData before = lastItems[i];
            if (now == before) continue;
            MS3AItemData changed = now ? now : before;
            MS3DQuality quality = QualityOf(changed);
            Color tint = MS3DEquipmentCatalog.QualityColor(quality);
            pulseTimes[i] = Mathf.Max(0.1f, pulseDuration);
            flashColors[i] = tint;
            if (playerWorldFX)
                playerWorldFX.Play(tint, now != null, quality == MS3DQuality.Legendary ? 2 : 1);
            lastItems[i] = now;
        }
        RefreshVisuals();
    }

    private MS3DQuality QualityOf(MS3AItemData item)
    {
        MS3DEquipmentDefinition row = null;
        return item && equipment && equipment.catalog && equipment.catalog.TryGet(item, out row) && row != null
            ? row.quality : MS3DQuality.Common;
    }

    private void RefreshVisuals()
    {
        for (int i = 0; i < 3; i++)
        {
            if (rarityGlows == null || i >= rarityGlows.Length || !rarityGlows[i]) continue;
            MS3AItemData item = equipment ? equipment.GetEquipped((MS3DEquipmentType)i) : null;
            Color color = item ? MS3DEquipmentCatalog.QualityColor(QualityOf(item)) : new Color(.39f, .29f, .56f);
            color.a = item ? idleGlowAlpha : .045f;
            rarityGlows[i].color = color;
            rarityGlows[i].raycastTarget = false;
        }
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        float dt = Mathf.Min(Time.unscaledDeltaTime, .10f);
        clock += dt;
        for (int i = 0; i < 3; i++)
        {
            Image glow = rarityGlows != null && i < rarityGlows.Length ? rarityGlows[i] : null;
            Image ring = pulseRings != null && i < pulseRings.Length ? pulseRings[i] : null;
            MS3AItemData item = equipment ? equipment.GetEquipped((MS3DEquipmentType)i) : null;
            if (glow)
            {
                Color c = item ? MS3DEquipmentCatalog.QualityColor(QualityOf(item)) : new Color(.39f,.29f,.56f);
                float breathing = 1f + breathingAmount * Mathf.Sin(clock * 2.05f + i * .8f);
                c.a = item ? idleGlowAlpha * breathing : .045f;
                glow.color = c;
                glow.raycastTarget = false;
            }
            if (ring)
            {
                pulseTimes[i] = Mathf.Max(0f, pulseTimes[i] - dt);
                float remaining = pulseTimes[i] / Mathf.Max(.10f, pulseDuration);
                Color c = flashColors[i];
                c.a = pulseAlpha * remaining * remaining;
                ring.color = c;
                ring.raycastTarget = false;
                float s = Mathf.Lerp(1.4f, .82f, 1f - remaining);
                ring.rectTransform.localScale = new Vector3(s, s, 1f);
            }
        }
    }
}
