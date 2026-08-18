using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class CardFactory
{
    public static readonly Vector2 ReferenceSize = new(180f, 252f);

    public static CardBinder Create(GameObject prefab, Transform parent, CardData data,
                                    Vector2 size, bool showDescription)
    {
        var binder = prefab != null
            ? Object.Instantiate(prefab, parent).GetComponent<CardBinder>()
            : Build(parent);

        if (binder == null)
        {
            Debug.LogError("[SkyZoo] Card prefab is missing its CardBinder component.");
            return null;
        }

        binder.name = $"Card_{data.cardName}";
        binder.SetSize(size);
        binder.Bind(data);
        binder.ShowDescription(showDescription);
        return binder;
    }

    public static CardBinder Build(Transform parent)
    {
        var root = new GameObject("Card", typeof(RectTransform), typeof(CanvasGroup), typeof(CardBinder));
        root.transform.SetParent(parent, false);

        var rootRt = root.GetComponent<RectTransform>();
        rootRt.sizeDelta = ReferenceSize;
        rootRt.anchorMin = rootRt.anchorMax = rootRt.pivot = new Vector2(0.5f, 0.5f);

        var binder = root.GetComponent<CardBinder>();
        binder.rect            = rootRt;
        binder.group           = root.GetComponent<CanvasGroup>();
        binder.referenceHeight = ReferenceSize.y;
        binder.fallbackArt     = UISprites.RoundedRect;

        var glowGO = Stretch(root.transform, "Glow", -11f);
        binder.glow = glowGO.AddComponent<Image>();
        binder.glow.sprite        = UISprites.RoundedRect;
        binder.glow.type          = Image.Type.Sliced;
        binder.glow.color         = new Color(binder.glowColor.r, binder.glowColor.g, binder.glowColor.b, 0f);
        binder.glow.raycastTarget = false;

        var frameGO = Stretch(root.transform, "Frame", 0f);
        binder.frame = frameGO.AddComponent<Image>();
        binder.frame.sprite        = UISprites.RoundedRect;
        binder.frame.type          = Image.Type.Sliced;
        binder.frame.color         = binder.frameColor;
        binder.frame.raycastTarget = true;

        var artGO = Region(frameGO.transform, "Art", 0.48f, 1f, 8f);
        binder.art = artGO.AddComponent<Image>();
        binder.art.sprite        = UISprites.RoundedRect;
        binder.art.type          = Image.Type.Sliced;
        binder.art.raycastTarget = false;

        binder.initial = AddText(Stretch(artGO.transform, "Initial", 0f), "A", 54f,
                                 TextAlignmentOptions.Center);
        binder.initial.color = new Color(1f, 1f, 1f, 0.75f);

        binder.nameText = AddText(Region(frameGO.transform, "Name", 0.36f, 0.48f, 5f),
                                  "Card Name", 17f, TextAlignmentOptions.Center);

        binder.descriptionText = AddText(Region(frameGO.transform, "Description", 0.04f, 0.36f, 8f),
                                         "Description", 13f, TextAlignmentOptions.Top);
        binder.descriptionText.color = new Color(0.82f, 0.84f, 0.9f, 1f);

        BuildCostBadge(frameGO.transform, binder);

        return binder;
    }

    private static void BuildCostBadge(Transform parent, CardBinder binder)
    {
        var badgeGO = new GameObject("CostBadge", typeof(RectTransform));
        badgeGO.transform.SetParent(parent, false);

        var rt = badgeGO.GetComponent<RectTransform>();
        rt.anchorMin        = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(38f, 38f);
        rt.anchoredPosition = new Vector2(20f, -20f);

        var badge = badgeGO.AddComponent<Image>();
        badge.sprite        = UISprites.Circle;
        badge.color         = new Color(0.16f, 0.35f, 0.72f, 1f);
        badge.raycastTarget = false;

        binder.costText = AddText(Stretch(badgeGO.transform, "Value", 0f), "1", 22f,
                                  TextAlignmentOptions.Center);
    }

    private static TMP_Text AddText(GameObject host, string content, float fontSize,
                                    TextAlignmentOptions alignment)
    {
        var text = host.AddComponent<TextMeshProUGUI>();
        text.text             = content;
        text.fontSize         = fontSize;
        text.fontStyle        = FontStyles.Bold;
        text.alignment        = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.color            = Color.white;
        text.raycastTarget    = false;
        return text;
    }

    public static GameObject Stretch(Transform parent, string name, float inset)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
        return go;
    }

    public static GameObject Region(Transform parent, string name, float yMin, float yMax, float pad)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, yMin);
        rt.anchorMax = new Vector2(1f, yMax);
        rt.offsetMin = new Vector2(pad, pad);
        rt.offsetMax = new Vector2(-pad, -pad);
        return go;
    }
}
