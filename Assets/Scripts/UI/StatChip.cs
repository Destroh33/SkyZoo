using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StatChip : MonoBehaviour
{
    [Header("Theme")]
    [SerializeField] private UITheme theme;

    [Header("Parts")]
    [SerializeField] private RectTransform rect;
    [SerializeField] private RectTransform iconAnchor;
    [SerializeField] private Image fill;
    [SerializeField] private Image outline;
    [SerializeField] private Image badgeFill;
    [SerializeField] private Image icon;
    [SerializeField] private Image barFill;
    [SerializeField] private RectTransform barFillRect;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private TMP_Text valueText;

    [Header("Colors")]
    [SerializeField] private Color accent    = new(1f, 0.80f, 0.32f, 1f);
    [SerializeField] private Color basePanel = new(0.15f, 0.16f, 0.22f, 1f);
    [SerializeField] private Color baseInk   = new(0.05f, 0.06f, 0.10f, 1f);

    [Header("Feel")]
    [SerializeField] private float punchStiffness = 520f;
    [SerializeField] private float punchDamping   = 15f;
    [SerializeField] private float rollDuration   = 0.32f;
    [SerializeField] private float flashDecay     = 3.2f;
    [SerializeField] private float barStiffness   = 240f;
    [SerializeField] private float barDamping     = 20f;

    public RectTransform Rect       => rect       != null ? rect : rect = (RectTransform)transform;
    public RectTransform IconAnchor => iconAnchor;
    public Color         Accent     => accent;

    private float _scale = 1f;
    private float _scaleVel;
    private float _iconScale = 1f;
    private float _iconVel;

    private float  _shown;
    private float  _from;
    private float  _target;
    private float  _rollTime = 1f;
    private string _format = "0";
    private string _suffix = "";
    private string _prefix = "";
    private bool   _hasNumber;

    private Color _flashColor;
    private float _flashAmount;

    private float _barTarget;
    private float _barShown;
    private float _barVel;

    void Awake()
    {
        if (theme == null) theme = UITheme.Active;
    }

    public void SetNumber(float value, string format = "0", string suffix = "", bool animate = true)
    {
        _format = format;
        _suffix = suffix;

        if (!_hasNumber || !animate)
        {
            _hasNumber = true;
            _shown = _from = _target = value;
            _rollTime = 1f;
            Render();
            return;
        }

        if (Mathf.Approximately(value, _target)) return;

        _from     = _shown;
        _target   = value;
        _rollTime = 0f;

        Punch(value > _from ? 0.16f : 0.09f);
    }

    public void SetText(string content)
    {
        _hasNumber = false;
        if (valueText != null) valueText.text = content;
    }

    public void SetLabel(string content)
    {
        if (labelText == null) return;
        labelText.text = theme != null ? theme.Label(content) : content.ToUpperInvariant();
    }

    public void SetBar(float normalized, bool animate = true)
    {
        _barTarget = Mathf.Clamp01(normalized);
        if (!animate) _barShown = _barTarget;
    }

    public void SetIcon(Sprite sprite)
    {
        if (icon != null) icon.sprite = sprite;
    }

    public void SetAccent(Color color)
    {
        accent = color;
        if (badgeFill != null) badgeFill.color = color;
        if (barFill   != null) barFill.color   = color;
    }

    public void Punch(float amount = 0.14f)
    {
        _scaleVel += amount * 22f;
        _iconVel  += amount * 26f;
    }

    public void Flash(Color color, float amount = 1f)
    {
        _flashColor  = color;
        _flashAmount = Mathf.Max(_flashAmount, amount);
    }

    void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        UiSpring.Step(ref _scale,     ref _scaleVel, 1f, punchStiffness, punchDamping, dt);
        UiSpring.Step(ref _iconScale, ref _iconVel,  1f, punchStiffness * 1.2f, punchDamping, dt);

        Rect.localScale = new Vector3(_scale, _scale, 1f);
        if (iconAnchor != null) iconAnchor.localScale = new Vector3(_iconScale, _iconScale, 1f);

        if (_rollTime < 1f && rollDuration > 0f)
        {
            _rollTime = Mathf.Min(1f, _rollTime + dt / rollDuration);
            _shown    = Mathf.Lerp(_from, _target, UiSpring.EaseOutCubic(_rollTime));
            Render();
        }

        if (_flashAmount > 0f)
        {
            _flashAmount = Mathf.Max(0f, _flashAmount - dt * flashDecay);
            if (fill    != null) fill.color    = Color.Lerp(basePanel, _flashColor, _flashAmount);
            if (outline != null) outline.color = Color.Lerp(baseInk, _flashColor, _flashAmount * 0.55f);
        }

        if (barFillRect == null) return;

        UiSpring.Step(ref _barShown, ref _barVel, _barTarget, barStiffness, barDamping, dt);
        barFillRect.anchorMax = new Vector2(Mathf.Clamp01(_barShown), 1f);
    }

    public void SetPrefix(string content)
    {
        _prefix = content ?? "";
        Render();
    }

    private void Render()
    {
        if (valueText != null) valueText.text = _prefix + _shown.ToString(_format) + _suffix;
    }

    public static StatChip Build(Transform parent, UITheme theme, string label, Sprite icon,
                                 Color accent, Vector2 size, bool withBar)
    {
        var root = new GameObject($"Chip_{label}", typeof(RectTransform));
        root.transform.SetParent(parent, false);

        var rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = size;

        var chip = root.AddComponent<StatChip>();
        chip.theme     = theme;
        chip.rect      = rootRect;
        chip.accent    = accent;
        chip.basePanel = theme.panel;
        chip.baseInk   = theme.ink;

        var shadow = Layer(rootRect, "Shadow", theme.PanelShape, theme.shadow, -theme.outlineWidth * 0.6f);
        shadow.rectTransform.anchoredPosition = new Vector2(0f, -theme.shadowDrop);

        chip.outline = Layer(rootRect, "Outline", theme.PanelShape, theme.ink, -theme.outlineWidth);
        chip.fill    = Layer(rootRect, "Fill",    theme.PanelShape, theme.panel, 0f);

        float badge = Mathf.Min(size.y - 18f, 56f);
        float padX  = 14f;

        var badgeGO = new GameObject("Badge", typeof(RectTransform));
        badgeGO.transform.SetParent(rootRect, false);

        chip.iconAnchor = badgeGO.GetComponent<RectTransform>();
        chip.iconAnchor.anchorMin        = chip.iconAnchor.anchorMax = new Vector2(0f, 0.5f);
        chip.iconAnchor.pivot            = new Vector2(0.5f, 0.5f);
        chip.iconAnchor.sizeDelta        = new Vector2(badge, badge);
        chip.iconAnchor.anchoredPosition = new Vector2(padX + badge * 0.5f, 0f);

        Layer(chip.iconAnchor, "BadgeOutline", theme.BadgeShape, theme.ink, -theme.outlineWidth * 0.8f);
        chip.badgeFill = Layer(chip.iconAnchor, "BadgeFill", theme.BadgeShape, accent, 0f);

        chip.icon = CardFactory.Stretch(chip.iconAnchor, "Icon", badge * 0.22f).AddComponent<Image>();
        chip.icon.sprite         = icon;
        chip.icon.color          = theme.ink;
        chip.icon.preserveAspect = true;
        chip.icon.raycastTarget  = false;

        float textLeft  = padX + badge + 12f;
        float textWidth = size.x - textLeft - padX;
        float centerY   = withBar ? 12f : 0f;

        chip.labelText = Text(rootRect, theme, "Label", theme.Label(label), UITheme.Role.Body,
                              size.y * 0.19f, theme.textMuted, TextAlignmentOptions.BottomLeft,
                              textLeft, textWidth, size.y * 0.28f, centerY + size.y * 0.19f);

        chip.valueText = Text(rootRect, theme, "Value", "0", UITheme.Role.Number,
                              size.y * 0.40f, theme.textStrong, TextAlignmentOptions.TopLeft,
                              textLeft, textWidth, size.y * 0.46f, centerY - size.y * 0.14f);

        if (withBar) chip.BuildBar(rootRect, theme, padX, accent);

        return chip;
    }

    private void BuildBar(RectTransform parent, UITheme theme, float padX, Color accentColor)
    {
        var trackGO = new GameObject("Bar", typeof(RectTransform));
        trackGO.transform.SetParent(parent, false);

        var trackRect = trackGO.GetComponent<RectTransform>();
        trackRect.anchorMin = new Vector2(0f, 0f);
        trackRect.anchorMax = new Vector2(1f, 0f);
        trackRect.pivot     = new Vector2(0.5f, 0f);
        trackRect.offsetMin = new Vector2(padX, 10f);
        trackRect.offsetMax = new Vector2(-padX, 24f);

        var track = trackGO.AddComponent<Image>();
        track.sprite        = theme.PillShape;
        track.type          = SpriteType(theme.PillShape);
        track.color         = theme.ink;
        track.raycastTarget = false;

        var inner  = CardFactory.Stretch(trackGO.transform, "BarInner", 3f);
        var fillGO = new GameObject("BarFill", typeof(RectTransform));
        fillGO.transform.SetParent(inner.transform, false);

        barFillRect = fillGO.GetComponent<RectTransform>();
        barFillRect.anchorMin = Vector2.zero;
        barFillRect.anchorMax = new Vector2(0f, 1f);
        barFillRect.pivot     = new Vector2(0f, 0.5f);
        barFillRect.offsetMin = Vector2.zero;
        barFillRect.offsetMax = Vector2.zero;

        barFill = fillGO.AddComponent<Image>();
        barFill.sprite        = theme.PillShape;
        barFill.type          = SpriteType(theme.PillShape);
        barFill.color         = accentColor;
        barFill.raycastTarget = false;
    }

    private static Image Layer(RectTransform parent, string name, Sprite sprite, Color color, float inset)
    {
        var image = CardFactory.Stretch(parent, name, inset).AddComponent<Image>();
        image.sprite        = sprite;
        image.type          = SpriteType(sprite);
        image.color         = color;
        image.raycastTarget = false;
        return image;
    }

    public static Image.Type SpriteType(Sprite sprite)
        => sprite != null && sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;

    private static TMP_Text Text(RectTransform parent, UITheme theme, string name, string content,
                                 UITheme.Role role, float size, Color color,
                                 TextAlignmentOptions alignment,
                                 float left, float width, float height, float y)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var textRect = go.GetComponent<RectTransform>();
        textRect.anchorMin        = textRect.anchorMax = new Vector2(0f, 0.5f);
        textRect.pivot            = new Vector2(0f, 0.5f);
        textRect.sizeDelta        = new Vector2(width, height);
        textRect.anchoredPosition = new Vector2(left, y);

        var text = go.AddComponent<TextMeshProUGUI>();
        text.text      = content;
        text.alignment = alignment;
        theme.Apply(text, role, size, color);
        return text;
    }
}
