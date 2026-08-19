using TMPro;
using UnityEngine;

[CreateAssetMenu(menuName = "SkyZoo/UI Theme", fileName = "UITheme")]
public class UITheme : ScriptableObject
{
    public enum Role { Display, Number, Body }

    public const string ResourcePath = "UITheme";

    [Header("Fonts")]
    public TMP_FontAsset displayFont;
    public TMP_FontAsset numberFont;
    public TMP_FontAsset bodyFont;

    [Header("Type")]
    public float displayTracking = 6f;
    public float bodyTracking    = 2f;
    public bool  uppercaseLabels = true;

    [Header("Palette")]
    public Color ink        = new(0.05f, 0.06f, 0.10f, 1f);
    public Color panel      = new(0.15f, 0.16f, 0.22f, 1f);
    public Color panelLight = new(0.22f, 0.24f, 0.32f, 1f);
    public Color shadow     = new(0f, 0f, 0f, 0.30f);
    public Color textStrong = new(1f, 1f, 1f, 1f);
    public Color textMuted  = new(0.66f, 0.70f, 0.82f, 1f);

    [Header("Accents")]
    public Color mana   = new(0.36f, 0.63f, 1f, 1f);
    public Color score  = new(1f, 0.80f, 0.32f, 1f);
    public Color quota  = new(0.44f, 0.85f, 0.50f, 1f);
    public Color money  = new(1f, 0.72f, 0.26f, 1f);
    public Color paths  = new(0.72f, 0.56f, 0.96f, 1f);
    public Color day    = new(0.98f, 0.85f, 0.42f, 1f);
    public Color danger = new(0.96f, 0.33f, 0.37f, 1f);

    [Header("Shapes")]
    public Sprite panelSprite;
    public Sprite pillSprite;
    public Sprite badgeSprite;
    public Sprite buttonSprite;

    [Header("Icons")]
    public Sprite manaIcon;
    public Sprite scoreIcon;
    public Sprite quotaIcon;
    public Sprite dayIcon;
    public Sprite pathIcon;
    public Sprite moneyIcon;

    [Header("Chrome")]
    public float outlineWidth = 5f;
    public float shadowDrop   = 6f;
    public float cornerScale  = 1f;

    public Sprite PanelShape  => panelSprite;
    public Sprite PillShape   => pillSprite;
    public Sprite BadgeShape  => badgeSprite  != null ? badgeSprite : UISprites.Circle;
    public Sprite ButtonShape => buttonSprite;

    public Sprite ManaIcon  => manaIcon;
    public Sprite ScoreIcon => scoreIcon;
    public Sprite QuotaIcon => quotaIcon;
    public Sprite DayIcon   => dayIcon;
    public Sprite PathIcon  => pathIcon;
    public Sprite MoneyIcon => moneyIcon;

    private static UITheme _active;

    public static UITheme Active
    {
        get
        {
            if (_active != null) return _active;

            _active = Resources.Load<UITheme>(ResourcePath);
            if (_active == null)
            {
                _active = CreateInstance<UITheme>();
                _active.name      = "UITheme (defaults)";
                _active.hideFlags = HideFlags.HideAndDontSave;
            }
            return _active;
        }
    }

    public static void SetActive(UITheme theme)
    {
        if (theme != null) _active = theme;
    }

    public void Apply(TMP_Text text, Role role, float size, Color color)
    {
        var font = role switch
        {
            Role.Display => displayFont != null ? displayFont : bodyFont,
            Role.Number  => numberFont  != null ? numberFont  : displayFont != null ? displayFont : bodyFont,
            _            => bodyFont
        };

        if (font != null) text.font = font;

        text.fontSize         = size;
        text.color            = color;
        text.fontStyle        = FontStyles.Bold;
        text.characterSpacing = role == Role.Body ? bodyTracking : displayTracking;
        text.raycastTarget    = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
    }

    public string Label(string value) => uppercaseLabels ? value.ToUpperInvariant() : value;
}
