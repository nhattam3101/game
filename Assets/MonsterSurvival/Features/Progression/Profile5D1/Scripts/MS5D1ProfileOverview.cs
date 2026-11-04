using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 5D.1: A READ-ONLY projection of Save 5A + Upgrades 5B + Unlocks 5C.
// Never calls TrySave, TryPurchase, SelectSlot, equipment or inventory mutations.
[DisallowMultipleComponent]
public sealed class MS5D1ProfileOverview : MonoBehaviour
{
    [Header("Wired by 5D.1 Installer")]
    public Button launcher;
    public MS5A2ProfileWidget existingProfileWidget;

    private GameObject overlay;
    private CanvasGroup group;
    private TMP_Text title, gold, saveInfo, status, summary;
    private readonly TMP_Text[] upgrades = new TMP_Text[4];
    private readonly TMP_Text[] maps = new TMP_Text[3];
    private readonly TMP_Text[] badges = new TMP_Text[2];
    private readonly TMP_Text[] licenses = new TMP_Text[2];
    private TMP_Text inventory, weapon, armor, accessory;
    private bool showing;
    private int lastSlot = -1;
    private float nextRefresh;
    private bool subscribed;

    private static readonly Color Background = new Color(.055f, .043f, .100f, .99f);
    private static readonly Color Panel = new Color(.112f, .080f, .178f, .98f);
    private static readonly Color Row = new Color(.151f, .111f, .227f, .97f);
    private static readonly Color Pale = new Color(.94f, .90f, .99f, 1f);
    private static readonly Color Muted = new Color(.73f, .69f, .81f, 1f);
    private static readonly Color Gold = new Color(.96f, .74f, .39f, 1f);
    private static readonly Color Teal = new Color(.51f, .90f, .77f, 1f);
    private static readonly Color Alert = new Color(1f, .55f, .50f, 1f);

    private void Start()
    {
        if (!launcher || !GetComponentInParent<Canvas>())
        {
            Debug.LogError("[5D.1] Launcher or MainMenu Canvas missing. Use installer/audit.", this);
            enabled = false;
            return;
        }
        Build();
        launcher.onClick.AddListener(Open);
        subscribed = true;
    }

    private void Update()
    {
        if (!overlay || !showing) return;
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
#endif
        if (!launcher || !launcher.gameObject.activeInHierarchy) { Close(); return; }
        if (lastSlot != MS5A2GoldBank.SelectedSlot || Time.unscaledTime >= nextRefresh)
            Refresh();
    }

    public void Open()
    {
        if (!overlay) return;
        overlay.transform.SetAsLastSibling();
        overlay.SetActive(true);
        showing = true;
        group.alpha = 1f;
        group.blocksRaycasts = true;
        group.interactable = true;
        Refresh();
    }

    public void Close()
    {
        showing = false;
        if (group) { group.blocksRaycasts = false; group.interactable = false; }
        if (overlay) overlay.SetActive(false);
    }

