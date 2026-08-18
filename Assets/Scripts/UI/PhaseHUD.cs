using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Wires a manually-built Canvas (one Button + four TMP text objects) to
// GameManager's day/week/phase loop:
//   - manaText / quotaText / scoreText / dayText refresh whenever the
//     economy changes.
//   - advanceButton calls GameManager.AdvanceDayPhase() — only visible/usable
//     during Phase.Build. Wiring is done in code (Start()); do not also add
//     an OnClick() entry for it in the Inspector.
//   - when GameManager enters Phase.Reward, this spawns its own runtime popup
//     canvas showing 3 big reward cards (from GameManager.RewardOptions);
//     clicking one calls GameManager.ChooseReward and returns to Phase.Build.
//
// Assign advanceButton/manaText/quotaText/scoreText/dayText in the Inspector
// after building the Canvas. Use TextMeshProUGUI for the text fields (TMP_Text
// is the base class both TextMeshProUGUI and 3D TMP use).
public class PhaseHUD : MonoBehaviour
{
    [Header("Wire these to your Canvas objects")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private Button   advanceButton;
    [SerializeField] private TMP_Text manaText;
    [SerializeField] private TMP_Text quotaText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text dayText;
    [SerializeField] private TMP_Text pathsText; // optional — leave unassigned if not built yet

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

    void Start()
    {
        if (gameManager == null)
            gameManager = GameManager.instance != null ? GameManager.instance : FindAnyObjectByType<GameManager>();

        advanceButton.onClick.AddListener(gameManager.AdvanceDayPhase);

        gameManager.OnEconomyChanged += RefreshTexts;
        gameManager.OnPhaseChanged   += HandlePhaseChanged;

        RefreshTexts();
        HandlePhaseChanged();
    }

    void OnDestroy()
    {
        if (gameManager == null) return;
        gameManager.OnEconomyChanged -= RefreshTexts;
        gameManager.OnPhaseChanged   -= HandlePhaseChanged;
    }

    private void RefreshTexts()
    {
        manaText.text  = $"Mana: {gameManager.Mana}/{gameManager.MaxMana}";
        quotaText.text = $"Quota: {gameManager.Quota:0}";
        scoreText.text = $"Score: {gameManager.WeekScore:0}";
        dayText.text   = $"Day {gameManager.Day} / {gameManager.DaysPerWeek}";
        if (pathsText != null) pathsText.text = $"Paths: {gameManager.PathsRemaining}/{gameManager.MaxPaths}";
    }

    private void HandlePhaseChanged()
    {
        RefreshTexts();
        bool inBuild = gameManager.CurrentPhase == GameManager.Phase.Build;
        advanceButton.gameObject.SetActive(inBuild);

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
        var canvasGO = new GameObject("RewardCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var blocker = CardFactory.Stretch(canvasGO.transform, "Blocker", 0f).AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0.6f);

        var titleGO = new GameObject("RewardTitle", typeof(RectTransform));
        titleGO.transform.SetParent(canvasGO.transform, false);
        var titleRt = titleGO.GetComponent<RectTransform>();
        titleRt.anchorMin        = titleRt.anchorMax = new Vector2(0.5f, 0.5f);
        titleRt.sizeDelta        = new Vector2(900f, 60f);
        titleRt.anchoredPosition = new Vector2(0f, cardHeight * 0.5f + 62f);

        var title = titleGO.AddComponent<TextMeshProUGUI>();
        title.text          = "PICK A CARD";
        title.fontSize      = 34f;
        title.fontStyle     = FontStyles.Bold;
        title.alignment     = TextAlignmentOptions.Center;
        title.color         = Color.white;
        title.raycastTarget = false;

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

    private void HideRewardPopup()
    {
        if (_rewardCanvas != null) Destroy(_rewardCanvas);
        _rewardCanvas = null;
    }
}
