using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PhaseHUD : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private GridView    gridView;

    [Header("Built by SkyZoo/UI/Build Game HUD")]
    [SerializeField] private UITheme       theme;
    [SerializeField] private StatChip      manaChip;
    [SerializeField] private StatChip      scoreChip;
    [SerializeField] private StatChip      moneyChip;
    [SerializeField] private StatChip      pathsChip;
    [SerializeField] private DayTrack      dayTrack;
    [SerializeField] private ChunkyButton  advanceButton;
    [SerializeField] private HudJuice      juice;
    [SerializeField] private RectTransform moteLayer;
    [SerializeField] private GameObject    legacyRoot;

    [Header("Score Motes")]
    [SerializeField] private float moteSize     = 46f;
    [SerializeField] private float moteDuration = 0.55f;
    [SerializeField] private float moteShake    = 5f;

    [Header("Prefabs (leave empty to generate at runtime)")]
    [SerializeField] private GameObject rewardPopupPrefab;
    [SerializeField] private GameObject rewardCardPrefab;
    [SerializeField] private GameObject shopOverlayPrefab;

    [Header("Reward Popup")]
    [SerializeField] private float rewardCardWidth  = 210f;
    [SerializeField] private float rewardCardHeight = 294f;
    [SerializeField] private float rewardCardGap    = 46f;

    private GameObject  _rewardCanvas;
    private ShopOverlay _shopOverlay;

    private float _scoreShown;
    private bool  _quotaCelebrated;

    void Start()
    {
        if (gameManager == null)
            gameManager = GameManager.instance != null ? GameManager.instance : FindAnyObjectByType<GameManager>();
        if (gridView == null) gridView = FindAnyObjectByType<GridView>();
        if (theme == null) theme = UITheme.Active;

        if (manaChip == null || scoreChip == null || advanceButton == null)
        {
            Debug.LogError("[SkyZoo] PhaseHUD is not built. Run SkyZoo/UI/Build Game HUD.");
            return;
        }

        if (legacyRoot != null) legacyRoot.SetActive(false);

        advanceButton.SetOnClick(gameManager.AdvanceDayPhase);
        if (moneyChip != null) moneyChip.SetPrefix("$");

        gameManager.OnEconomyChanged  += Refresh;
        gameManager.OnPhaseChanged    += HandlePhaseChanged;
        gameManager.OnManaDenied      += HandleManaDenied;
        gameManager.OnEnclosureScored += HandleEnclosureScored;

        _scoreShown = gameManager.WeekScore;
        Refresh();
        HandlePhaseChanged();
    }

    void OnDestroy()
    {
        if (gameManager == null) return;
        gameManager.OnEconomyChanged  -= Refresh;
        gameManager.OnPhaseChanged    -= HandlePhaseChanged;
        gameManager.OnManaDenied      -= HandleManaDenied;
        gameManager.OnEnclosureScored -= HandleEnclosureScored;
    }

    private void Refresh()
    {
        manaChip.SetNumber(gameManager.Mana, "0", $"/{gameManager.MaxMana}");
        manaChip.SetBar(gameManager.MaxMana > 0 ? gameManager.Mana / (float)gameManager.MaxMana : 0f);

        if (moneyChip != null) moneyChip.SetNumber(gameManager.money);
        if (pathsChip != null) pathsChip.SetNumber(gameManager.PathsRemaining, "0", $"/{gameManager.MaxPaths}");

        if (gameManager.CurrentPhase != GameManager.Phase.Scoring)
        {
            _scoreShown = gameManager.WeekScore;
            ApplyScore(false);
        }

        if (dayTrack != null)
            dayTrack.SetDays(gameManager.Day, gameManager.DaysPerWeek, gameManager.Week);
    }

    private void ApplyScore(bool animate)
    {
        float quota = Mathf.Max(1f, gameManager.Quota);

        scoreChip.SetNumber(_scoreShown, "0", "", animate);
        scoreChip.SetLabel($"Score  ·  quota {gameManager.Quota:0}");
        scoreChip.SetBar(_scoreShown / quota, animate);

        bool met = _scoreShown >= gameManager.Quota;
        if (met && !_quotaCelebrated)
        {
            _quotaCelebrated = true;
            scoreChip.SetAccent(theme.quota);
            scoreChip.Punch(0.42f);
            scoreChip.Flash(theme.quota, 0.8f);
            if (juice != null)
            {
                juice.Shake(16f);
                juice.Flash(theme.quota, 0.22f);
            }
            Sfx.Coin();
        }
        else if (!met && _quotaCelebrated)
        {
            _quotaCelebrated = false;
            scoreChip.SetAccent(theme.score);
        }
    }

    private void HandleManaDenied()
    {
        manaChip.Flash(theme.danger, 1f);
        manaChip.Punch(0.2f);
        if (juice != null) juice.Shake(7f);
    }

    private void HandleEnclosureScored(EnclosureInstance instance, float score)
    {
        if (moteLayer == null || gridView == null || Camera.main == null)
        {
            _scoreShown += score;
            ApplyScore(true);
            return;
        }

        var world  = gridView.ScoreAnchorWorld(instance);
        var screen = Camera.main.WorldToScreenPoint(world);

        if (screen.z < 0f)
        {
            _scoreShown += score;
            ApplyScore(true);
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            moteLayer, screen, null, out var from);

        var target = RectTransformUtility.WorldToScreenPoint(null, scoreChip.Rect.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            moteLayer, target, null, out var to);

        float landed = score;
        ScoreMote.Spawn(moteLayer, from, to, theme.ScoreIcon, theme.score,
                        moteSize, moteDuration, 0f, () =>
        {
            _scoreShown += landed;
            ApplyScore(true);
            if (juice != null) juice.Shake(moteShake);
        });
    }

    private void HandlePhaseChanged()
    {
        Refresh();

        bool inBuild = gameManager.CurrentPhase == GameManager.Phase.Build;
        advanceButton.gameObject.SetActive(inBuild);
        advanceButton.SetPulsing(inBuild);

        if (gameManager.CurrentPhase == GameManager.Phase.Reward) ShowRewardPopup();
        else                                                      HideRewardPopup();

        if (gameManager.CurrentPhase == GameManager.Phase.Shop) ShowShop();
        else                                                    HideShop();
    }

    private void ShowShop()
    {
        if (_shopOverlay != null) return;
        _shopOverlay = ShopOverlay.Create(gameManager, shopOverlayPrefab);
    }

    private void HideShop()
    {
        if (_shopOverlay != null) Destroy(_shopOverlay.gameObject);
        _shopOverlay = null;
    }

    private void ShowRewardPopup()
    {
        HideRewardPopup();

        var canvasGO = rewardPopupPrefab != null
            ? Instantiate(rewardPopupPrefab)
            : BuildRewardPopup(rewardCardHeight);
        canvasGO.name = "RewardCanvas";

        var row = canvasGO.transform.Find("CardRow") as RectTransform;
        if (row == null)
        {
            Debug.LogError("[SkyZoo] Reward popup is missing a child named 'CardRow'.");
            Destroy(canvasGO);
            return;
        }

        var layout = row.GetComponent<HorizontalLayoutGroup>();
        if (layout != null) layout.spacing = rewardCardGap;

        var options = gameManager.RewardOptions;
        for (int i = 0; i < options.Count; i++)
        {
            var card = options[i];
            var slot = new GameObject($"RewardSlot{i}", typeof(RectTransform), typeof(LayoutElement));
            slot.transform.SetParent(row, false);

            var element = slot.GetComponent<LayoutElement>();
            element.preferredWidth  = rewardCardWidth;
            element.preferredHeight = rewardCardHeight;

            var binder = CardFactory.Create(rewardCardPrefab, slot.transform, card,
                                            new Vector2(rewardCardWidth, rewardCardHeight), true);

            var chosen = card;
            var view   = binder.gameObject.AddComponent<CardView>();
            view.Init(binder, card, null, 26f, 26f, 1.09f, 1.09f,
                      _ => { Sfx.RewardPick(); gameManager.ChooseReward(chosen); });
            view.SetSlot(Vector2.zero, 0f, i);
        }

        Sfx.RewardAppear();
        _rewardCanvas = canvasGO;
    }

    public static GameObject BuildRewardPopup(float cardHeight)
    {
        var theme = UITheme.Active;

        var canvasGO = new GameObject("RewardCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var blocker = CardFactory.Stretch(canvasGO.transform, "Blocker", 0f).AddComponent<Image>();
        blocker.color = new Color(theme.ink.r, theme.ink.g, theme.ink.b, 0.78f);

        var panelGO = new GameObject("Panel", typeof(RectTransform));
        panelGO.transform.SetParent(canvasGO.transform, false);

        var panelRt = panelGO.GetComponent<RectTransform>();
        panelRt.anchorMin = panelRt.anchorMax = panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(1000f, cardHeight + 240f);

        var panelShadow = RewardLayer(panelRt, "Shadow", theme.PanelShape, theme.shadow, -theme.outlineWidth);
        panelShadow.rectTransform.anchoredPosition = new Vector2(0f, -theme.shadowDrop * 1.6f);

        RewardLayer(panelRt, "Outline", theme.PanelShape, theme.ink, -theme.outlineWidth * 1.4f);
        RewardLayer(panelRt, "Fill",    theme.PanelShape, theme.panel, 0f);

        var titleGO = new GameObject("RewardTitle", typeof(RectTransform));
        titleGO.transform.SetParent(canvasGO.transform, false);
        var titleRt = titleGO.GetComponent<RectTransform>();
        titleRt.anchorMin        = titleRt.anchorMax = new Vector2(0.5f, 0.5f);
        titleRt.sizeDelta        = new Vector2(900f, 60f);
        titleRt.anchoredPosition = new Vector2(0f, cardHeight * 0.5f + 68f);

        var title = titleGO.AddComponent<TextMeshProUGUI>();
        title.text      = theme.Label("Pick a card");
        title.alignment = TextAlignmentOptions.Center;
        theme.Apply(title, UITheme.Role.Display, 38f, theme.textStrong);

        var rowGO = new GameObject("CardRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        rowGO.transform.SetParent(canvasGO.transform, false);

        var rowRt = rowGO.GetComponent<RectTransform>();
        rowRt.anchorMin = rowRt.anchorMax = rowRt.pivot = new Vector2(0.5f, 0.5f);
        rowRt.sizeDelta = new Vector2(1400f, cardHeight + 40f);

        var layout = rowGO.GetComponent<HorizontalLayoutGroup>();
        layout.childAlignment         = TextAnchor.MiddleCenter;
        layout.childForceExpandWidth  = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth      = true;
        layout.childControlHeight     = true;

        return canvasGO;
    }

    private static Image RewardLayer(RectTransform parent, string name, Sprite sprite,
                                     Color color, float inset)
    {
        var image = CardFactory.Stretch(parent, name, inset).AddComponent<Image>();
        image.sprite        = sprite;
        image.type          = StatChip.SpriteType(sprite);
        image.color         = color;
        image.raycastTarget = false;
        return image;
    }

    private void HideRewardPopup()
    {
        if (_rewardCanvas != null) Destroy(_rewardCanvas);
        _rewardCanvas = null;
    }
}
