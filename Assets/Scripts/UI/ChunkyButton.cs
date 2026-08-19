using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ChunkyButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
                            IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [Header("Theme")]
    [SerializeField] private UITheme theme;

    [Header("Parts")]
    [SerializeField] private RectTransform face;
    [SerializeField] private Image    fill;
    [SerializeField] private TMP_Text label;

    [Header("Colors")]
    [SerializeField] private Color accent   = new(0.44f, 0.85f, 0.50f, 1f);
    [SerializeField] private Color labelInk = new(0.05f, 0.06f, 0.10f, 1f);
    [SerializeField] private Color disabledTint = new(0.15f, 0.16f, 0.22f, 1f);
    [SerializeField] private Color disabledInk  = new(0.66f, 0.70f, 0.82f, 1f);

    [Header("Feel")]
    [SerializeField] private float hoverScale   = 1.06f;
    [SerializeField] private float pressDrop    = 6f;
    [SerializeField] private float pulseAmount  = 0.022f;
    [SerializeField] private float pulseSpeed   = 3.4f;
    [SerializeField] private bool  pulsing;

    [Header("Events")]
    public UnityEngine.Events.UnityEvent onClick;

    private Action _callback;
    private float  _scale = 1f;
    private float  _scaleVel;
    private float  _press;
    private bool   _hovered;
    private bool   _held;
    private bool   _interactable = true;

    public RectTransform Rect => (RectTransform)transform;

    void Awake()
    {
        if (theme == null) theme = UITheme.Active;
    }

    public void SetOnClick(Action callback) => _callback = callback;

    public void SetLabel(string content)
    {
        if (label == null) return;
        label.text = theme != null ? theme.Label(content) : content.ToUpperInvariant();
    }

    public void SetInteractable(bool value)
    {
        _interactable = value;
        if (value) return;
        _hovered = false;
        _held    = false;
    }

    public void SetPulsing(bool value) => pulsing = value;

    public void SetSize(Vector2 size) => Rect.sizeDelta = size;

    public void SetAccent(Color color)
    {
        accent = color;
        if (fill != null) fill.color = color;
    }

    public static ChunkyButton Create(GameObject prefab, Transform parent, string labelText,
                                      Vector2 size, UITheme theme, Color fallbackAccent)
    {
        var button = prefab != null
            ? Instantiate(prefab, parent).GetComponent<ChunkyButton>()
            : Build(parent, theme != null ? theme : UITheme.Active, labelText, fallbackAccent, size);

        if (button == null)
        {
            Debug.LogError("[SkyZoo] Button prefab is missing its ChunkyButton component.");
            return null;
        }

        button.name = $"Button_{labelText}";
        button.SetSize(size);
        button.SetLabel(labelText);
        return button;
    }

    void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        float target = _interactable ? (_hovered ? hoverScale : 1f) : 0.98f;
        if (pulsing && _interactable && !_hovered)
            target += Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmount;

        UiSpring.Step(ref _scale, ref _scaleVel, target, 560f, 17f, dt);
        transform.localScale = new Vector3(_scale, _scale, 1f);

        _press = Mathf.MoveTowards(_press, _held ? 1f : 0f, dt * 14f);
        if (face != null) face.anchoredPosition = new Vector2(0f, -pressDrop * _press);

        if (fill != null)
        {
            var tint = _interactable ? accent : Color.Lerp(accent, disabledTint, 0.7f);
            fill.color = Color.Lerp(tint, Color.white, _hovered && _interactable ? 0.16f : 0f);
        }

        if (label != null)
            label.color = _interactable ? labelInk : disabledInk;
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (!_interactable || _hovered) return;
        _hovered   = true;
        _scaleVel += 3f;
        Sfx.CardHover();
    }

    public void OnPointerExit(PointerEventData e)
    {
        _hovered = false;
        _held    = false;
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (_interactable) _held = true;
    }

    public void OnPointerUp(PointerEventData e) => _held = false;

    public void OnPointerClick(PointerEventData e)
    {
        if (!_interactable) return;
        _scaleVel += 7f;
        Sfx.ButtonPress();
        _callback?.Invoke();
        onClick?.Invoke();
    }

    public static ChunkyButton Build(Transform parent, UITheme theme, string labelText,
                                     Color accent, Vector2 size)
    {
        var root = new GameObject($"Button_{labelText}", typeof(RectTransform));
        root.transform.SetParent(parent, false);

        var rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = size;

        var button = root.AddComponent<ChunkyButton>();
        button.theme        = theme;
        button.accent       = accent;
        button.labelInk     = theme.ink;
        button.disabledTint = theme.panel;
        button.disabledInk  = theme.textMuted;
        button.pressDrop    = theme.shadowDrop;

        var shadow = Layer(rootRect, "Shadow", theme.ButtonShape, theme.ink, -theme.outlineWidth * 0.5f);
        shadow.rectTransform.anchoredPosition = new Vector2(0f, -theme.shadowDrop);

        var hit = CardFactory.Stretch(rootRect, "Hit", -theme.outlineWidth).AddComponent<Image>();
        hit.color         = new Color(0f, 0f, 0f, 0f);
        hit.raycastTarget = true;

        button.face = CardFactory.Stretch(rootRect, "Face", 0f).GetComponent<RectTransform>();

        Layer(button.face, "Outline", theme.ButtonShape, theme.ink, -theme.outlineWidth);
        button.fill = Layer(button.face, "Fill", theme.ButtonShape, accent, 0f);

        var labelGO = CardFactory.Stretch(button.face, "Label", 8f);
        button.label = labelGO.AddComponent<TextMeshProUGUI>();
        button.label.text      = theme.Label(labelText);
        button.label.alignment = TextAlignmentOptions.Center;
        theme.Apply(button.label, UITheme.Role.Display, size.y * 0.34f, theme.ink);
        button.label.textWrappingMode = TextWrappingModes.Normal;
        button.label.enableAutoSizing = true;
        button.label.fontSizeMax      = size.y * 0.34f;
        button.label.fontSizeMin      = 9f;
        button.label.overflowMode     = TextOverflowModes.Ellipsis;

        return button;
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
}
