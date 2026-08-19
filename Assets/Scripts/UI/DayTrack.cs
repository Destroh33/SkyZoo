using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DayTrack : MonoBehaviour
{
    [Header("Theme")]
    [SerializeField] private UITheme theme;

    [Header("Parts")]
    [SerializeField] private TMP_Text caption;
    [SerializeField] private RectTransform row;
    [SerializeField] private List<RectTransform> pips  = new();
    [SerializeField] private List<Image>         fills = new();
    [SerializeField] private List<Image>         rings = new();

    [Header("Layout")]
    [SerializeField] private float pipSize    = 26f;
    [SerializeField] private float pipSpacing = 12f;

    [Header("Feel")]
    [SerializeField] private float popStiffness = 540f;
    [SerializeField] private float popDamping   = 16f;

    private readonly List<float> _scales = new();
    private readonly List<float> _vels   = new();
    private readonly List<bool>  _lit    = new();

    public RectTransform Rect => (RectTransform)transform;

    void Awake()
    {
        if (theme == null) theme = UITheme.Active;
    }

    public void SetDays(int current, int total, int week)
    {
        if (caption != null)
            caption.text = theme != null
                ? theme.Label($"Week {week}   ·   Day {Mathf.Min(current, total)} / {total}")
                : $"WEEK {week}   ·   DAY {Mathf.Min(current, total)} / {total}";

        if (total > pips.Count) AddPips(total - pips.Count);
        SyncState();

        for (int i = 0; i < pips.Count; i++)
        {
            if (pips[i] == null) continue;
            pips[i].gameObject.SetActive(i < total);
            if (i >= total) continue;

            bool lit = i < current - 1;
            bool now = i == current - 1;

            if (lit && !_lit[i]) _vels[i] += 9f;
            _lit[i] = lit;

            if (fills[i] != null)
                fills[i].color = lit ? theme.day : now ? theme.panelLight : theme.panel;
            if (rings[i] != null)
                rings[i].color = now ? theme.day : theme.ink;
        }
    }

    public void LayoutPips()
    {
        int active = 0;
        foreach (var pip in pips) if (pip != null) active++;

        float span = active * pipSize + Mathf.Max(0, active - 1) * pipSpacing;
        int index = 0;

        foreach (var pip in pips)
        {
            if (pip == null) continue;
            pip.sizeDelta        = new Vector2(pipSize, pipSize);
            pip.anchoredPosition = new Vector2(-span * 0.5f + pipSize * 0.5f + index * (pipSize + pipSpacing), 0f);
            index++;
        }
    }

    private void SyncState()
    {
        while (_scales.Count < pips.Count) { _scales.Add(1f); _vels.Add(0f); _lit.Add(false); }
    }

    void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        SyncState();

        for (int i = 0; i < pips.Count; i++)
        {
            if (pips[i] == null) continue;

            float scale = _scales[i];
            float vel   = _vels[i];
            UiSpring.Step(ref scale, ref vel, 1f, popStiffness, popDamping, dt);
            _scales[i] = scale;
            _vels[i]   = vel;
            pips[i].localScale = new Vector3(scale, scale, 1f);
        }
    }

    public static DayTrack Build(Transform parent, UITheme theme, float width, int pipCount)
    {
        var root = new GameObject("DayTrack", typeof(RectTransform));
        root.transform.SetParent(parent, false);

        var rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(width, 74f);

        var track = root.AddComponent<DayTrack>();
        track.theme = theme;

        var captionGO = new GameObject("Caption", typeof(RectTransform));
        captionGO.transform.SetParent(rootRect, false);

        var captionRect = captionGO.GetComponent<RectTransform>();
        captionRect.anchorMin        = captionRect.anchorMax = new Vector2(0.5f, 1f);
        captionRect.pivot            = new Vector2(0.5f, 1f);
        captionRect.sizeDelta        = new Vector2(width, 30f);
        captionRect.anchoredPosition = Vector2.zero;

        track.caption = captionGO.AddComponent<TextMeshProUGUI>();
        track.caption.alignment = TextAlignmentOptions.Center;
        theme.Apply(track.caption, UITheme.Role.Display, 24f, theme.textStrong);

        var rowGO = new GameObject("Pips", typeof(RectTransform));
        rowGO.transform.SetParent(rootRect, false);

        track.row = rowGO.GetComponent<RectTransform>();
        track.row.anchorMin        = track.row.anchorMax = new Vector2(0.5f, 1f);
        track.row.pivot            = new Vector2(0.5f, 1f);
        track.row.sizeDelta        = new Vector2(width, 34f);
        track.row.anchoredPosition = new Vector2(0f, -36f);

        track.AddPips(pipCount);
        track.LayoutPips();
        return track;
    }

    private void AddPips(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var pipGO = new GameObject($"Pip{pips.Count}", typeof(RectTransform));
            pipGO.transform.SetParent(row, false);

            var pipRect = pipGO.GetComponent<RectTransform>();
            pipRect.anchorMin = pipRect.anchorMax = pipRect.pivot = new Vector2(0.5f, 0.5f);
            pipRect.sizeDelta = new Vector2(pipSize, pipSize);

            var ring = CardFactory.Stretch(pipGO.transform, "Ring", -4f).AddComponent<Image>();
            ring.sprite        = theme.BadgeShape;
            ring.color         = theme.ink;
            ring.raycastTarget = false;

            var fill = CardFactory.Stretch(pipGO.transform, "Fill", 0f).AddComponent<Image>();
            fill.sprite        = theme.BadgeShape;
            fill.color         = theme.panel;
            fill.raycastTarget = false;

            pips.Add(pipRect);
            rings.Add(ring);
            fills.Add(fill);
        }

        LayoutPips();
    }
}