    public void Refresh()
    {
        int slot = MS5A2GoldBank.SelectedSlot;
        lastSlot = slot;
        nextRefresh = Time.unscaledTime + .7f;
        title.text = "PROFILE " + slot.ToString("00") + "  /  03";
        MS5A1SaveData data;
        bool fromBackup;
        string message;
        bool saved = MS5A1SaveService.TryLoad(slot, out data, out message, out fromBackup);
        bool fresh = !saved && !string.IsNullOrEmpty(message) &&
                     message.StartsWith("NO_SAVE", StringComparison.Ordinal);
        if (!saved && !fresh)
        {
            gold.text = "BANK GOLD  --";
            saveInfo.text = "SAVE UNAVAILABLE";
            status.text = "SAVE ERROR / READ BLOCKED — Check Console or Manage Saves before continuing.";
            status.color = Alert;
            FillUnavailable();
            return;
        }
        if (fresh) data = MS5A1SaveService.CreateFresh(slot);
        gold.text = "BANK GOLD  " + data.bankGold.ToString("N0");
        saveInfo.text = fresh ? "NEW PROFILE / NO SAVE FILE" :
            "REV " + data.revision + "   ·   LAST SAVE " + FormatDate(data.lastSavedUtc);
        status.text = fromBackup ? "BACKUP ONLY — read-only; recovery can contain older progress." :
            fresh ? "NEW PROFILE — no file created by this overview." :
            "READ ONLY — displaying verified saved progress.";
        status.color = fromBackup ? Alert : Muted;

        MS5B1UpgradeDefinition[] definitions = MS5B1UpgradeDefinitions.GetAll();
        for (int i = 0; i < upgrades.Length; i++)
        {
            MS5B1UpgradeDefinition def = i < definitions.Length ? definitions[i] : null;
            if (def == null) { upgrades[i].text = "—"; continue; }
            int level = 0;
            if (data.permanentUpgrades != null)
                foreach (MS5A1UpgradeEntry entry in data.permanentUpgrades)
                    if (entry != null && entry.id == def.id) { level = Mathf.Clamp(entry.level, 0, def.maxLevel); break; }
            upgrades[i].text = def.title + "    LV " + level + "/" + def.maxLevel +
                "    ·    " + Bonus(def.id, level);
        }

        string[] mapIds = { "map.green_meadow", "map.sand_dunes", "map.ancient_ruins" };
        string[] mapNames = { "GREEN MEADOW", "SAND DUNES", "ANCIENT RUINS" };
        for (int i = 0; i < 3; i++)
        {
            bool owned = i == 0 || Has(data.unlockedContentIds, mapIds[i]);
            MS5C3MapPolicy.State access;
            bool valid = MS5C3MapPolicy.TryGet(slot, i, out access);
            string label = !valid ? "ACCESS UNKNOWN" : owned ? "UNLOCKED" :
                access.allowed && access.legacy ? "LEGACY ACCESS" : access.allowed ? "ACCESS ALLOWED" : "LOCKED";
            maps[i].text = mapNames[i] + "    ·    " + label;
            maps[i].color = !valid ? Alert : access.allowed ? Teal : Muted;
        }
        string[] bossIds = { "achievement.shadow_titan", "achievement.grave_warden" };
        string[] bossNames = { "TITAN SLAYER", "WARDEN BREAKER" };
        for (int i = 0; i < 2; i++)
        {
            bool earned = Has(data.unlockedContentIds, bossIds[i]);
            badges[i].text = bossNames[i] + "    ·    " + (earned ? "UNLOCKED" : "LOCKED");
            badges[i].color = earned ? Teal : Muted;
        }
        string[] licenseIds = { "equipment.reaper_edge", "equipment.wardens_oath" };
        string[] licenseNames = { "REAPER EDGE", "WARDEN'S OATH" };
        for (int i = 0; i < 2; i++)
        {
            bool earned = Has(data.unlockedContentIds, licenseIds[i]);
            licenses[i].text = licenseNames[i] + "    ·    " + (earned ? "LICENSED" : "LOCKED");
            licenses[i].color = earned ? Teal : Muted;
        }
        int occupied = 0, quantity = 0;
        if (data.inventorySnapshotPresent && data.inventorySlots != null)
            foreach (MS5A3SavedStack stack in data.inventorySlots)
                if (stack != null && !string.IsNullOrEmpty(stack.itemId) && stack.quantity > 0)
                { occupied++; quantity += stack.quantity; }
        inventory.text = !data.inventorySnapshotPresent ? "INVENTORY   ·   NO SNAPSHOT YET" :
            "INVENTORY   ·   " + occupied + "/20 SLOTS    ·    " + quantity + " ITEMS";
        weapon.text = "WEAPON       " + Equipped(data, 0);
        armor.text = "ARMOR           " + Equipped(data, 1);
        accessory.text = "ACCESSORY  " + Equipped(data, 2);
        int equipCount = 0;
        if (data.inventorySnapshotPresent && data.equippedItems != null)
            foreach (MS5A3SavedEquipped eq in data.equippedItems)
                if (eq != null && !string.IsNullOrEmpty(eq.itemId)) equipCount++;
        summary.text = (fresh ? "NEW PROFILE" : "SAVED PROFILE") + "   ·   " +
            occupied + " INVENTORY STACKS   ·   " + equipCount + "/3 EQUIPPED";
    }

    private static bool Has(List<string> ids, string id)
    { return ids != null && ids.Contains(id); }

