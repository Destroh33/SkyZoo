using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopOverlay : MonoBehaviour
{
    [Header("Theme")]
    [SerializeField] private UITheme theme;

    [Header("Wired by the prefab (auto-built if empty)")]
    [SerializeField] private RectTransform panel;
    [SerializeField] private StatChip      moneyChip;
    [SerializeField] private ChunkyButton  rerollButton;
    [SerializeField] private ChunkyButton  leaveButton;
    [SerializeField] private RectTransform cardRow;
    [SerializeField] private RectTransform landRow;

    [Header("Slot prefabs (leave empty to generate at runtime)")]
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private GameObject landButtonPrefab;

    [Header("Sizing")]
    [SerializeField] private Vector2 shopCardSize = new(180f, 252f);
    [SerializeField] private Vector2 landSlotSize = new(210f, 116f);
    [SerializeField] private float   priceDrop    = 22f;

    private static readonly Vector2 PanelSize = new(1160f, 680f);

    private GameManager _gm;

    private readonly List<GameObject>   _cardSlots   = new();
    private readonly List<GameObject>   _landSlots   = new();
    private readonly List<CardView>     _cardViews   = new();
    private readonly List<ChunkyButton> _landButtons = new();

    private UITheme Theme => theme != null ? theme : theme = UITheme.Active;

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

        if (rerollButton != null) rerollButton.SetOnClick(OnReroll);
        if (leaveButton  != null) leaveButton.SetOnClick(OnLeave);
        if (moneyChip    != null) moneyChip.SetPrefix("$");

        BuildSlots();
        Refresh();

        Sfx.RewardAppear();
        _gm.OnEconomyChanged += Refresh;
    }

    public void SetSlotPrefabs(GameObject card, GameObject landButton)
    {
        cardPrefab       = card;
        landButtonPrefab = landButton;
    }

    public void WireParts()
    {
        panel        = transform.Find("Panel") as RectTransform;
        cardRow      = transform.Find("Panel/CardRow") as RectTransform;
        landRow      = transform.Find("Panel/LandRow") as RectTransform;
        moneyChip    = Find<StatChip>("Panel/Chip_Money");
        rerollButton = Find<ChunkyButton>("Panel/Button_Reroll");
        leaveButton  = Find<ChunkyButton>("Panel/Button_Leave");
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

        PriceLabel(rt, new Vector2(0.5f, 0f), new Vector2(0f, -priceDrop));
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

        var button = ChunkyButton.Create(landButtonPrefab, rt, item.name,
                                         new Vector2(landSlotSize.x, landSlotSize.y - 34f),
                                         Theme, Theme.quota);
        button.Rect.anchoredPosition = new Vector2(0f, 14f);
        button.SetOnClick(() => OnBuyLand(index));
        _landButtons.Add(button);

        PriceLabel(rt, new Vector2(0.5f, 0f), new Vector2(0f, 14f));
        return slot;
    }

    private TMP_Text PriceLabel(RectTransform parent, Vector2 anchor, Vector2 position)
    {
        var price = Label(parent, Theme, "Price", "", UITheme.Role.Number, 20f, Theme.money,
                          TextAlignmentOptions.Center, anchor, position);
        return price;
    }

    private void SoldOutPlaceholder(RectTransform parent)
    {
        var backing = CardFactory.Stretch(parent, "Empty", 0f).AddComponent<Image>();
        backing.sprite        = Theme.PanelShape;
        backing.type          = StatChip.SpriteType(Theme.PanelShape);
        backing.color         = Theme.panel;
        backing.raycastTarget = false;

        Label(parent, Theme, "SoldOut", Theme.Label("Sold"), UITheme.Role.Display, 20f, Theme.textMuted,
              TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero);
    }

    private void OnBuyCard(int index)
    {
        var shop = _gm.CurrentShop;
        if (shop != null && shop.BuyCard(index, _gm))
        {
            Sfx.Buy();
            if (moneyChip != null) moneyChip.Punch(0.24f);
            BuildSlots();
            Refresh();
        }
        else
        {
            Sfx.Invalid();
            if (moneyChip != null) moneyChip.Flash(Theme.danger);
        }
    }

    private void OnBuyLand(int index)
    {
        var shop = _gm.CurrentShop;
        if (shop != null && shop.BuyLand(index, _gm))
        {
            Sfx.Buy();
            if (moneyChip != null) moneyChip.Punch(0.24f);
            BuildSlots();
            Refresh();
        }
        else
        {
            Sfx.Invalid();
            if (moneyChip != null) moneyChip.Flash(Theme.danger);
        }
    }

    private void OnReroll()
    {
        if (_gm.TryRerollShop())
        {
            Sfx.Gacha();
            if (moneyChip != null) moneyChip.Punch(0.24f);
            BuildSlots();
            Refresh();
        }
        else
        {
            Sfx.Invalid();
            if (moneyChip != null) moneyChip.Flash(Theme.danger);
        }
    }

    private void OnLeave() => _gm.CloseShop();

    private void Refresh()
    {
        if (_gm == null) return;

        if (moneyChip != null) moneyChip.SetNumber(_gm.money);

        if (rerollButton != null)
        {
            rerollButton.SetLabel($"Reroll  ${_gm.ShopRerollCost}");
            rerollButton.SetInteractable(_gm.CanAfford(_gm.ShopRerollCost));
        }

        bool canAffordCard = _gm.CanAfford(_gm.BuyCardCost);
        foreach (var view in _cardViews)
        {
            if (view == null) continue;
            view.SetInteractable(canAffordCard);
            SetPrice(view.transform.parent, _gm.BuyCardCost);
        }

        bool canAffordLand = _gm.CanAfford(_gm.BuyLandCost);
        for (int i = 0; i < _landButtons.Count; i++)
        {
            if (_landButtons[i] == null) continue;
            _landButtons[i].SetInteractable(canAffordLand);
            if (i < _landSlots.Count && _landSlots[i] != null)
                SetPrice(_landSlots[i].transform, _gm.BuyLandCost);
        }
    }

    private static void SetPrice(Transform slot, int cost)
    {
        var price = slot.Find("Price");
        if (price != null) price.GetComponent<TMP_Text>().text = $"${cost}";
    }

    public static GameObject BuildOverlay()
    {
        var theme = UITheme.Active;

        var root = new GameObject("ShopOverlay", typeof(Canvas), typeof(CanvasScaler),
                                  typeof(GraphicRaycaster), typeof(ShopOverlay));

        var overlay = root.GetComponent<ShopOverlay>();
        overlay.theme = theme;

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var blocker = CardFactory.Stretch(root.transform, "Blocker", 0f).AddComponent<Image>();
        blocker.color = new Color(theme.ink.r, theme.ink.g, theme.ink.b, 0.78f);

        var panelGO = new GameObject("Panel", typeof(RectTransform));
        panelGO.transform.SetParent(root.transform, false);

        var panelRt = panelGO.GetComponent<RectTransform>();
        panelRt.anchorMin = panelRt.anchorMax = panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = PanelSize;

        var shadow = Layer(panelRt, "Shadow", theme.PanelShape, theme.shadow, -theme.outlineWidth);
        shadow.rectTransform.anchoredPosition = new Vector2(0f, -theme.shadowDrop * 1.6f);

        Layer(panelRt, "Outline", theme.PanelShape, theme.ink, -theme.outlineWidth * 1.4f);
        Layer(panelRt, "Fill",    theme.PanelShape, theme.panel, 0f);

        Label(panelRt, theme, "Title", theme.Label("End of week — shop"), UITheme.Role.Display, 34f,
              theme.textStrong, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(36f, -48f));

        overlay.moneyChip = StatChip.Build(panelRt, theme, "Money", theme.MoneyIcon, theme.money,
                                           new Vector2(250f, 78f), false);
        overlay.moneyChip.Rect.anchorMin        = overlay.moneyChip.Rect.anchorMax =
        overlay.moneyChip.Rect.pivot            = new Vector2(1f, 1f);
        overlay.moneyChip.Rect.anchoredPosition = new Vector2(-30f, -26f);

        Label(panelRt, theme, "CardsHeader", theme.Label("Cards"), UITheme.Role.Body, 18f, theme.textMuted,
              TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(36f, 232f));

        Label(panelRt, theme, "LandHeader", theme.Label("Land"), UITheme.Role.Body, 18f, theme.textMuted,
              TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(36f, -92f));

        overlay.cardRow = BuildRow(panelRt, "CardRow", new Vector2(0f, 80f), new Vector2(1080f, 300f), 26f);
        overlay.landRow = BuildRow(panelRt, "LandRow", new Vector2(0f, -168f), new Vector2(1080f, 140f), 24f);

        overlay.rerollButton = ChunkyButton.Build(panelRt, theme, "Reroll", theme.paths,
                                                  new Vector2(250f, 68f));
        overlay.rerollButton.Rect.anchorMin = overlay.rerollButton.Rect.anchorMax =
        overlay.rerollButton.Rect.pivot     = new Vector2(0f, 0f);
        overlay.rerollButton.Rect.anchoredPosition = new Vector2(36f, 42f);

        overlay.leaveButton = ChunkyButton.Build(panelRt, theme, "Leave Shop", theme.quota,
                                                 new Vector2(250f, 68f));
        overlay.leaveButton.Rect.anchorMin = overlay.leaveButton.Rect.anchorMax =
        overlay.leaveButton.Rect.pivot     = new Vector2(1f, 0f);
        overlay.leaveButton.Rect.anchoredPosition = new Vector2(-36f, 42f);
        overlay.leaveButton.SetPulsing(true);

        overlay.panel = panelRt;
        return root;
    }

    private static Image Layer(RectTransform parent, string name, Sprite sprite, Color color, float inset)
    {
        var image = CardFactory.Stretch(parent, name, inset).AddComponent<Image>();
        image.sprite        = sprite;
        image.type          = StatChip.SpriteType(sprite);
        image.color         = color;
        image.raycastTarget = false;
        return image;
    }

    private static RectTransform BuildRow(RectTransform parent, string name, Vector2 position,
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
        return rt;
    }

    private static TMP_Text Label(RectTransform parent, UITheme theme, string name, string content,
                                  UITheme.Role role, float fontSize, Color color,
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
        text.text      = content;
        text.alignment = alignment;
        theme.Apply(text, role, fontSize, color);
        return text;
    }
}
