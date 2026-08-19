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
    public TMP_Text      costText;

    [Header("Colors")]
    public Color frameColor    = new(0.13f, 0.14f, 0.19f, 1f);
    public Color selectedColor = new(0.22f, 0.52f, 0.34f, 1f);
    public Color glowColor     = new(1f, 0.92f, 0.45f, 1f);

    [Header("Layout")]
    public float referenceHeight = 252f;

    [Header("Fallbacks")]
    public Sprite fallbackArt;

    private bool  _baseFontsCached;
    private float _baseName, _baseCost, _baseInitial;
    private bool  _hasArt;

    public void Bind(CardData data)
    {
        if (data == null) return;

        if (nameText != null) nameText.text = data.cardName;
        if (costText != null) costText.text = data.manaCost.ToString();

        Sprite sprite = data.Art;
        _hasArt = sprite != null;

        if (frame != null)
        {
            frame.sprite         = _hasArt ? sprite : fallbackArt;
            frame.type           = _hasArt ? Image.Type.Simple : Image.Type.Sliced;
            frame.color          = _hasArt ? Color.white : frameColor;
            frame.preserveAspect = _hasArt;
        }

        if (art != null)
        {
            art.gameObject.SetActive(!_hasArt);
            art.sprite         = fallbackArt;
            art.type           = Image.Type.Sliced;
            art.color          = data.accentColor;
            art.preserveAspect = false;
        }

        if (initial != null)
        {
            initial.gameObject.SetActive(!_hasArt);
            initial.text = string.IsNullOrEmpty(data.cardName)
                ? "?"
                : data.cardName[..1].ToUpperInvariant();
        }
    }

    public Color FrameTint(float selection) => _hasArt
        ? Color.Lerp(Color.white, selectedColor, selection * 0.35f)
        : Color.Lerp(frameColor, selectedColor, selection);

    public void SetSize(Vector2 size)
    {
        if (rect == null) return;

        CacheBaseFonts();
        rect.sizeDelta = size;

        if (referenceHeight <= 0f) return;

        float scale = size.y / referenceHeight;
        if (nameText != null) nameText.fontSize = _baseName    * scale;
        if (costText != null) costText.fontSize = _baseCost    * scale;
        if (initial  != null) initial.fontSize  = _baseInitial * scale;
    }

    private void CacheBaseFonts()
    {
        if (_baseFontsCached) return;
        _baseFontsCached = true;

        if (nameText != null) _baseName    = nameText.fontSize;
        if (costText != null) _baseCost    = costText.fontSize;
        if (initial  != null) _baseInitial = initial.fontSize;
    }
}