    private static string Equipped(MS5A1SaveData data, int slot)
    {
        if (!data.inventorySnapshotPresent) return "NOT SAVED";
        if (data.equippedItems != null)
            foreach (MS5A3SavedEquipped eq in data.equippedItems)
                if (eq != null && eq.type == slot && !string.IsNullOrEmpty(eq.itemId))
                    return eq.itemId;
        return "EMPTY";
    }

    private static string Bonus(string id, int level)
    {
        switch (id)
        {
            case "vitality": return "+" + (level * 10) + " MAX HP";
            case "strength": return "+" + (level * 5) + "% DAMAGE";
            case "fury": return "+" + (level * 3) + "% ATTACK SPEED";
            case "agility": return "+" + (level * 2) + "% MOVE SPEED";
            default: return "—";
        }
    }

    private static string FormatDate(string value)
    {
        if (string.IsNullOrEmpty(value)) return "NOT SAVED";
        DateTime parsed;
        if (DateTime.TryParse(value, out parsed)) return parsed.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        return "UNKNOWN";
    }

    private void FillUnavailable()
    {
        for (int i = 0; i < upgrades.Length; i++) upgrades[i].text = "UPGRADE DATA UNAVAILABLE";
        for (int i = 0; i < maps.Length; i++) maps[i].text = "MAP ACCESS UNAVAILABLE";
        for (int i = 0; i < badges.Length; i++) badges[i].text = "ACHIEVEMENT DATA UNAVAILABLE";
        for (int i = 0; i < licenses.Length; i++) licenses[i].text = "LICENSE DATA UNAVAILABLE";
        inventory.text = "INVENTORY UNAVAILABLE";
        weapon.text = armor.text = accessory.text = "EQUIPMENT UNAVAILABLE";
        summary.text = "NO VERIFIED SAVE DATA";
    }

    private void Build()
    {
        overlay = Rect("MS5D1_OverlayRuntime", transform, Vector2.zero, Vector2.one).gameObject;
        group = overlay.AddComponent<CanvasGroup>();
        Surface("Dim", overlay.transform, Vector2.zero, Vector2.one, new Color(.015f, .010f, .033f, .96f), true);
        RectTransform panel = Rect("ProfilePanel", overlay.transform, new Vector2(.075f, .06f), new Vector2(.925f, .94f));
        Surface("Background", panel, Vector2.zero, Vector2.one, Background);
        Surface("GoldLine", panel, new Vector2(.025f, .972f), new Vector2(.975f, .983f), Gold);
        title = Label("PROFILE", panel, new Vector2(.04f, .865f), new Vector2(.48f, .95f), 42, Pale);
        gold = Label("BANK GOLD", panel, new Vector2(.53f, .878f), new Vector2(.88f, .945f), 29, Gold);
        saveInfo = Label("REV", panel, new Vector2(.53f, .823f), new Vector2(.93f, .887f), 16, Muted);
        Button close = MakeButton("Close", panel, "X", new Vector2(.935f, .882f), new Vector2(.977f, .958f));
        close.onClick.AddListener(Close);
        summary = Label("SAVED PROFILE", panel, new Vector2(.04f, .813f), new Vector2(.515f, .866f), 17, Muted);

        RectTransform leftTop = Section(panel, "Permanent Upgrades", new Vector2(.04f, .47f), new Vector2(.485f, .795f));
        SectionHeader("PERMANENT UPGRADES", leftTop);
        for (int i = 0; i < 4; i++)
        {
            float top = .82f - .19f * i;
            Surface("UpgradeRow" + i, leftTop, new Vector2(.035f, top-.17f), new Vector2(.965f, top), Row);
            upgrades[i] = Label("UPGRADE", leftTop, new Vector2(.07f, top-.17f), new Vector2(.95f, top), 18, Pale);
        }
        RectTransform leftBottom = Section(panel, "Maps", new Vector2(.04f, .115f), new Vector2(.485f, .45f));
        SectionHeader("MAP ACCESS / PROFILE POLICY", leftBottom);
        for (int i = 0; i < 3; i++)
        {
            float top = .82f - .247f * i;
            Surface("MapRow" + i, leftBottom, new Vector2(.035f, top-.22f), new Vector2(.965f, top), Row);
            maps[i] = Label("MAP", leftBottom, new Vector2(.065f, top-.22f), new Vector2(.945f, top), 18, Pale);
        }

        RectTransform rightTop = Section(panel, "Achievements", new Vector2(.505f, .51f), new Vector2(.96f, .795f));
        SectionHeader("BOSS ACHIEVEMENTS", rightTop);
        for (int i = 0; i < 2; i++)
        {
            float top = .8f - .35f * i;
            Surface("BadgeRow" + i, rightTop, new Vector2(.035f, top-.29f), new Vector2(.965f, top), Row);
            badges[i] = Label("BOSS", rightTop, new Vector2(.065f, top-.29f), new Vector2(.95f, top), 19, Pale);
        }
        RectTransform rightBottom = Section(panel, "InventoryEquipment", new Vector2(.505f, .115f), new Vector2(.96f, .49f));
        SectionHeader("INVENTORY / EQUIPMENT", rightBottom);
        inventory = Label("INVENTORY", rightBottom, new Vector2(.055f, .745f), new Vector2(.96f, .86f), 19, Gold);
        weapon = Label("WEAPON", rightBottom, new Vector2(.055f, .605f), new Vector2(.96f, .72f), 17, Pale);
        armor = Label("ARMOR", rightBottom, new Vector2(.055f, .48f), new Vector2(.96f, .595f), 17, Pale);
        accessory = Label("ACCESSORY", rightBottom, new Vector2(.055f, .355f), new Vector2(.96f, .47f), 17, Pale);
        Surface("Divider", rightBottom, new Vector2(.04f, .328f), new Vector2(.96f, .334f), Gold);
        for (int i = 0; i < 2; i++)
        {
            float top = .30f - i * .135f;
            licenses[i] = Label("LICENSE", rightBottom, new Vector2(.055f, top-.125f), new Vector2(.96f, top), 16, Muted);
        }
        status = Label("READ ONLY", panel, new Vector2(.05f, .025f), new Vector2(.95f, .098f), 16, Muted);
        overlay.SetActive(false);
    }

