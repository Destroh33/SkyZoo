using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class HandHUD : MonoBehaviour
{
    [SerializeField] private GridView    gridView;
    [SerializeField] private GameManager gameManager;

    [Header("Prefabs (leave empty to generate at runtime)")]
    [SerializeField] private GameObject handCanvasPrefab;
    [SerializeField] private GameObject cardPrefab;

    [Header("Layout")]
    [SerializeField] private float cardWidth    = 128f;
    [SerializeField] private float cardHeight   = 180f;
    [SerializeField] private float idealSpacing = 110f;
    [SerializeField] private float maxRowWidth  = 900f;
    [SerializeField] private float bottomMargin = 26f;

    [Header("Fan")]
    [SerializeField] private float anglePerCard = 5.5f;
    [SerializeField] private float maxFanAngle  = 22f;
    [SerializeField] private float arcHeight    = 30f;

    [Header("Pop")]
    [SerializeField] private float hoverLift   = 40f;
    [SerializeField] private float selectLift  = 68f;
    [SerializeField] private float hoverScale  = 1.14f;
    [SerializeField] private float selectScale = 1.28f;

    private RectTransform _row;
    private readonly List<CardView> _cardViews = new();

    void Start()
    {
        if (gridView == null) gridView = FindAnyObjectByType<GridView>();
        if (gameManager == null)
            gameManager = GameManager.instance != null ? GameManager.instance : FindAnyObjectByType<GameManager>();

        EnsureEventSystem();
        SetUpCanvas();

        if (gameManager != null) gameManager.OnHandChanged += Rebuild;
        if (gridView    != null) gridView.OnPendingCardChanged += RefreshHighlight;

        Rebuild();
    }

    void OnDestroy()
    {
        if (gameManager != null) gameManager.OnHandChanged -= Rebuild;
        if (gridView    != null) gridView.OnPendingCardChanged -= RefreshHighlight;
    }

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;

        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    private void SetUpCanvas()
    {
        GameObject canvasGO;

        if (handCanvasPrefab != null)
        {
            canvasGO = Instantiate(handCanvasPrefab, transform);
            canvasGO.name = "HandCanvas";
            var found = canvasGO.transform.Find("HandRow");
            if (found == null)
            {
                Debug.LogError("[SkyZoo] Hand canvas prefab has no child named 'HandRow'.");
                return;
            }
            _row = (RectTransform)found;
        }
        else
        {
            canvasGO = BuildCanvas(transform);
            _row     = (RectTransform)canvasGO.transform.Find("HandRow");
        }

        _row.anchoredPosition = new Vector2(0f, bottomMargin + cardHeight * 0.5f);
        _row.sizeDelta        = new Vector2(maxRowWidth, cardHeight);
    }

    public static GameObject BuildCanvas(Transform parent)
    {
        var canvasGO = new GameObject("HandCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        if (parent != null) canvasGO.transform.SetParent(parent, false);

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var rowGO = new GameObject("HandRow", typeof(RectTransform));
        rowGO.transform.SetParent(canvasGO.transform, false);

        var row = rowGO.GetComponent<RectTransform>();
        row.anchorMin = new Vector2(0.5f, 0f);
        row.anchorMax = new Vector2(0.5f, 0f);
        row.pivot     = new Vector2(0.5f, 0f);

        return canvasGO;
    }

    private void Rebuild()
    {
        if (gameManager == null || _row == null) return;

        var cards = gameManager.HandCards;
        int count = cards.Count;

        var stale = new List<CardView>(_cardViews);
        _cardViews.Clear();

        float spacing = count <= 1 ? 0f : Mathf.Min(idealSpacing, (maxRowWidth - cardWidth) / (count - 1));
        float fan     = Mathf.Min(anglePerCard * (count - 1), maxFanAngle * 2f) * 0.5f;

        int dealIndex = 0;
        for (int i = 0; i < count; i++)
        {
            float t = count <= 1 ? 0f : i / (float)(count - 1) * 2f - 1f;

            var view = stale.Find(v => v != null && v.Instance == cards[i]);
            int deal = 0;
            if (view != null) stale.Remove(view);
            else            { view = CreateCard(cards[i]); deal = dealIndex++; }

            view.transform.SetSiblingIndex(i);
            view.SlotIndex = i;
            view.SetSlot(new Vector2(t * spacing * (count - 1) * 0.5f, -arcHeight * t * t), -t * fan, deal);
            _cardViews.Add(view);
        }

        foreach (var view in stale) if (view != null) Destroy(view.gameObject);

        RefreshHighlight();
    }

    private CardView CreateCard(CardInstance card)
    {
        var binder = CardFactory.Create(cardPrefab, _row, card.Data,
                                        new Vector2(cardWidth, cardHeight));

        var view = binder.gameObject.AddComponent<CardView>();
        view.Init(binder, card.Data, card, hoverLift, selectLift, hoverScale, selectScale, OnCardClicked);
        return view;
    }

    private void OnCardClicked(CardView view)
    {
        Sfx.CardSelect();
        gridView.SelectCard(view.Instance);
    }

    private void RefreshHighlight()
    {
        if (gridView == null) return;

        foreach (var view in _cardViews)
        {
            if (view == null) continue;
            bool selected = view.Instance == gridView.PendingCard;
            view.SetSelected(selected);

            if (selected) view.transform.SetAsLastSibling();
            else          view.RestoreSiblingOrder();
        }
    }
}
