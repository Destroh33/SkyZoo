using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Wires a manually-built Canvas (one Button + four TMP text objects) to
// GridView's day/week/phase loop:
//   - manaText / quotaText / scoreText / dayText refresh whenever the
//     economy changes.
//   - advanceButton calls GridView.AdvanceDayPhase() — only visible/usable
//     during Phase.Build. Wiring is done in code (Start()); do not also add
//     an OnClick() entry for it in the Inspector.
//   - when GridView enters Phase.Reward, this spawns its own runtime popup
//     canvas showing 3 big reward cards (from GridView.RewardOptions);
//     clicking one calls GridView.ChooseReward and returns to Phase.Build.
//
// Assign gridView/advanceButton/manaText/quotaText/scoreText/dayText in the
// Inspector after building the Canvas. Use TextMeshProUGUI for the text
// fields (TMP_Text is the base class both TextMeshProUGUI and 3D TMP use).
public class PhaseHUD : MonoBehaviour
{
    [Header("Wire these to your Canvas objects")]
    [SerializeField] private GridView gridView;
    [SerializeField] private Button   advanceButton;
    [SerializeField] private TMP_Text manaText;
    [SerializeField] private TMP_Text quotaText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text dayText;

    [Header("Reward Popup (built at runtime)")]
    [SerializeField] private float rewardCardWidth  = 220f;
    [SerializeField] private float rewardCardHeight = 320f;
    [SerializeField] private float rewardCardGap    = 40f;

    private GameObject _rewardCanvas;

    void Start()
    {
        if (gridView == null) gridView = FindFirstObjectByType<GridView>();

        advanceButton.onClick.AddListener(gridView.AdvanceDayPhase);

        gridView.OnEconomyChanged += RefreshTexts;
        gridView.OnPhaseChanged   += HandlePhaseChanged;

        RefreshTexts();
        HandlePhaseChanged();
    }

    void OnDestroy()
    {
        if (gridView == null) return;
        gridView.OnEconomyChanged -= RefreshTexts;
        gridView.OnPhaseChanged   -= HandlePhaseChanged;
    }

    private void RefreshTexts()
    {
        manaText.text  = $"Mana: {gridView.Mana}/{gridView.MaxMana}";
        quotaText.text = $"Quota: {gridView.Quota:0}";
        scoreText.text = $"Score: {gridView.WeekScore:0}";
        dayText.text   = $"Day {gridView.Day} / {gridView.DaysPerWeek}";
    }

    private void HandlePhaseChanged()
    {
        RefreshTexts();
        bool inBuild = gridView.CurrentPhase == GridView.Phase.Build;
        advanceButton.gameObject.SetActive(inBuild);

        if (gridView.CurrentPhase == GridView.Phase.Reward) ShowRewardPopup();
        else                                                HideRewardPopup();
    }

    private void ShowRewardPopup()
    {
        HideRewardPopup();

        var canvasGO = new GameObject("RewardCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10; // draw above the hand row

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // Full-screen dim blocker — also stops clicks reaching the hand row underneath.
        var blockerGO = new GameObject("Blocker", typeof(RectTransform), typeof(Image));
        blockerGO.transform.SetParent(canvasGO.transform, false);
        var blockerRt = blockerGO.GetComponent<RectTransform>();
        blockerRt.anchorMin = Vector2.zero;
        blockerRt.anchorMax = Vector2.one;
        blockerRt.offsetMin = Vector2.zero;
        blockerRt.offsetMax = Vector2.zero;
        blockerGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

        var options = gridView.RewardOptions;
        int count = options.Count;
        float totalWidth = count * rewardCardWidth + Mathf.Max(0, count - 1) * rewardCardGap;
        float startX     = -totalWidth * 0.5f + rewardCardWidth * 0.5f;

        for (int i = 0; i < count; i++)
        {
            var card   = options[i];
            var cardGO = new GameObject($"Reward_{card.cardName}", typeof(RectTransform), typeof(Image), typeof(Button));
            cardGO.transform.SetParent(canvasGO.transform, false);

            var rt = cardGO.GetComponent<RectTransform>();
            rt.sizeDelta        = new Vector2(rewardCardWidth, rewardCardHeight);
            rt.anchorMin        = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(startX + i * (rewardCardWidth + rewardCardGap), 0f);

            cardGO.GetComponent<Image>().color = new Color(0.18f, 0.18f, 0.24f, 0.98f);

            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(cardGO.transform, false);
            var labelRt = labelGO.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(10f, 10f);
            labelRt.offsetMax = new Vector2(-10f, -10f);

            var text = labelGO.AddComponent<TextMeshProUGUI>();
            text.fontSize  = 20;
            text.alignment = TextAlignmentOptions.Center;
            text.color     = Color.white;
            text.text      = $"{card.cardName}\n\n{card.description}\n\n{card.manaCost} mana";

            var chosen = card; // capture for the closure
            cardGO.GetComponent<Button>().onClick.AddListener(() => gridView.ChooseReward(chosen));
        }

        _rewardCanvas = canvasGO;
    }

    private void HideRewardPopup()
    {
        if (_rewardCanvas != null) Destroy(_rewardCanvas);
        _rewardCanvas = null;
    }
}
