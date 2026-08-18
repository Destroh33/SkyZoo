using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardBinder : MonoBehaviour
{
    [Header("Parts")]
    public RectTransform rect;
    public CanvasGroup   group;
    public Image         frame;
    public Image         glow;
    public Image         art;
    public TMP_Text      initial;
    public TMP_Text      nameText;
    public TMP_Text      descriptionText;
    public TMP_Text      costText;

    [Header("Colors")]
    public Color frameColor    = new(0.13f, 0.14f, 0.19f, 1f);
    public Color selectedColor = new(0.22f, 0.52f, 0.34f, 1f);
    public Color glowColor     = new(1f, 0.92f, 0.45f, 1f);

    [Header("Layout")]
    public float referenceHeight              = 252f;
    public float nameBottomWithDescription    = 0.36f;
    public float nameBottomWithoutDescription = 0.02f;

    [Header("Fallbacks")]
    public Sprite fallbackArt;

    private bool  _baseFontsCached;
    private float _baseName, _baseDescription, _baseCost, _baseInitial;

    public void Bind(CardData data)
    {
        if (data == null) return;

        if (nameText != null) nameText.text = data.cardName;
        if (costText != null) costText.text = data.manaCost.ToString();
        if (descriptionText != null) descriptionText.text = data.description;

        Sprite sprite = data.Art;

        if (art != null)
        {
            bool hasArt = sprite != null;
            art.sprite         = hasArt ? sprite : fallbackArt;
            art.type           = hasArt ? Image.Type.Simple : Image.Type.Sliced;
            art.color          = hasArt ? Color.white : data.accentColor;
            art.preserveAspect = hasArt;
        }

        if (initial != null)
        {
            initial.gameObject.SetActive(sprite == null);
            initial.text = string.IsNullOrEmpty(data.cardName)
                ? "?"
                : data.cardName[..1].ToUpperInvariant();
        }

        if (frame != null) frame.color = frameColor;
    }

    public void ShowDescription(bool visible)
    {
        bool showing = visible && descriptionText != null
                               && !string.IsNullOrWhiteSpace(descriptionText.text);

        if (descriptionText != null) descriptionText.gameObject.SetActive(showing);

        if (nameText == null) return;
        var nameRect = nameText.rectTransform;
        var min = nameRect.anchorMin;
        min.y = showing ? nameBottomWithDescription : nameBottomWithoutDescription;
        nameRect.anchorMin = min;
    }

    public void SetSize(Vector2 size)
    {
        if (rect == null) return;

        CacheBaseFonts();
        rect.sizeDelta = size;

        if (referenceHeight <= 0f) return;

        float scale = size.y / referenceHeight;
        if (nameText        != null) nameText.fontSize        = _baseName        * scale;
        if (descriptionText != null) descriptionText.fontSize = _baseDescription * scale;
        if (costText        != null) costText.fontSize        = _baseCost        * scale;
        if (initial         != null) initial.fontSize         = _baseInitial     * scale;
    }

    private void CacheBaseFonts()
    {
        if (_baseFontsCached) return;
        _baseFontsCached = true;

        if (nameText        != null) _baseName        = nameText.fontSize;
        if (descriptionText != null) _baseDescription = descriptionText.fontSize;
        if (costText        != null) _baseCost        = costText.fontSize;
        if (initial         != null) _baseInitial     = initial.fontSize;
    }
}
