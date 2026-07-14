using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Isometric 3D grid view — grid lives in the XZ plane, Y is up.
// The GridView GameObject's world position is the CENTER of the grid.
// Hotkeys (bound in InputSystem_Actions → Grid map):
//   1-9   select hand slot → enters the mode that card's TargetMode requires
//   P     path placement mode
//   N     advance day (debug: same as clicking the Advance button)
//   Esc   cancel / clear mode
//   LMB   place enclosure / toggle path edge / pick target(s) for a card
//   RMB   remove enclosure under cursor (refunds partial mana)
// All grid/hand interaction is gated to Phase.Build — during Phase.Reward
// (the daily 3-card choice) the board is frozen until ChooseReward is called.
public class GridView : MonoBehaviour
{
    public enum Phase { Build, Scoring, Reward }

    [Header("Grid Settings")]
    [SerializeField] private int   gridWidth  = 10;
    [SerializeField] private int   gridHeight = 10;
    [SerializeField] private float cellSize   = 1f;

    [Header("Island")]
    [SerializeField] private GameObject islandPrefab; // assign a 3D island model; leave null for default plane

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("Visuals")]
    [SerializeField] private Color gridLineColor  = new(0f,    0f,    0f,    0.8f);
    [SerializeField] private Color pathColor      = new(1f,    0.85f, 0.2f,  1f);
    [SerializeField] private Color previewValid   = new(1f,    1f,    1f,    0.45f);
    [SerializeField] private Color previewInvalid = new(1f,    0.25f, 0.25f, 0.45f);
    [SerializeField] private Color edgeHoverColor = new(1f,    0.85f, 0.2f,  0.5f);
    [SerializeField] private Color startVertexColor = new(0.2f, 1f,   0.2f,  1f);
    [SerializeField] private Color endVertexColor   = new(1f,   0.2f, 0.2f,  1f);
    [SerializeField] private float lineThickness  = 0.05f;
    [SerializeField] private float pathThickness  = 0.18f;
    [SerializeField] private float edgeSnapDist   = 0.25f;
    [SerializeField] private float vertexMarkerSize = 0.3f;

    [Header("Paths")]
    [SerializeField] private int maxPaths = 8; // global pool, shared for the whole game — never refills

    [Header("Score Wave")]
    [SerializeField] private float scoreWaveStagger = 0.25f; // seconds between each enclosure's popup, ordered by distance from the start vertex
    [SerializeField] private float endOfDayPause    = 2f;    // pause after the wave (and week/quota result) before the reward screen appears

    [Header("Camera")]
    [SerializeField] private float camPitch       = 30f;
    [SerializeField] private float camYaw         = 45f;
    [SerializeField] private float camZoom        = 1f;   // >1 zooms out, <1 zooms in
    [SerializeField] private float perspectiveFov = 60f;

    [Header("Economy")]
    [SerializeField] private int   startingMana          = 3;
    [SerializeField] private float enclosureRefundFraction = 0.5f; // partial, not full, refund on deletion

    [Header("Starting Hand (assign card assets in Inspector)")]
    [SerializeField] private CardData[] startingHand;

    [Header("Phase / Week")]
    [SerializeField] private int   daysPerWeek       = 5; // GDD originally said 7 — using 5 per latest direction
    [SerializeField] private float startingQuota     = 20f;
    [SerializeField] private float quotaGrowthPerWeek = 1.25f; // quota scaling is the run's difficulty curve
    [SerializeField] private CardData[] cardPool;          // possible cards offered by the daily reward draft

    // ── Y heights for overlay layers ──────────────────────────────────────────
    private const float YGrid    = 0.005f;
    private const float YPath    = 0.015f;
    private const float YPreview = 0.025f;

    // ── Sorting orders ────────────────────────────────────────────────────────
    private const int SortGridLines = -5;
    private const int SortPathEdges =  5;
    private const int SortEdgeHover =  8;
    private const int SortPreview   = 10;

    // ── Input actions ────────────────────────────────────────────────────────
    private InputAction _pointerPositionAction;
    private InputAction _clickAction;
    private InputAction _removeAction;
    private InputAction _pathModeAction;
    private InputAction _cancelAction;
    private InputAction _selectSlotAction;
    private InputAction _toggleCamAction;
    private InputAction _advanceDayAction;

    // ── Runtime state ────────────────────────────────────────────────────────
    private GridModel _model;
    private Sprite    _whiteSprite;

    private Hand     _hand;
    private ManaPool _mana;
    private int      _currentDay; // ever-increasing master clock, used for timed-bonus expiry

    private Phase _phase = Phase.Build;
    private int   _day   = 1;      // 1..daysPerWeek, resets each week — for display
    private int   _week  = 1;
    private float _quota;
    private float _weekScore;
    private List<CardData> _rewardOptions;

    // For UI (e.g. HandHUD, PhaseHUD) to read/react to without polling every frame.
    public event Action OnHandChanged;
    public event Action OnPendingCardChanged;
    public event Action OnEconomyChanged; // mana / quota / score / day / week changed
    public event Action OnPhaseChanged;

    public IReadOnlyList<CardInstance> HandCards => _hand.Cards;
    public CardInstance PendingCard => _pendingCard;

    public Phase CurrentPhase => _phase;
    public int   Day         => _day;
    public int   DaysPerWeek => daysPerWeek;
    public int   Week        => _week;
    public float Quota       => _quota;
    public float WeekScore   => _weekScore;
    public int   Mana        => _mana.Current;
    public int   MaxMana     => _mana.Max;
    public IReadOnlyList<CardData> RewardOptions => _rewardOptions;
    public int   PathsRemaining => _model.PathsRemaining;
    public int   MaxPaths        => _model.MaxPaths;

    // World-space position of grid corner (0,0) — set in Start from transform.position
    private Vector3 _origin;

    private readonly Dictionary<EnclosureInstance, GameObject> _enclosureViews = new();
    private readonly List<GameObject>                          _pathViews       = new();

    private GameObject     _enclosurePreview;
    private SpriteRenderer _enclosurePreviewSr;
    private GameObject     _edgePreview;

    private enum Mode { None, Enclosure, Path, SelectSingleTarget, SelectMoveSource, SelectMoveDestination }
    private Mode          _mode;
    private CardInstance  _pendingCard;
    private EnclosureData _pendingEnclosureData; // resolved from _pendingCard when TargetMode == PlaceEnclosure
    private EnclosureInstance _moveSource;


    private readonly Plane _groundPlane = new(Vector3.up, Vector3.zero);

    private bool                 _isPerspective;
    private FreeCameraController _freeCam;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    void Awake()
    {
        var map = inputActions.FindActionMap("Grid", throwIfNotFound: true);
        _pointerPositionAction = map.FindAction("PointerPosition", throwIfNotFound: true);
        _clickAction           = map.FindAction("Click",           throwIfNotFound: true);
        _removeAction          = map.FindAction("RemoveEnclosure", throwIfNotFound: true);
        _pathModeAction        = map.FindAction("PathMode",        throwIfNotFound: true);
        _cancelAction          = map.FindAction("Cancel",          throwIfNotFound: true);
        _selectSlotAction      = map.FindAction("SelectSlot",      throwIfNotFound: true);
        _toggleCamAction       = map.FindAction("ToggleCamera",    throwIfNotFound: true);
        _advanceDayAction      = map.FindAction("AdvanceDay",      throwIfNotFound: true);

        // Built here (not Start) so hand/mana state exists before any other
        // script's Start() runs — Unity doesn't guarantee Start() ordering
        // between different components, but Awake() always fully completes
        // first across every object. HandHUD.Start() reads this immediately.
        _origin = transform.position + new Vector3(
            -gridWidth  * cellSize * 0.5f,
            0f,
            -gridHeight * cellSize * 0.5f);

        _model       = new GridModel(gridWidth, gridHeight, maxPaths);
        _whiteSprite = MakeWhiteSprite();

        _mana  = new ManaPool(startingMana);
        _hand  = new Hand();
        _quota = startingQuota;
        foreach (var card in startingHand)
            if (card != null) _hand.Add(card);
    }

    void OnEnable()
    {
        inputActions.FindActionMap("Grid").Enable();
        _clickAction.performed      += OnClick;
        _removeAction.performed     += OnRemoveEnclosure;
        _pathModeAction.performed   += OnPathMode;
        _cancelAction.performed     += OnCancel;
        _selectSlotAction.performed += OnSelectSlot;
        _toggleCamAction.performed  += OnToggleCamera;
        _advanceDayAction.performed += OnAdvanceDay;
    }

    void OnDisable()
    {
        _clickAction.performed      -= OnClick;
        _removeAction.performed     -= OnRemoveEnclosure;
        _pathModeAction.performed   -= OnPathMode;
        _cancelAction.performed     -= OnCancel;
        _selectSlotAction.performed -= OnSelectSlot;
        _toggleCamAction.performed  -= OnToggleCamera;
        _advanceDayAction.performed -= OnAdvanceDay;
        inputActions.FindActionMap("Grid").Disable();
    }

    void Start()
    {
        LogState("Game start");

        SpawnIsland();
        BuildGridLines();
        SpawnPathEndpointMarkers();

        (_enclosurePreview, _enclosurePreviewSr) = MakeFlatQuad("Preview_Enclosure", Color.clear,    SortPreview);
        _enclosurePreview.SetActive(false);

        (_edgePreview, _) = MakeFlatQuad("Preview_Edge", edgeHoverColor, SortEdgeHover);
        _edgePreview.SetActive(false);

        // Find or add FreeCameraController on the main camera
        _freeCam = Camera.main.GetComponent<FreeCameraController>();
        if (_freeCam == null) _freeCam = Camera.main.gameObject.AddComponent<FreeCameraController>();
        _freeCam.enabled = false;

        FitCamera();
    }

    void Update()
    {
        if (TryGetGroundHit(out Vector3 hit))
            UpdateHoverPreview(hit);
    }

    // ── Input callbacks ──────────────────────────────────────────────────────

    private void OnToggleCamera(InputAction.CallbackContext ctx)
    {
        _isPerspective = !_isPerspective;
        var cam = Camera.main;

        if (_isPerspective)
        {
            cam.orthographic = false;
            cam.fieldOfView  = perspectiveFov;
            _freeCam.enabled = true;
            SetMode(Mode.None);
        }
        else
        {
            _freeCam.enabled = false;
            cam.orthographic = true;
            FitCamera();
        }
    }

    private void OnSelectSlot(InputAction.CallbackContext ctx)
    {
        if (_phase != Phase.Build) return;
        if (!int.TryParse(ctx.control.name, out int num) || num < 1 || num > _hand.Cards.Count) return;
        SelectCard(_hand.Cards[num - 1]);
    }

    // Selects a card from the hand as the pending card to play — entering the
    // mode its TargetMode requires. Selecting the already-pending card again
    // deselects it. Called by number-key hotkeys and by HandHUD card clicks.
    // Takes the specific CardInstance (not just its CardData) so duplicate
    // copies of the same card type in hand are never confused for one another.
    public void SelectCard(CardInstance card)
    {
        if (card == null || _phase != Phase.Build) return;

        if (_pendingCard == card)
        {
            SetMode(Mode.None);
            return;
        }

        _pendingCard = card;
        _moveSource  = null;

        switch (card.Data.TargetMode)
        {
            case CardTargetMode.PlaceEnclosure:
                _pendingEnclosureData = ((EnclosureCardData)card.Data).enclosure;
                _mode = Mode.Enclosure;
                break;
            case CardTargetMode.SelectOneEnclosure:
                _mode = Mode.SelectSingleTarget;
                break;
            case CardTargetMode.MoveEnclosure:
                _mode = Mode.SelectMoveSource;
                break;
        }

        OnPendingCardChanged?.Invoke();
    }

    private void OnPathMode(InputAction.CallbackContext ctx)
    {
        if (_phase != Phase.Build) return;
        _mode = Mode.Path;
        _enclosurePreview.SetActive(false);
    }

    private void OnCancel(InputAction.CallbackContext ctx) => SetMode(Mode.None);

    // Keyboard shortcut for the same thing the Advance button does.
    private void OnAdvanceDay(InputAction.CallbackContext ctx) => AdvanceDayPhase();

    private void OnClick(InputAction.CallbackContext ctx)
    {
        if (_phase != Phase.Build) return;
        if (!TryGetGroundHit(out Vector3 hit)) return;
        switch (_mode)
        {
            case Mode.Enclosure:            TryPlaceEnclosure(hit);   break;
            case Mode.Path:                 TryTogglePath(hit);       break;
            case Mode.SelectSingleTarget:    TryApplyAmplify(hit);    break;
            case Mode.SelectMoveSource:      TrySelectMoveSource(hit); break;
            case Mode.SelectMoveDestination: TryCompleteMove(hit);     break;
        }
    }

    private void OnRemoveEnclosure(InputAction.CallbackContext ctx)
    {
        if (_phase != Phase.Build) return;
        if (TryGetGroundHit(out Vector3 hit)) TryRemoveAt(hit);
    }

    // ── Day / week phase loop ─────────────────────────────────────────────────

    // Ends the build phase for today: scores the grid, advances the day/week
    // counters (rolling over and scaling the quota at week's end), refills
    // mana, expires timed bonuses, and rolls the 3-card daily reward.
    // Scoring plays out as a staggered wave of popups (nearest the start
    // vertex first) rather than adding instantly — see ScoreDayCoroutine.
    public void AdvanceDayPhase()
    {
        if (_phase != Phase.Build) return;

        if (!_model.HasValidPath())
        {
            Debug.Log("[SkyZoo] Can't advance — no valid path from start to end.");
            return;
        }

        SetMode(Mode.None); // clear any pending card/targeting before ending the day
        _phase = Phase.Scoring;
        OnPhaseChanged?.Invoke();

        StartCoroutine(ScoreDayCoroutine());
    }

    private System.Collections.IEnumerator ScoreDayCoroutine()
    {
        _currentDay++;
        _model.CurrentDay = _currentDay;

        Vector3 startWorld = G2W(_model.StartVertex.x * cellSize, _model.StartVertex.y * cellSize, YPath);

        var ordered = new List<EnclosureInstance>(_model.Enclosures);
        ordered.Sort((a, b) =>
            Vector3.Distance(EnclosureWorldPosition(a), startWorld)
                .CompareTo(Vector3.Distance(EnclosureWorldPosition(b), startWorld)));

        foreach (var instance in ordered)
        {
            float score = _model.GetEnclosureScore(instance);
            _weekScore += score;

            ScorePopup.Spawn(EnclosureWorldPosition(instance) + Vector3.up, score, transform);
            OnEconomyChanged?.Invoke();

            if (scoreWaveStagger > 0f) yield return new WaitForSeconds(scoreWaveStagger);
        }

        foreach (var e in _model.Enclosures) e.ExpireBonuses(_currentDay);
        _mana.RefillForNewDay();

        LogState($"Day {_day}/{daysPerWeek} scored → week total {_weekScore:0.#}/{_quota:0.#}");

        if (_day >= daysPerWeek)
        {
            bool passed = _weekScore >= _quota;
            Debug.Log(passed
                ? $"[SkyZoo] Week {_week} complete — quota met! ({_weekScore:0.#}/{_quota:0.#})"
                : $"[SkyZoo] Week {_week} FAILED quota. ({_weekScore:0.#}/{_quota:0.#})");

            _week++;
            _quota    *= quotaGrowthPerWeek;
            _weekScore = 0f;
            _day       = 1;
        }
        else
        {
            _day++;
        }

        OnEconomyChanged?.Invoke();

        if (endOfDayPause > 0f) yield return new WaitForSeconds(endOfDayPause);

        _rewardOptions = PickRandomCards(3);
        _phase = Phase.Reward;
        OnPhaseChanged?.Invoke();
    }

    // Called by the reward-popup UI when the player picks one of the 3 cards.
    public void ChooseReward(CardData card)
    {
        if (_phase != Phase.Reward || card == null) return;

        _hand.Add(card);
        _rewardOptions = null;
        _phase = Phase.Build;

        LogState($"Added '{card.cardName}' to hand from daily reward");
        OnHandChanged?.Invoke();
        OnPhaseChanged?.Invoke();
    }

    private List<CardData> PickRandomCards(int n)
    {
        var pool   = new List<CardData>(cardPool);
        var result = new List<CardData>();
        for (int i = 0; i < n && pool.Count > 0; i++)
        {
            int idx = UnityEngine.Random.Range(0, pool.Count);
            result.Add(pool[idx]);
            pool.RemoveAt(idx);
        }
        return result;
    }

    // ── Card play logic ──────────────────────────────────────────────────────

    private void TryPlaceEnclosure(Vector3 hit)
    {
        if (_pendingCard == null || _pendingEnclosureData == null) return;
        var cell = WorldToCell(hit);
        if (!_model.CanPlaceEnclosure(cell, _pendingEnclosureData.size))
        {
            Debug.Log($"[SkyZoo] Can't place '{_pendingCard.Data.cardName}' there — space is occupied or out of bounds.");
            return;
        }
        if (!_mana.TrySpend(_pendingCard.Data.manaCost))
        {
            Debug.Log($"[SkyZoo] Not enough mana to play '{_pendingCard.Data.cardName}' (need {_pendingCard.Data.manaCost}, have {_mana.Current}).");
            return;
        }

        var instance = _model.PlaceEnclosure(_pendingEnclosureData, cell, _pendingCard.Data.manaCost);
        SpawnEnclosureView(instance);
        RebuildPathViews(); // clears the view for any path piece the model just deleted underneath it
        _hand.Remove(_pendingCard);
        LogState($"Played '{_pendingCard.Data.cardName}' → placed enclosure");
        SetMode(Mode.None);
        OnHandChanged?.Invoke();
        OnEconomyChanged?.Invoke();
    }

    private void TryApplyAmplify(Vector3 hit)
    {
        var card = (AmplifyCardData)_pendingCard.Data;
        var cell = WorldToCell(hit);
        if (!InCellBounds(cell)) return;
        var target = _model.GetCell(cell.x, cell.y);
        if (target == null) return;
        if (!_mana.TrySpend(card.manaCost))
        {
            Debug.Log($"[SkyZoo] Not enough mana to play '{card.cardName}' (need {card.manaCost}, have {_mana.Current}).");
            return;
        }

        if (card.durationDays <= 0) target.AddPermanentBonus(card.bonusAmount);
        else                        target.AddTimedBonus(card.bonusAmount, _currentDay + card.durationDays);

        _hand.Remove(_pendingCard);
        LogState($"Played '{card.cardName}' → +{card.bonusAmount} bonus on enclosure at {target.GridPosition}");
        SetMode(Mode.None);
        OnHandChanged?.Invoke();
        OnEconomyChanged?.Invoke();
    }

    private void TrySelectMoveSource(Vector3 hit)
    {
        var cell = WorldToCell(hit);
        if (!InCellBounds(cell)) return;
        var target = _model.GetCell(cell.x, cell.y);
        if (target == null) return;

        _moveSource = target;
        _mode       = Mode.SelectMoveDestination;
    }

    private void TryCompleteMove(Vector3 hit)
    {
        var cell = WorldToCell(hit);
        if (!InCellBounds(cell)) return;
        if (cell == _moveSource.GridPosition) return; // no-op move, don't waste mana
        if (!_model.CanPlaceEnclosureIgnoring(_moveSource, cell, _moveSource.Data.size)) return;
        if (!_mana.TrySpend(_pendingCard.Data.manaCost))
        {
            Debug.Log($"[SkyZoo] Not enough mana to play '{_pendingCard.Data.cardName}' (need {_pendingCard.Data.manaCost}, have {_mana.Current}).");
            return;
        }

        _model.MoveEnclosure(_moveSource, cell);
        RefreshEnclosureView(_moveSource);
        RebuildPathViews(); // clears the view for any path piece the model just deleted underneath it

        _hand.Remove(_pendingCard);
        LogState($"Played '{_pendingCard.Data.cardName}' → moved enclosure");
        SetMode(Mode.None);
        OnHandChanged?.Invoke();
        OnEconomyChanged?.Invoke();
    }

    private void TryTogglePath(Vector3 hit)
    {
        if (!TrySnapToEdge(hit, out bool horiz, out int ex, out int ey)) return;
        bool toggled = horiz ? _model.ToggleHEdge(ex, ey) : _model.ToggleVEdge(ex, ey);
        if (toggled) RebuildPathViews();
    }

    private void TryRemoveAt(Vector3 hit)
    {
        var cell = WorldToCell(hit);
        if (!InCellBounds(cell)) return;
        var instance = _model.GetCell(cell.x, cell.y);
        if (instance == null) return;

        _model.RemoveEnclosure(instance);
        int refund = Mathf.FloorToInt(instance.ManaCostPaid * enclosureRefundFraction);
        _mana.Refund(refund);

        if (_enclosureViews.TryGetValue(instance, out var go))
        {
            Destroy(go);
            _enclosureViews.Remove(instance);
        }

        LogState($"Removed '{instance.Data.enclosureName}' → refunded {refund} mana");
        OnEconomyChanged?.Invoke();
    }

    // Prints current mana and hand contents — the only visibility into game
    // state right now, since there's no HUD yet.
    private void LogState(string action)
    {
        var names = new List<string>(_hand.Cards.Count);
        foreach (var c in _hand.Cards) names.Add($"{c.Data.cardName}({c.Data.manaCost})");
        string hand = names.Count > 0 ? string.Join(", ", names) : "(empty)";

        Debug.Log($"[SkyZoo] {action} — mana {_mana.Current}/{_mana.Max} | hand: {hand}");
    }

    // ── Hover preview ────────────────────────────────────────────────────────

    private void UpdateHoverPreview(Vector3 hit)
    {
        switch (_mode)
        {
            case Mode.Enclosure:
                _edgePreview.SetActive(false);
                if (_pendingEnclosureData == null) { _enclosurePreview.SetActive(false); break; }
                var cell  = WorldToCell(hit);
                var size  = _pendingEnclosureData.size;
                bool ok   = _model.CanPlaceEnclosure(cell, size);
                PlaceFootprint(_enclosurePreview.transform,
                    CellCenterWorld(cell, size, YPreview),
                    (size.x * cellSize - 0.08f, size.y * cellSize - 0.08f));
                _enclosurePreview.SetActive(true);
                _enclosurePreviewSr.color = ok ? previewValid : previewInvalid;
                break;

            case Mode.Path:
                _enclosurePreview.SetActive(false);
                if (TrySnapToEdge(hit, out bool horiz, out int ex, out int ey))
                {
                    _edgePreview.SetActive(true);
                    PositionEdgeQuad(_edgePreview.transform, horiz, ex, ey, YPreview);
                }
                else
                {
                    _edgePreview.SetActive(false);
                }
                break;

            case Mode.SelectSingleTarget:
            case Mode.SelectMoveSource:
                _edgePreview.SetActive(false);
                var targetCell = WorldToCell(hit);
                var occupant   = InCellBounds(targetCell) ? _model.GetCell(targetCell.x, targetCell.y) : null;
                if (occupant != null)
                {
                    PlaceFootprint(_enclosurePreview.transform,
                        CellCenterWorld(occupant.GridPosition, occupant.Data.size, YPreview),
                        (occupant.Data.size.x * cellSize - 0.08f, occupant.Data.size.y * cellSize - 0.08f));
                    _enclosurePreview.SetActive(true);
                    _enclosurePreviewSr.color = previewValid;
                }
                else
                {
                    _enclosurePreview.SetActive(false);
                }
                break;

            case Mode.SelectMoveDestination:
                _edgePreview.SetActive(false);
                var destCell = WorldToCell(hit);
                var moveSize = _moveSource.Data.size;
                bool destOk  = _model.CanPlaceEnclosureIgnoring(_moveSource, destCell, moveSize);
                PlaceFootprint(_enclosurePreview.transform,
                    CellCenterWorld(destCell, moveSize, YPreview),
                    (moveSize.x * cellSize - 0.08f, moveSize.y * cellSize - 0.08f));
                _enclosurePreview.SetActive(true);
                _enclosurePreviewSr.color = destOk ? previewValid : previewInvalid;
                break;

            default:
                _enclosurePreview.SetActive(false);
                _edgePreview.SetActive(false);
                break;
        }
    }

    // ── Visuals ──────────────────────────────────────────────────────────────

    private void SpawnIsland()
    {
        if (islandPrefab != null)
        {
            // Island model should be centered at its own pivot — place it at the grid center
            Instantiate(islandPrefab, transform.position, Quaternion.identity, transform);
        }
        else
        {
            // Placeholder: Unity Plane is 10×10 world units, scale to fit grid
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.transform.SetParent(transform);
            float w = gridWidth * cellSize, h = gridHeight * cellSize;
            plane.transform.position   = transform.position + new Vector3(0f, -0.02f, 0f);
            plane.transform.localScale = new Vector3(w / 10f, 1f, h / 10f);
            plane.name = "Island_Placeholder";
        }
    }

    private void BuildGridLines()
    {
        float w = gridWidth  * cellSize;
        float h = gridHeight * cellSize;

        for (int row = 0; row <= gridHeight; row++)
        {
            var (go, _) = MakeFlatQuad($"HLine_{row}", gridLineColor, SortGridLines);
            go.transform.position   = G2W(w * 0.5f, row * cellSize, YGrid);
            go.transform.localScale = new Vector3(w, lineThickness, 1f);
        }

        for (int col = 0; col <= gridWidth; col++)
        {
            var (go, _) = MakeFlatQuad($"VLine_{col}", gridLineColor, SortGridLines);
            go.transform.position   = G2W(col * cellSize, h * 0.5f, YGrid);
            go.transform.localScale = new Vector3(lineThickness, h, 1f);
        }
    }

    // Marks the fixed start/end grid-vertices that a path must connect —
    // purely visual, positions come from the model's auto-picked vertices.
    private void SpawnPathEndpointMarkers()
    {
        SpawnVertexMarker("PathStart", _model.StartVertex, startVertexColor);
        SpawnVertexMarker("PathEnd",   _model.EndVertex,   endVertexColor);
    }

    private void SpawnVertexMarker(string objName, Vector2Int vertex, Color color)
    {
        var (go, _) = MakeFlatQuad(objName, color, SortPathEdges + 1);
        go.transform.position   = G2W(vertex.x * cellSize, vertex.y * cellSize, YPath);
        go.transform.localScale = new Vector3(vertexMarkerSize, vertexMarkerSize, 1f);
    }

    // World position an enclosure's view should sit at — prefab pivot vs. flat-quad footprint.
    private Vector3 EnclosureWorldPosition(EnclosureInstance instance)
    {
        bool hasPrefab = instance.Data.prefab != null;
        var  center    = CellCenterWorld(instance.GridPosition, instance.Data.size, hasPrefab ? 0f : YPath);
        return hasPrefab ? center + instance.Data.prefabOffset : center;
    }

    private void SpawnEnclosureView(EnclosureInstance instance)
    {
        GameObject go;
        if (instance.Data.prefab != null)
        {
            go = Instantiate(instance.Data.prefab, EnclosureWorldPosition(instance), Quaternion.identity, transform);
            go.name = $"Enclosure_{instance.GridPosition.x}_{instance.GridPosition.y}";
        }
        else
        {
            var (quad, _) = MakeFlatQuad($"Enclosure_{instance.GridPosition.x}_{instance.GridPosition.y}", instance.Data.footprintColor, 0);
            PlaceFootprint(quad.transform, EnclosureWorldPosition(instance),
                (instance.Data.size.x * cellSize - 0.08f, instance.Data.size.y * cellSize - 0.08f));
            go = quad;
        }

        _enclosureViews[instance] = go;
    }

    private void RefreshEnclosureView(EnclosureInstance instance)
    {
        if (_enclosureViews.TryGetValue(instance, out var go))
            go.transform.position = EnclosureWorldPosition(instance);
    }

    private void RebuildPathViews()
    {
        foreach (var go in _pathViews) Destroy(go);
        _pathViews.Clear();

        for (int x = 0; x < _model.Width; x++)
            for (int y = 0; y <= _model.Height; y++)
                if (_model.GetHEdge(x, y))
                {
                    var (go, _) = MakeFlatQuad($"HEdge_{x}_{y}", pathColor, SortPathEdges);
                    PositionEdgeQuad(go.transform, true, x, y, YPath);
                    _pathViews.Add(go);
                }

        for (int x = 0; x <= _model.Width; x++)
            for (int y = 0; y < _model.Height; y++)
                if (_model.GetVEdge(x, y))
                {
                    var (go, _) = MakeFlatQuad($"VEdge_{x}_{y}", pathColor, SortPathEdges);
                    PositionEdgeQuad(go.transform, false, x, y, YPath);
                    _pathViews.Add(go);
                }
    }

    // ── Layout helpers ────────────────────────────────────────────────────────

    private void PositionEdgeQuad(Transform t, bool horiz, int x, int y, float worldY)
    {
        float cs = cellSize;
        if (horiz)
        {
            t.position   = G2W((x + 0.5f) * cs, y * cs,         worldY);
            t.localScale = new Vector3(cs, pathThickness, 1f);
        }
        else
        {
            t.position   = G2W(x * cs,         (y + 0.5f) * cs, worldY);
            t.localScale = new Vector3(pathThickness, cs, 1f);
        }
    }

    private static void PlaceFootprint(Transform t, Vector3 center, (float w, float d) size)
    {
        t.position   = center;
        t.localScale = new Vector3(size.w, size.d, 1f);
    }

    // ── Edge snapping ─────────────────────────────────────────────────────────

    private bool TrySnapToEdge(Vector3 hit, out bool horiz, out int ex, out int ey)
    {
        // Convert world hit to grid-local coords
        float gx = (hit.x - _origin.x) / cellSize;
        float gz = (hit.z - _origin.z) / cellSize;

        int   hx = Mathf.FloorToInt(gx);
        int   hy = Mathf.RoundToInt(gz);
        float dh = Mathf.Abs(gz - hy) * cellSize;
        bool hOk = hx >= 0 && hx < _model.Width && hy >= 0 && hy <= _model.Height
                   && dh < edgeSnapDist && !_model.IsHEdgeBlocked(hx, hy);

        int   vx = Mathf.RoundToInt(gx);
        int   vy = Mathf.FloorToInt(gz);
        float dv = Mathf.Abs(gx - vx) * cellSize;
        bool vOk = vx >= 0 && vx <= _model.Width && vy >= 0 && vy < _model.Height
                   && dv < edgeSnapDist && !_model.IsVEdgeBlocked(vx, vy);

        if (hOk && (!vOk || dh <= dv)) { horiz = true;  ex = hx; ey = hy; return true; }
        if (vOk)                        { horiz = false; ex = vx; ey = vy; return true; }

        horiz = false; ex = 0; ey = 0;
        return false;
    }

    // ── Camera ────────────────────────────────────────────────────────────────

    private void FitCamera()
    {
        var cam = Camera.main;
        cam.orthographic = true;
        cam.transform.rotation = Quaternion.Euler(camPitch, camYaw, 0f);

        float diagHalf = Mathf.Sqrt(gridWidth * gridWidth + gridHeight * gridHeight) * cellSize * 0.5f;
        cam.transform.position = transform.position - cam.transform.forward * diagHalf * 3f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane  = diagHalf * 8f;

        float pitchRad = camPitch * Mathf.Deg2Rad;
        cam.orthographicSize = (diagHalf + 0.5f) / Mathf.Cos(pitchRad) * camZoom;
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    private void SetMode(Mode m)
    {
        _mode = m;
        _pendingCard          = null;
        _pendingEnclosureData = null;
        _moveSource           = null;
        _enclosurePreview.SetActive(false);
        _edgePreview.SetActive(false);
        OnPendingCardChanged?.Invoke();
    }

    private bool TryGetGroundHit(out Vector3 worldPos)
    {
        var screenPos = _pointerPositionAction.ReadValue<Vector2>();
        var ray       = Camera.main.ScreenPointToRay(screenPos);
        if (_groundPlane.Raycast(ray, out float dist))
        {
            worldPos = ray.GetPoint(dist);
            return true;
        }
        worldPos = default;
        return false;
    }

    // Grid-local XZ offset → world position (handles the centered origin)
    private Vector3 G2W(float localX, float localZ, float worldY)
        => new(_origin.x + localX, worldY, _origin.z + localZ);

    // World center of a cell range, at a given world Y
    private Vector3 CellCenterWorld(Vector2Int cell, Vector2Int size, float worldY)
        => G2W((cell.x + size.x * 0.5f) * cellSize, (cell.y + size.y * 0.5f) * cellSize, worldY);

    private Vector2Int WorldToCell(Vector3 world)
        => new(Mathf.FloorToInt((world.x - _origin.x) / cellSize),
               Mathf.FloorToInt((world.z - _origin.z) / cellSize));

    private bool InCellBounds(Vector2Int cell)
        => cell.x >= 0 && cell.y >= 0 && cell.x < _model.Width && cell.y < _model.Height;

    // Sprites rotated 90° around X lie flat in XZ:
    //   local X → world X,  local Y → world +Z,  sprite face → world +Y
    private (GameObject go, SpriteRenderer sr) MakeFlatQuad(string objName, Color color, int order)
    {
        var go = new GameObject(objName);
        go.transform.SetParent(transform);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = _whiteSprite;
        sr.color        = color;
        sr.sortingOrder = order;
        return (go, sr);
    }

    private static Sprite MakeWhiteSprite()
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }
}
