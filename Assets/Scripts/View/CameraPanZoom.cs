using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class CameraPanZoom : MonoBehaviour
{
    [Header("Zoom")]
    [SerializeField] private float zoomStep     = 0.1f;
    [SerializeField] private float minOrthoSize = 2f;
    [SerializeField] private float maxOrthoSize = 20f;
    [SerializeField] private bool  zoomToCursor = true;

    [Header("Pan")]
    [SerializeField] private bool  invertPan      = false;
    [SerializeField] private float maxPanDistance = 15f;

    private Camera _cam;
    private readonly Plane _ground = new(Vector3.up, Vector3.zero);

    private bool    _hasHome;
    private Vector3 _home;

    private bool    _isPanning;
    private Vector3 _grabPoint;

    void Awake() => _cam = GetComponent<Camera>();

    void Update()
    {
        if (!_hasHome)
        {
            _home    = transform.position;
            _hasHome = true;
        }

        var mouse = Mouse.current;
        if (mouse == null) return;

        HandleZoom(mouse);
        HandlePan(mouse);
    }

    private void HandleZoom(Mouse mouse)
    {
        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Approximately(scroll, 0f)) return;

        Vector2 cursor = mouse.position.ReadValue();
        Vector3 anchorBefore = default;
        bool hadAnchor = zoomToCursor && TryGroundPoint(cursor, out anchorBefore);

        float notches = Mathf.Sign(scroll) * Mathf.Min(Mathf.Abs(scroll) / 120f, 3f);
        float scale   = Mathf.Pow(1f - zoomStep, notches);

        _cam.orthographicSize = Mathf.Clamp(_cam.orthographicSize * scale, minOrthoSize, maxOrthoSize);

        if (hadAnchor && TryGroundPoint(cursor, out Vector3 anchorAfter))
            MoveRig(anchorBefore - anchorAfter);
    }

    private void HandlePan(Mouse mouse)
    {
        if (mouse.middleButton.wasPressedThisFrame)
            _isPanning = TryGroundPoint(mouse.position.ReadValue(), out _grabPoint);

        if (mouse.middleButton.wasReleasedThisFrame || !mouse.middleButton.isPressed)
        {
            _isPanning = false;
            return;
        }

        if (!_isPanning) return;
        if (!TryGroundPoint(mouse.position.ReadValue(), out Vector3 current)) return;

        Vector3 delta = _grabPoint - current;
        MoveRig(invertPan ? -delta : delta);
    }

    private void MoveRig(Vector3 worldDelta)
    {
        worldDelta.y = 0f;

        Vector3 target = transform.position + worldDelta;

        if (maxPanDistance > 0f)
        {
            Vector3 offset = target - _home;
            offset.y = 0f;
            if (offset.sqrMagnitude > maxPanDistance * maxPanDistance)
            {
                offset = offset.normalized * maxPanDistance;
                target = new Vector3(_home.x + offset.x, target.y, _home.z + offset.z);
            }
        }

        transform.position = target;
    }

    private bool TryGroundPoint(Vector2 screenPos, out Vector3 worldPos)
    {
        var ray = _cam.ScreenPointToRay(screenPos);
        if (_ground.Raycast(ray, out float dist))
        {
            worldPos = ray.GetPoint(dist);
            return true;
        }
        worldPos = default;
        return false;
    }
}
