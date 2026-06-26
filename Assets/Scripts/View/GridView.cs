using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Isometric 3D grid view — grid lives in the XZ plane, Y is up.
// The GridView GameObject's world position is the CENTER of the grid.
// Hotkeys (bound in InputSystem_Actions → Grid map):
//   1-9   select enclosure slot → enclosure placement mode
//   P     path placement mode
//   Esc   cancel / clear mode
//   LMB   place enclosure / toggle path edge
//   RMB   remove enclosure under cursor
public class GridView : MonoBehaviour
{
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
    [SerializeField] private float lineThickness  = 0.05f;
    [SerializeField] private float pathThickness  = 0.18f;
    [SerializeField] private float edgeSnapDist   = 0.25f;

    [Header("Camera")]
    [SerializeField] private float camPitch       = 30f;
    [SerializeField] private float camYaw         = 45f;
    [SerializeField] private float camZoom        = 1f;   // >1 zooms out, <1 zooms in
    [SerializeField] private float perspectiveFov = 60f;

    [Header("Test Enclosures (assign in Inspector)")]
    [SerializeField] private EnclosureData[] testEnclosures;

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

    // ── Runtime state ────────────────────────────────────────────────────────
    private GridModel _model;
    private Sprite    _whiteSprite;

    // World-space position of grid corner (0,0) — set in Start from transform.position
    private Vector3 _origin;

    private readonly Dictionary<EnclosureInstance, GameObject> _enclosureViews = new();
    private readonly List<GameObject>                          _pathViews       = new();

    private GameObject     _enclosurePreview;
    private SpriteRenderer _enclosurePreviewSr;
    private GameObject     _edgePreview;

    private enum Mode { None, Enclosure, Path }
    private Mode          _mode;
    private EnclosureData _pendingEnclosure;


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
    }

    void OnDisable()
    {
        _clickAction.performed      -= OnClick;
        _removeAction.performed     -= OnRemoveEnclosure;
        _pathModeAction.performed   -= OnPathMode;
        _cancelAction.performed     -= OnCancel;
        _selectSlotAction.performed -= OnSelectSlot;
        _toggleCamAction.performed  -= OnToggleCamera;
        inputActions.FindActionMap("Grid").Disable();
    }

    void Start()
    {
        // Grid is centered on this transform — origin is the (0,0) corner offset
        _origin = transform.position + new Vector3(
            -gridWidth  * cellSize * 0.5f,
            0f,
            -gridHeight * cellSize * 0.5f);

        _model       = new GridModel(gridWidth, gridHeight);
        _whiteSprite = MakeWhiteSprite();

        SpawnIsland();
        BuildGridLines();

        (_enclosurePreview, _enclosurePreviewSr) = MakeFlatQuad("Preview_Enclosure", Color.clear,    SortPreview);
        _enclosurePreview.SetActive(false);

        (_edgePreview, _) = MakeFlatQuad("Preview_Edge", edgeHoverColor, SortEdgeHover);
        _edgePreview.SetActive(false);

        // Find or add FreeCameraController on the main camera
        _freeCam  = Camera.main.GetComponent<FreeCameraController>();
        _freeCam ??= Camera.main.gameObject.AddComponent<FreeCameraController>();
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
        if (int.TryParse(ctx.control.name, out int num) && num >= 1 && num <= testEnclosures.Length
            && testEnclosures[num - 1] != null)
        {
            _pendingEnclosure = testEnclosures[num - 1];
            _mode = Mode.Enclosure;
        }
    }

    private void OnPathMode(InputAction.CallbackContext ctx)
    {
        _mode = Mode.Path;
        _enclosurePreview.SetActive(false);
    }

    private void OnCancel(InputAction.CallbackContext ctx) => SetMode(Mode.None);

    private void OnClick(InputAction.CallbackContext ctx)
    {
        if (!TryGetGroundHit(out Vector3 hit)) return;
        switch (_mode)
        {
            case Mode.Enclosure: TryPlaceEnclosure(hit); break;
            case Mode.Path:      TryTogglePath(hit);     break;
        }
    }

    private void OnRemoveEnclosure(InputAction.CallbackContext ctx)
    {
        if (TryGetGroundHit(out Vector3 hit)) TryRemoveAt(hit);
    }

    // ── Placement logic ──────────────────────────────────────────────────────

    private void TryPlaceEnclosure(Vector3 hit)
    {
        if (_pendingEnclosure == null) return;
        var cell = WorldToCell(hit);
        if (!_model.CanPlaceEnclosure(cell, _pendingEnclosure.size)) return;
        SpawnEnclosureView(_model.PlaceEnclosure(_pendingEnclosure, cell));
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
        if (_enclosureViews.TryGetValue(instance, out var go))
        {
            Destroy(go);
            _enclosureViews.Remove(instance);
        }
    }

    // ── Hover preview ────────────────────────────────────────────────────────

    private void UpdateHoverPreview(Vector3 hit)
    {
        switch (_mode)
        {
            case Mode.Enclosure:
                _edgePreview.SetActive(false);
                if (_pendingEnclosure == null) { _enclosurePreview.SetActive(false); break; }
                var cell  = WorldToCell(hit);
                var size  = _pendingEnclosure.size;
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

    private void SpawnEnclosureView(EnclosureInstance instance)
    {
        var cell   = instance.GridPosition;
        var size   = instance.Data.size;
        var center = CellCenterWorld(cell, size, 0f);

        GameObject go;
        if (instance.Data.prefab != null)
        {
            go = Instantiate(instance.Data.prefab, center + instance.Data.prefabOffset, Quaternion.identity, transform);
            go.name = $"Enclosure_{cell.x}_{cell.y}";
        }
        else
        {
            var (quad, _) = MakeFlatQuad($"Enclosure_{cell.x}_{cell.y}", instance.Data.footprintColor, 0);
            PlaceFootprint(quad.transform, CellCenterWorld(cell, size, YPath),
                (size.x * cellSize - 0.08f, size.y * cellSize - 0.08f));
            go = quad;
        }

        _enclosureViews[instance] = go;
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
        _enclosurePreview.SetActive(false);
        _edgePreview.SetActive(false);
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
