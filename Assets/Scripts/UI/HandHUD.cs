using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Balatro-style hand row along the bottom of the screen — one button per
// card in GridView's hand, fanned out and overlapping when there are many.
// Click a card to select it as the pending card (GridView.SelectCard); click
// the selected card again to deselect. The selected card pops upward and is
// tinted so it's obvious which one is "in hand, about to be played."
// Built entirely at runtime — no scene/prefab dependencies required.
public class HandHUD : MonoBehaviour
{
    [SerializeField] private GridView gridView;

    [Header("Layout")]
    [SerializeField] private float cardWidth      = 110f;
    [SerializeField] private float cardHeight     = 150f;
    [SerializeField] private float idealSpacing   = 90f;  // gap between card centers when hand is small
    [SerializeField] private float maxRowWidth    = 900f; // spacing shrinks (cards overlap) past this width
    [SerializeField] private float selectedYOffset = 36f;
    [SerializeField] private float bottomMargin    = 40f;

    [Header("Colors")]
    [SerializeField] private Color cardColor     = new(0.15f, 0.15f, 0.2f, 0.95f);
    [SerializeField] private Color selectedColor = new(0.25f, 0.55f, 0.35f, 1f);

    private RectTransform _row;
    private readonly List<GameObject> _cardViews = new();

    void Start()
    {
        if (gridView == null) gridView = FindFirstObjectByType<GridView>();

        EnsureEventSystem();
        BuildCanvas();

        gridView.OnHandChanged        += Rebuild;
        gridView.OnPendingCardChanged += RefreshHighlight;

        Rebuild();
    }

    void OnDestroy()
    {
        if (gridView == null) return;
        gridView.OnHandChanged        -= Rebuild;
        gridView.OnPendingCardChanged -= RefreshHighlight;
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;

        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    private void BuildCanvas()
    {
        var canvasGO = new GameObject("HandCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var rowGO = new GameObject("HandRow", typeof(RectTransform));
        rowGO.transform.SetParent(canvasGO.transform, false);
        _row = rowGO.GetComponent<RectTransform>();
        _row.anchorMin        = new Vector2(0.5f, 0f);
        _row.anchorMax        = new Vector2(0.5f, 0f);
        _row.pivot            = new Vector2(0.5f, 0f);
        _row.anchoredPosition = new Vector2(0f, bottomMargin);
        _row.sizeDelta        = new Vector2(maxRowWidth, cardHeight + selectedYOffset + 20f);
    }

    private void Rebuild()
    {
        foreach (var go in _cardViews) Destroy(go);
        _cardViews.Clear();

        if(gridView == null) return;
        var cards = gridView.HandCards;
        int count = cards.Count;
        if (count == 0) return;

        float spacing    = count <= 1 ? 0f : Mathf.Min(idealSpacing, maxRowWidth / count);
        float totalWidth = spacing * (count - 1) + cardWidth;
        float startX     = -totalWidth * 0.5f + cardWidth * 0.5f;

        for (int i = 0; i < count; i++)
        {
            var view = CreateCardButton(cards[i]);
            var rt   = view.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(startX + i * spacing, 0f);
            _cardViews.Add(view);
        }

        RefreshHighlight();
    }

    private GameObject CreateCardButton(CardInstance card)
    {
        var go = new GameObject($"Card_{card.Data.cardName}", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(_row, false);

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(cardWidth, cardHeight);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot     = new Vector2(0.5f, 0f);

        go.GetComponent<Image>().color = cardColor;

        var textGO = new GameObject("Label", typeof(RectTransform));
        textGO.transform.SetParent(go.transform, false);
        var textRt = textGO.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(4f, 4f);
        textRt.offsetMax = new Vector2(-4f, -4f);

        var text = textGO.AddComponent<TextMeshProUGUI>();
        text.fontSize  = 16;
        text.alignment = TextAlignmentOptions.Center;
        text.color     = Color.white;
        text.text      = $"{card.Data.cardName}\n{card.Data.manaCost} mana";

        go.GetComponent<Button>().onClick.AddListener(() => gridView.SelectCard(card));

        return go;
    }

    private void RefreshHighlight()
    {
        var cards = gridView.HandCards;
        for (int i = 0; i < _cardViews.Count && i < cards.Count; i++)
        {
            var view     = _cardViews[i];
            var rt       = view.GetComponent<RectTransform>();
            var img      = view.GetComponent<Image>();
            bool selected = cards[i] == gridView.PendingCard;

            var pos = rt.anchoredPosition;
            pos.y   = selected ? selectedYOffset : 0f;
            rt.anchoredPosition = pos;
            rt.localScale       = selected ? Vector3.one * 1.08f : Vector3.one;

            img.color = selected ? selectedColor : cardColor;

            if (selected) view.transform.SetAsLastSibling();
        }
    }
}