    private static RectTransform Section(Transform parent, string id, Vector2 min, Vector2 max)
    {
        RectTransform rect = Rect(id, parent, min, max);
        Surface("SectionBackground", rect, Vector2.zero, Vector2.one, Panel);
        return rect;
    }
    private static void SectionHeader(string text, Transform parent)
    {
        Label(text, parent, new Vector2(.045f,.85f), new Vector2(.965f,.99f), 20, Gold);
    }
    private static RectTransform Rect(string id, Transform parent, Vector2 min, Vector2 max)
    {
        GameObject go = new GameObject(id, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = min; r.anchorMax = max; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        return r;
    }
    private static Image Surface(string id, Transform parent, Vector2 min, Vector2 max, Color color, bool raycast = false)
    {
        RectTransform r = Rect(id, parent, min, max);
        Image image = r.gameObject.AddComponent<Image>();
        image.color = color; image.raycastTarget = raycast;
        return image;
    }
    private static TMP_Text Label(string name, Transform parent, Vector2 min, Vector2 max, int fontSize, Color color)
    {
        RectTransform r = Rect("TXT_" + name.Replace(' ','_'), parent, min, max);
        TextMeshProUGUI text = r.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = name; text.fontSize = fontSize; text.fontSizeMin = 10; text.fontSizeMax = fontSize;
        text.enableAutoSizing = true; text.alignment = TextAlignmentOptions.MidlineLeft;
        text.color = color; text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        if (TMP_Settings.defaultFontAsset) text.font = TMP_Settings.defaultFontAsset;
        return text;
    }
    private static Button MakeButton(string id, Transform parent, string caption, Vector2 min, Vector2 max)
    {
        RectTransform r = Rect(id, parent, min, max);
        Image image = r.gameObject.AddComponent<Image>();
        image.color = new Color(.31f, .19f, .43f, 1f); image.raycastTarget = true;
        Button b = r.gameObject.AddComponent<Button>(); b.targetGraphic = image;
        ColorBlock cb = b.colors; cb.highlightedColor = new Color(1f,.83f,.98f,1f); cb.pressedColor = Gold; b.colors = cb;
        TMP_Text t = Label(caption, r, Vector2.zero, Vector2.one, 25, Pale);
        t.alignment = TextAlignmentOptions.Center;
        return b;
    }
    private void OnDestroy()
    {
        if (subscribed && launcher) launcher.onClick.RemoveListener(Open);
    }
}
