using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopOverlay : MonoBehaviour
{
    [Header("Wired by the prefab (auto-built if empty)")]
    [SerializeField] private RectTransform panel;
    [SerializeField] private TMP_Text      moneyText;
    [SerializeField] private TMP_Text      rerollLabel;
    [SerializeField] private Button        rerollButton;
    [SerializeField] private Button        leaveButton;
    [SerializeField] private RectTransform cardRow;
    [SerializeField] private RectTransform landRow;

    [Header("Prefabs (leave empty to generate at runtime)")]
    [SerializeField] private GameObject cardPrefab;

    [Header("Sizing")]
    [SerializeField] private Vector2 shopCardSize = new(180f, 252f);
    [SerializeField] private Vector2 landSlotSize = new(210f, 96f);
    [SerializeField] private float   priceDrop    = 22f;

    private static readonly Vector2 PanelSize = new(1160f, 660f);

    private static readonly Color PanelColor  = new(0.09f, 0.10f, 0.14f, 0.98f);
    private static readonly Color SlotColor   = new(0.16f, 0.17f, 0.23f, 1f);
    private static readonly Color LandColor   = new(0.20f, 0.30f, 0.24f, 1f);
    private static readonly Color ButtonColor = new(0.24f, 0.26f, 0.34f, 1f);
    private static readonly Color GoldColor   = new(1f, 0.86f, 0.35f, 1f);

    private GameManager _gm;

    private readonly List<GameObject> _cardSlots   = new();
    private readonly List<GameObject> _landSlots   = new();
    private readonly List<CardView>   _cardViews   = new();
    private readonly List<Button>     _landButtons = new();

    public static ShopOverlay Create(GameManager gameManager, GameObject prefab)
    {
        var go = prefab != null ? Instantiate(prefab) : BuildOverlay();
        go.name = "ShopOverlay";

        var overlay = go.GetComponent<ShopOverlay>();
        if (overlay == null) overlay = go.AddComponent<ShopOverlay>();

        overlay.Bind(gameManager);
        return overlay;
    }

    private void Bind(GameManager gameManager)
    {
        _gm = gameManager;

        if (panel == null) WireParts();

        if (rerollButton != null)
        {
            rerollButton.onClick.RemoveAllListeners();
            rerollButton.onClick.AddListener(OnReroll);
        }

        if (leaveButton != null)
        {
            leaveButton.onClick.RemoveAllListeners();
            leaveButton.onClick.AddListener(OnLeave);
        }

        BuildSlots();
        Refresh();

        _gm.OnEconomyChanged += Refresh;
    }

    public void WireParts()
    {
        panel        = transform.Find("Panel") as RectTransform;
        moneyText    = Find<TMP_Text>("Panel/Money");
        cardRow      = transform.Find("Panel/CardRow") as RectTransform;
        landRow      = transform.Find("Panel/LandRow") as RectTransform;
        rerollButton = Find<Button>("Panel/Reroll");
        leaveButton  = Find<Button>("Panel/Leave");
        rerollLabel  = Find<TMP_Text>("Panel/Reroll/Label");
    }

    private T Find<T>(string path) where T : Component
    {
        var found = transform.Find(path);
        return found != null ? found.GetComponent<T>() : null;
    }

    void OnDestroy()
    {
        if (_gm != null) _gm.OnEconomyChanged -= Refresh;
    }

    private void BuildSlots()
    {
        foreach (var slot in _cardSlots) Retire(slot);
        foreach (var slot in _landSlots) Retire(slot);
        _cardSlots.Clear();
        _landSlots.Clear();
        _cardViews.Clear();
        _landButtons.Clear();

        var shop = _gm.CurrentShop;
        if (shop == null || cardRow == null || landRow == null) return;

        for (int i = 0; i < shop.buyCardsArea.Count; i++)
            _cardSlots.Add(BuildCardSlot(shop.buyCardsArea[i], i));

        for (int i = 0; i < shop.buyLandArea.Count; i++)
            _landSlots.Add(BuildLandSlot(shop.buyLandArea[i], i));
    }

    private void Retire(GameObject slot)
    {
        if (slot == null) return;
        slot.SetActive(false);
        Destroy(slot);
    }

    private GameObject MakeSlot(RectTransform row, string name, Vector2 size)
    {
        var slot = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
        slot.transform.SetParent(row, false);

        var element = slot.GetComponent<LayoutElement>();
        element.preferredWidth  = size.x;
        element.preferredHeight = size.y;
        return slot;
    }

    private GameObject BuildCardSlot(CardData card, int index)
    {
        var slot = MakeSlot(cardRow, $"CardSlot{index}", shopCardSize);
        var rt   = (RectTransform)slot.transform;

        if (card == null)
        {
            SoldOutPlaceholder(rt);
            return slot;
        }

        var binder = CardFactory.Create(cardPrefab, rt, card, shopCardSize, true);

        var view = binder.gameObject.AddComponent<CardView>();
        view.Init(binder, card, null, 18f, 18f, 1.07f, 1.07f, _ => OnBuyCard(index));
        view.SetSlot(Vector2.zero, 0f, index);
        _cardViews.Add(view);

        var price = MakeLabel(rt, "Price", "", 20f, TextAlignmentOptions.Center,
                              new Vector2(0.5f, 0f), new Vector2(0f, -priceDrop));
        price.color = GoldColor;

        return slot;
    }

    private GameObject BuildLandSlot(GameObject item, int index)
    {
        var slot = MakeSlot(landRow, $"LandSlot{index}", landSlotSize);
        var rt   = (RectTransform)slot.transform;

        if (item == null)
        {
            SoldOutPlaceholder(rt);
            _landButtons.Add(null);
            return slot;
        }

        var image = slot.AddComponent<Image>();
        image.sprite = UISprites.RoundedRect;
        image.type   = Image.Type.Sliced;
        image.color  = LandColor;

        var button = slot.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => OnBuyLand(index));
        _landButtons.Add(button);

        MakeLabel(rt, "Label", item.name, 18f, TextAlignmentOptions.Center,
                  new Vector2(0.5f, 0.5f), new Vector2(0f, 14f));

        var price = MakeLabel(rt, "Price", "", 18f, TextAlignmentOptions.Center,
                              new Vector2(0.5f, 0.5f), new Vector2(0f, -18f));
        price.color = GoldColor;

        return slot;
    }

    private void SoldOutPlaceholder(RectTransform parent)
    {
        var backing = CardFactory.Stretch(parent, "Empty", 0f).AddComponent<Image>();
        backing.sprite        = UISprites.RoundedRect;
        backing.type          = Image.Type.Sliced;
        backing.color         = SlotColor;
        backing.raycastTarget = false;

        var label = MakeLabel(parent, "SoldOut", "SOLD", 20f, TextAlignmentOptions.Center,
                              new Vector2(0.5f, 0.5f), Vector2.zero);
        label.color = new Color(0.45f, 0.47f, 0.55f, 1f);
    }

    private void OnBuyCard(int index)
    {
        var shop = _gm.CurrentShop;
        if (shop != null && shop.BuyCard(index, _gm))
        {
            Sfx.Buy();
            BuildSlots();
            Refresh();
        }
        else Sfx.Invalid();
    }

    private void OnBuyLand(int index)
    {
        var shop = _gm.CurrentShop;
        if (shop != null && shop.BuyLand(index, _gm))
        {
            Sfx.Buy();
            BuildSlots();
            Refresh();
        }
        else Sfx.Invalid();
    }

    private void OnReroll()
    {
        if (_gm.TryRerollShop())
        {
            Sfx.Gacha();
            BuildSlots();
            Refresh();
        }
        else Sfx.Invalid();
    }

    private void OnLeave()
    {
        Sfx.ButtonPress();
        _gm.CloseShop();
    }

    private void Refresh()
    {
        if (_gm == null) return;

        if (moneyText   != null) moneyText.text   = $"${_gm.money}";
        if (rerollLabel != null) rerollLabel.text = $"REROLL  ${_gm.ShopRerollCost}";
        if (rerollButton != null) rerollButton.interactable = _gm.CanAfford(_gm.ShopRerollCost);

        bool canAffordCard = _gm.CanAfford(_gm.BuyCardCost);
        foreach (var view in _cardViews)
        {
            if (view == null) continue;
            view.SetInteractable(canAffordCard);
            SetPrice(view.transform.parent, _gm.BuyCardCost);
        }

        bool canAffordLand = _gm.CanAfford(_gm.BuyLandCost);
        foreach (var button in _landButtons)
        {
            if (button == null) continue;
            button.interactable = canAffordLand;
            SetPrice(button.transform, _gm.BuyLandCost);
        }
    }

    private static void SetPrice(Transform slot, int cost)
    {
        var price = slot.Find("Price");
        if (price != null) price.GetComponent<TMP_Text>().text = $"${cost}";
    }

    public static GameObject BuildOverlay()
    {
        var root = new GameObject("ShopOverlay", typeof(Canvas), typeof(CanvasScaler),
                                  typeof(GraphicRaycaster), typeof(ShopOverlay));

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var blocker = CardFactory.Stretch(root.transform, "Blocker", 0f).AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0.72f);

        var panelGO = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panelGO.transform.SetParent(root.transform, false);

        var panelRt = panelGO.GetComponent<RectTransform>();
        panelRt.anchorMin = panelRt.anchorMax = panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = PanelSize;

        var panelImage = panelGO.GetComponent<Image>();
        panelImage.sprite = UISprites.RoundedRect;
        panelImage.type   = Image.Type.Sliced;
        panelImage.color  = PanelColor;

        StaticLabel(panelRt, "Title", "END OF WEEK — SHOP", 34f, TextAlignmentOptions.Left,
                    new Vector2(0f, 1f), new Vector2(36f, -44f));

        StaticLabel(panelRt, "Money", "$0", 28f, TextAlignmentOptions.Right,
                    new Vector2(1f, 1f), new Vector2(-36f, -44f)).color = GoldColor;

        StaticLabel(panelRt, "CardsHeader", "CARDS", 18f, TextAlignmentOptions.Left,
                    new Vector2(0f, 0.5f), new Vector2(36f, 226f))
            .color = new Color(0.6f, 0.65f, 0.75f, 1f);

        StaticLabel(panelRt, "LandHeader", "LAND", 18f, TextAlignmentOptions.Left,
                    new Vector2(0f, 0.5f), new Vector2(36f, -94f))
            .color = new Color(0.6f, 0.65f, 0.75f, 1f);

        BuildRow(panelRt, "CardRow", new Vector2(0f, 74f), new Vector2(1080f, 300f), 26f);
        BuildRow(panelRt, "LandRow", new Vector2(0f, -166f), new Vector2(1080f, 120f), 24f);

        StaticButton(panelRt, "Reroll", "REROLL", new Vector2(230f, 62f),
                     new Vector2(0f, 0f), new Vector2(150f, 42f));
        StaticButton(panelRt, "Leave", "LEAVE SHOP", new Vector2(230f, 62f),
                     new Vector2(1f, 0f), new Vector2(-150f, 42f));

        return root;
    }

    private static void BuildRow(RectTransform parent, string name, Vector2 position,
                                 Vector2 size, float spacing)
    {
        var rowGO = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
        rowGO.transform.SetParent(parent, false);

        var rt = rowGO.GetComponent<RectTransform>();
        rt.anchorMin        = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = size;
        rt.anchoredPosition = position;

        var layout = rowGO.GetComponent<HorizontalLayoutGroup>();
        layout.spacing                = spacing;
        layout.childAlignment         = TextAnchor.MiddleCenter;
        layout.childForceExpandWidth  = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth      = true;
        layout.childControlHeight     = true;
    }

    private TMP_Text MakeLabel(RectTransform parent, string name, string content, float fontSize,
                               TextAlignmentOptions alignment, Vector2 anchor, Vector2 position)
        => StaticLabel(parent, name, content, fontSize, alignment, anchor, position);

    private static TMP_Text StaticLabel(RectTransform parent, string name, string content, float fontSize,
                                        TextAlignmentOptions alignment, Vector2 anchor, Vector2 position)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = rt.anchorMax = anchor;
        rt.pivot            = new Vector2(anchor.x, 0.5f);
        rt.sizeDelta        = new Vector2(520f, 46f);
        rt.anchoredPosition = position;

        var text = go.AddComponent<TextMeshProUGUI>();
        text.text          = content;
        text.fontSize      = fontSize;
        text.fontStyle     = FontStyles.Bold;
        text.alignment     = alignment;
        text.color         = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static void StaticButton(RectTransform parent, string name, string content,
                                     Vector2 size, Vector2 anchor, Vector2 position)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = rt.anchorMax = anchor;
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = size;
        rt.anchoredPosition = position;

        var image = go.GetComponent<Image>();
        image.sprite = UISprites.RoundedRect;
        image.type   = Image.Type.Sliced;
        image.color  = ButtonColor;

        go.GetComponent<Button>().targetGraphic = image;

        StaticLabel(rt, "Label", content, 19f, TextAlignmentOptions.Center,
                    new Vector2(0.5f, 0.5f), Vector2.zero);
    }
}
