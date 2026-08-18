using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GridView : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private int   gridWidth  = 10;
    [SerializeField] private int   gridHeight = 10;
    [SerializeField] private float cellSize   = 1f;

    [Header("Island")]
    [SerializeField] private GameObject islandPrefab;

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
    [SerializeField] private float dragEdgeSnapDist = 0.14f;
    [SerializeField] private float dragDirectionLockDist = 0.2f;
    [SerializeField] private float vertexMarkerSize = 0.3f;

    [Header("Paths")]
    [SerializeField] private int maxPaths = 8;

    [Header("Camera (locked isometric view)")]
    [SerializeField] private float camPitch = 30f;
    [SerializeField] private float camYaw   = 45f;
    [SerializeField] private float camZoom  = 1f;

    private const float YGrid    = 0.005f;
    private const float YPath    = 0.015f;
    private const float YPreview = 0.025f;

    private const int SortGridLines = -5;
    private const int SortPathEdges =  5;
    private const int SortEdgeHover =  8;
    private const int SortPreview   = 10;

    private InputAction _pointerPositionAction;
    private InputAction _clickAction;
    private InputAction _removeAction;
    private InputAction _pathModeAction;
    private InputAction _cancelAction;
    private InputAction _selectSlotAction;
    private InputAction _advanceDayAction;
    private InputAction _rotateAction;

    private bool _isPathDragging;
    private bool _hasPathDragDirectionLock;
    private bool _pathDragHorizontal;
    private Vector2 _pathDragAnchorLocal;
    private readonly HashSet<Vector2Int> _draggedPathEdgesH = new();
    private readonly HashSet<Vector2Int> _draggedPathEdgesV = new();

    private GridModel _model;
    private Sprite    _whiteSprite;

    public GridModel Model => _model;

    public event Action OnPendingCardChanged;

    public CardInstance PendingCard => _pendingCard;
    public int PathsRemaining => _model.PathsRemaining;
    public int MaxPaths       => _model.MaxPaths;

    private Vector3 _origin;

    private readonly Dictionary<EnclosureInstance, GameObject> _enclosureViews = new();
    private readonly List<GameObject>                          _pathViews       = new();

    private readonly List<SpriteRenderer> _previewQuads = new();
    private GameObject     _edgePreview;

    private enum Mode { None, Enclosure, Path, SelectSingleTarget, SelectMoveSource, SelectMoveDestination }
    private Mode          _mode;
    private CardInstance  _pendingCard;
    private EnclosureData _pendingEnclosureData;
    private int           _pendingRotation;
    private EnclosureInstance _moveSource;
    private int           _moveRotation;

    private readonly Plane _groundPlane = new(Vector3.up, Vector3.zero);

    private GameManager _game;

    private GameManager Game
    {
        get
        {
            if (_game == null) _game = GameManager.instance != null ? GameManager.instance : FindAnyObjectByType<GameManager>();
            return _game;
        }
    }

    private bool InBuildPhase => Game != null && Game.InBuildPhase;

    void Awake()
    {
        var map = inputActions.FindActionMap("Grid", throwIfNotFound: true);
        _pointerPositionAction = map.FindAction("PointerPosition", throwIfNotFound: true);
        _clickAction           = map.FindAction("Click",           throwIfNotFound: true);
        _removeAction          = map.FindAction("RemoveEnclosure", throwIfNotFound: true);
        _pathModeAction        = map.FindAction("PathMode",        throwIfNotFound: true);
        _cancelAction          = map.FindAction("Cancel",          throwIfNotFound: true);
        _selectSlotAction      = map.FindAction("SelectSlot",      throwIfNotFound: true);
        _advanceDayAction      = map.FindAction("AdvanceDay",      throwIfNotFound: true);
        _rotateAction          = map.FindAction("Rotate",          throwIfNotFound: true);

        _origin = transform.position + new Vector3(
            -gridWidth  * cellSize * 0.5f,
            0f,
            -gridHeight * cellSize * 0.5f);

        _model       = new GridModel(gridWidth, gridHeight, maxPaths);
        _whiteSprite = MakeWhiteSprite();
    }

    void OnEnable()
    {
        inputActions.FindActionMap("Grid").Enable();
        _clickAction.performed      += OnClick;
        _removeAction.performed     += OnRemoveEnclosure;
        _pathModeAction.performed   += OnPathMode;
        _cancelAction.performed     += OnCancel;
        _selectSlotAction.performed += OnSelectSlot;
        _advanceDayAction.performed += OnAdvanceDay;
        _rotateAction.performed     += OnRotate;
    }

    void OnDisable()
    {
        _clickAction.performed      -= OnClick;
        _removeAction.performed     -= OnRemoveEnclosure;
        _pathModeAction.performed   -= OnPathMode;
        _cancelAction.performed     -= OnCancel;
        _selectSlotAction.performed -= OnSelectSlot;
        _advanceDayAction.performed -= OnAdvanceDay;
        _rotateAction.performed     -= OnRotate;
        inputActions.FindActionMap("Grid").Disable();
    }

    void OnDestroy()
    {
        if (_game != null) _game.OnEnclosureScored -= SpawnScorePopup;
    }

    void Start()
    {
        if (Game != null) Game.OnEnclosureScored += SpawnScorePopup;

        SpawnIsland();
        BuildGridLines();
        SpawnPathEndpointMarkers();

        (_edgePreview, _) = MakeFlatQuad("Preview_Edge", edgeHoverColor, SortEdgeHover);
        _edgePreview.SetActive(false);

        FitCamera();
    }

    void Update()
    {
        bool hasHit = TryGetGroundHit(out Vector3 hit);
        if (hasHit)
            UpdateHoverPreview(hit);

        if (!InBuildPhase || _mode != Mode.Path) return;

        if (_clickAction.IsPressed())
        {
            if (hasHit && !_isPathDragging)
                BeginPathDrag(hit);

            if (hasHit)
                TryPaintPath(hit);
        }
        else if (_isPathDragging)
        {
            ResetPathDragState();
        }
    }

    private void SpawnScorePopup(EnclosureInstance instance, float score)
    {
        ScorePopup.Spawn(EnclosurePivotWorld(instance, 0f) + Vector3.up, score, transform);
    }

    private void OnSelectSlot(InputAction.CallbackContext ctx)
    {
        if (!InBuildPhase) return;
        var cards = Game.HandCards;
        if (!int.TryParse(ctx.control.name, out int num) || num < 1 || num > cards.Count) return;
        SelectCard(cards[num - 1]);
    }

    public void SelectCard(CardInstance card)
    {
        if (card == null || !InBuildPhase) return;

        if (_pendingCard == card)
        {
            SetMode(Mode.None);
            return;
        }

        _pendingCard     = card;
        _moveSource      = null;
        _pendingRotation = 0;
        _moveRotation    = 0;

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

    public void CancelSelection() => SetMode(Mode.None);

    private void OnPathMode(InputAction.CallbackContext ctx)
    {
        if (!InBuildPhase) return;
        _mode = Mode.Path;
        ResetPathDragState();
        HideFootprintPreview();
    }

    private void OnCancel(InputAction.CallbackContext ctx) => SetMode(Mode.None);

    private void OnRotate(InputAction.CallbackContext ctx)
    {
        if (!InBuildPhase) return;

        switch (_mode)
        {
            case Mode.Enclosure:             _pendingRotation = (_pendingRotation + 1) % 4; break;
            case Mode.SelectMoveDestination: _moveRotation    = (_moveRotation    + 1) % 4; break;
        }
    }

    private void OnAdvanceDay(InputAction.CallbackContext ctx)
    {
        if (Game != null) Game.AdvanceDayPhase();
    }

    private void OnClick(InputAction.CallbackContext ctx)
    {
        if (!InBuildPhase) return;
        if (!TryGetGroundHit(out Vector3 hit)) return;
        switch (_mode)
        {
            case Mode.Enclosure:            TryPlaceEnclosure(hit);   break;
            case Mode.Path:
                BeginPathDrag(hit);
                TryTogglePath(hit);
                break;
            case Mode.SelectSingleTarget:    TryApplyAmplify(hit);    break;
            case Mode.SelectMoveSource:      TrySelectMoveSource(hit); break;
            case Mode.SelectMoveDestination: TryCompleteMove(hit);     break;
        }
    }

    private void OnRemoveEnclosure(InputAction.CallbackContext ctx)
    {
        if (!InBuildPhase) return;
        if (TryGetGroundHit(out Vector3 hit)) TryRemoveAt(hit);
    }

    private void TryPlaceEnclosure(Vector3 hit)
    {
        if (_pendingCard == null || _pendingEnclosureData == null) return;
        var cell = WorldToPivot(hit, _pendingEnclosureData, _pendingRotation);
        if (!_model.CanPlaceEnclosure(_pendingEnclosureData, cell, _pendingRotation))
        {
            Debug.Log($"[SkyZoo] Can't place '{_pendingCard.Data.cardName}' there — space is occupied or out of bounds.");
            return;
        }

        var card = _pendingCard;
        if (!Game.TryPlayCard(card)) return;

        var instance = _model.PlaceEnclosure(_pendingEnclosureData, cell, _pendingRotation, card.Data.manaCost);
        SpawnEnclosureView(instance);
        RebuildPathViews();
        Game.LogState($"Played '{card.Data.cardName}' → placed enclosure");
        SetMode(Mode.None);
    }

    private void TryApplyAmplify(Vector3 hit)
    {
        var card = (AmplifyCardData)_pendingCard.Data;
        var cell = WorldToCell(hit);
        if (!InCellBounds(cell)) return;
        var target = _model.GetCell(cell.x, cell.y);
        if (target == null) return;

        if (!Game.TryPlayCard(_pendingCard)) return;

        if (card.durationDays <= 0) target.AddPermanentBonus(card.bonusAmount);
        else                        target.AddTimedBonus(card.bonusAmount, Game.CurrentDay + card.durationDays);

        Game.LogState($"Played '{card.cardName}' → +{card.bonusAmount} bonus on enclosure at {target.Bounds.min}");
        SetMode(Mode.None);
    }

    private void TrySelectMoveSource(Vector3 hit)
    {
        var cell = WorldToCell(hit);
        if (!InCellBounds(cell)) return;
        var target = _model.GetCell(cell.x, cell.y);
        if (target == null) return;

        _moveSource   = target;
        _moveRotation = target.Rotation;
        _mode         = Mode.SelectMoveDestination;
    }

    private void TryCompleteMove(Vector3 hit)
    {
        var cell = WorldToPivot(hit, _moveSource.Data, _moveRotation);

        if (cell == _moveSource.PivotHalf && _moveRotation == _moveSource.Rotation) return;
        if (!_model.CanPlaceEnclosureIgnoring(_moveSource, _moveSource.Data, cell, _moveRotation)) return;

        var card   = _pendingCard;
        var source = _moveSource;
        if (!Game.TryPlayCard(card)) return;

        _model.MoveEnclosure(source, cell, _moveRotation);
        RefreshEnclosureView(source);
        RebuildPathViews();

        Game.LogState($"Played '{card.Data.cardName}' → moved enclosure");
        SetMode(Mode.None);
    }

    private void TryTogglePath(Vector3 hit)
    {
        if (!TrySnapToEdge(hit, out bool horiz, out int ex, out int ey)) return;
        bool toggled = horiz ? _model.ToggleHEdge(ex, ey) : _model.ToggleVEdge(ex, ey);
        TrackDraggedPathEdge(horiz, ex, ey);
        if (toggled) RebuildPathViews();
    }

    private void TryPaintPath(Vector3 hit)
    {
        if (!TrySnapToEdgeAligned(hit, out bool horiz, out int ex, out int ey)) return;
        if (HasDraggedPathEdge(horiz, ex, ey)) return;

        bool placed = horiz ? _model.PlaceHEdge(ex, ey) : _model.PlaceVEdge(ex, ey);
        TrackDraggedPathEdge(horiz, ex, ey);
        if (placed) RebuildPathViews();
    }

    private void BeginPathDrag(Vector3 hit)
    {
        if (_isPathDragging) return;
        _isPathDragging = true;
        _hasPathDragDirectionLock = false;
        _pathDragAnchorLocal = new Vector2((hit.x - _origin.x) / cellSize, (hit.z - _origin.z) / cellSize);
        _draggedPathEdgesH.Clear();
        _draggedPathEdgesV.Clear();
    }

    private void TrackDraggedPathEdge(bool horiz, int ex, int ey)
    {
        var edge = new Vector2Int(ex, ey);
        if (horiz) _draggedPathEdgesH.Add(edge);
        else       _draggedPathEdgesV.Add(edge);
    }

    private bool HasDraggedPathEdge(bool horiz, int ex, int ey)
    {
        var edge = new Vector2Int(ex, ey);
        return horiz ? _draggedPathEdgesH.Contains(edge) : _draggedPathEdgesV.Contains(edge);
    }

    private void TryRemoveAt(Vector3 hit)
    {
        var cell = WorldToCell(hit);
        if (!InCellBounds(cell)) return;
        var instance = _model.GetCell(cell.x, cell.y);
        if (instance == null) return;

        DespawnEnclosure(instance);
        int refund = Game.RefundForEnclosure(instance);

        Game.LogState($"Removed '{instance.Data.enclosureName}' → refunded {refund} mana");
    }

    public void DespawnEnclosure(EnclosureInstance instance)
    {
        if (instance == null) return;

        _model.RemoveEnclosure(instance);

        if (_enclosureViews.TryGetValue(instance, out var go))
        {
            Destroy(go);
            _enclosureViews.Remove(instance);
        }
    }

    private void UpdateHoverPreview(Vector3 hit)
    {
        switch (_mode)
        {
            case Mode.Enclosure:
                _edgePreview.SetActive(false);
                if (_pendingEnclosureData == null) { HideFootprintPreview(); break; }
                var cell = WorldToPivot(hit, _pendingEnclosureData, _pendingRotation);
                bool ok  = _model.CanPlaceEnclosure(_pendingEnclosureData, cell, _pendingRotation);
                ShowFootprintPreview(_pendingEnclosureData, cell, _pendingRotation,
                                     ok ? previewValid : previewInvalid);
                break;

            case Mode.Path:
                HideFootprintPreview();
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
                    ShowFootprintPreview(occupant.Data, occupant.PivotHalf, occupant.Rotation, previewValid);
                else
                    HideFootprintPreview();
                break;

            case Mode.SelectMoveDestination:
                _edgePreview.SetActive(false);
                var destCell = WorldToPivot(hit, _moveSource.Data, _moveRotation);
                bool destOk  = _model.CanPlaceEnclosureIgnoring(_moveSource, _moveSource.Data, destCell, _moveRotation);
                ShowFootprintPreview(_moveSource.Data, destCell, _moveRotation,
                                     destOk ? previewValid : previewInvalid);
                break;

            default:
                HideFootprintPreview();
                _edgePreview.SetActive(false);
                break;
        }
    }

    private void ShowFootprintPreview(EnclosureData data, Vector2Int origin, int rotation, Color color)
    {
        for (int i = 0; i < data.CellCount; i++)
        {
            var sr = GetPreviewQuad(i);
            PlaceFootprint(sr.transform,
                CellCenterWorld(data.GetCell(i, origin, rotation), Vector2Int.one, YPreview),
                (cellSize - 0.08f, cellSize - 0.08f));
            sr.color = color;
            sr.gameObject.SetActive(true);
        }

        for (int i = data.CellCount; i < _previewQuads.Count; i++)
            _previewQuads[i].gameObject.SetActive(false);
    }

    private void HideFootprintPreview()
    {
        foreach (var sr in _previewQuads) sr.gameObject.SetActive(false);
    }

    private SpriteRenderer GetPreviewQuad(int index)
    {
        while (_previewQuads.Count <= index)
        {
            var (go, sr) = MakeFlatQuad($"Preview_Enclosure_{_previewQuads.Count}", Color.clear, SortPreview);
            go.SetActive(false);
            _previewQuads.Add(sr);
        }
        return _previewQuads[index];
    }

    private void SpawnIsland()
    {
        if (islandPrefab != null)
        {
            Instantiate(islandPrefab, transform.position, Quaternion.identity, transform);
        }
        else
        {
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

    private const float PlaceholderCubeHeight = 0.2f;

    private Vector3 EnclosurePivotWorld(EnclosureInstance instance, float worldY)
        => HalfPointWorld(instance.PivotHalf, worldY);

    private void SpawnEnclosureView(EnclosureInstance instance)
    {
        GameObject go;
        var rotation = Quaternion.Euler(0f, 90f * instance.Rotation, 0f);

        if (instance.Data.prefab != null)
        {
            go = Instantiate(instance.Data.prefab,
                             EnclosurePivotWorld(instance, 0f) + rotation * instance.Data.prefabOffset,
                             rotation, transform);
        }
        else
        {
            go = new GameObject("Enclosure");
            go.transform.SetParent(transform);
            go.transform.position = EnclosurePivotWorld(instance, 0f);

            foreach (var c in instance.Cells)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.SetParent(go.transform);
                cube.transform.position   = CellCenterWorld(c, Vector2Int.one, PlaceholderCubeHeight * 0.5f);
                cube.transform.localScale = new Vector3(cellSize, PlaceholderCubeHeight, cellSize) * 0.9f;
                cube.GetComponent<Renderer>().material.color = instance.Data.footprintColor;
            }
        }

        var bounds = instance.Bounds;
        go.name = $"Enclosure_{bounds.xMin}_{bounds.yMin}";
        _enclosureViews[instance] = go;
    }

    private void RefreshEnclosureView(EnclosureInstance instance)
    {
        if (_enclosureViews.TryGetValue(instance, out var old))
        {
            Destroy(old);
            _enclosureViews.Remove(instance);
        }
        SpawnEnclosureView(instance);
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

    private bool TrySnapToEdge(Vector3 hit, out bool horiz, out int ex, out int ey)
        => TrySnapToEdge(hit, edgeSnapDist, out horiz, out ex, out ey);

    private bool TrySnapToEdge(Vector3 hit, float snapDist, out bool horiz, out int ex, out int ey)
    {
        float gx = (hit.x - _origin.x) / cellSize;
        float gz = (hit.z - _origin.z) / cellSize;

        int   hx = Mathf.FloorToInt(gx);
        int   hy = Mathf.RoundToInt(gz);
        float dh = Mathf.Abs(gz - hy) * cellSize;
        bool hOk = hx >= 0 && hx < _model.Width && hy >= 0 && hy <= _model.Height
                   && dh < snapDist && !_model.IsHEdgeBlocked(hx, hy);

        int   vx = Mathf.RoundToInt(gx);
        int   vy = Mathf.FloorToInt(gz);
        float dv = Mathf.Abs(gx - vx) * cellSize;
        bool vOk = vx >= 0 && vx <= _model.Width && vy >= 0 && vy < _model.Height
                   && dv < snapDist && !_model.IsVEdgeBlocked(vx, vy);

        if (hOk && (!vOk || dh <= dv)) { horiz = true;  ex = hx; ey = hy; return true; }
        if (vOk)                        { horiz = false; ex = vx; ey = vy; return true; }

        horiz = false; ex = 0; ey = 0;
        return false;
    }

    private bool TrySnapToEdgeAligned(Vector3 hit, out bool horiz, out int ex, out int ey)
    {
        float gx = (hit.x - _origin.x) / cellSize;
        float gz = (hit.z - _origin.z) / cellSize;

        if (!_hasPathDragDirectionLock)
        {
            float dx = Mathf.Abs(gx - _pathDragAnchorLocal.x);
            float dz = Mathf.Abs(gz - _pathDragAnchorLocal.y);
            if (Mathf.Max(dx, dz) < dragDirectionLockDist)
            {
                horiz = false; ex = 0; ey = 0;
                return false;
            }

            _pathDragHorizontal = dx >= dz;
            _hasPathDragDirectionLock = true;
        }

        if (_pathDragHorizontal)
        {
            int hx = Mathf.FloorToInt(gx);
            int hy = Mathf.RoundToInt(gz);
            float dh = Mathf.Abs(gz - hy) * cellSize;
            bool hOk = hx >= 0 && hx < _model.Width && hy >= 0 && hy <= _model.Height
                       && dh < dragEdgeSnapDist && !_model.IsHEdgeBlocked(hx, hy);
            if (!hOk)
            {
                horiz = false; ex = 0; ey = 0;
                return false;
            }

            horiz = true; ex = hx; ey = hy;
            return true;
        }
        else
        {
            int vx = Mathf.RoundToInt(gx);
            int vy = Mathf.FloorToInt(gz);
            float dv = Mathf.Abs(gx - vx) * cellSize;
            bool vOk = vx >= 0 && vx <= _model.Width && vy >= 0 && vy < _model.Height
                       && dv < dragEdgeSnapDist && !_model.IsVEdgeBlocked(vx, vy);
            if (!vOk)
            {
                horiz = false; ex = 0; ey = 0;
                return false;
            }

            horiz = false; ex = vx; ey = vy;
            return true;
        }
    }

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

    private void SetMode(Mode m)
    {
        _mode = m;
        _pendingCard          = null;
        _pendingEnclosureData = null;
        _pendingRotation      = 0;
        _moveSource           = null;
        _moveRotation         = 0;
        HideFootprintPreview();
        if (_edgePreview != null) _edgePreview.SetActive(false);
        ResetPathDragState();
        OnPendingCardChanged?.Invoke();
    }

    private void ResetPathDragState()
    {
        _isPathDragging = false;
        _hasPathDragDirectionLock = false;
        _pathDragHorizontal = false;
        _pathDragAnchorLocal = default;
        _draggedPathEdgesH.Clear();
        _draggedPathEdgesV.Clear();
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

    private Vector3 G2W(float localX, float localZ, float worldY)
        => new(_origin.x + localX, worldY, _origin.z + localZ);

    private Vector3 CellCenterWorld(Vector2Int cell, Vector2Int size, float worldY)
        => G2W((cell.x + size.x * 0.5f) * cellSize, (cell.y + size.y * 0.5f) * cellSize, worldY);

    private Vector2Int WorldToCell(Vector3 world)
        => new(Mathf.FloorToInt((world.x - _origin.x) / cellSize),
               Mathf.FloorToInt((world.z - _origin.z) / cellSize));

    private Vector2Int WorldToPivot(Vector3 world, EnclosureData data, int rotation)
    {
        var parity = data.GetOriginParity(rotation);
        return new Vector2Int(
            SnapToParity((world.x - _origin.x) / cellSize * 2f, parity.x),
            SnapToParity((world.z - _origin.z) / cellSize * 2f, parity.y));
    }

    private static int SnapToParity(float value, int parity)
        => Mathf.RoundToInt((value - parity) * 0.5f) * 2 + parity;

    private Vector3 HalfPointWorld(Vector2Int half, float worldY)
        => G2W(half.x * 0.5f * cellSize, half.y * 0.5f * cellSize, worldY);

    private bool InCellBounds(Vector2Int cell)
        => cell.x >= 0 && cell.y >= 0 && cell.x < _model.Width && cell.y < _model.Height;

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
