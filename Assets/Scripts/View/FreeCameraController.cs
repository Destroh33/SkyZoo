using UnityEngine;
using UnityEngine.InputSystem;

// FPS-style free camera. Enabled/disabled by GridView on Tab.
// Cursor locks on enable, unlocks on disable.
// Controls:
//   Mouse move    look around (always, no button hold)
//   WASD          fly relative to camera facing
//   Q / E         move down / up (world Y)
//   Shift         sprint
//   Scroll        adjust move speed
[RequireComponent(typeof(Camera))]
public class FreeCameraController : MonoBehaviour
{
    [SerializeField] private float moveSpeed        = 8f;
    [SerializeField] private float lookSensitivity  = 0.15f;
    [SerializeField] private float sprintMultiplier = 4f;

    private float _yaw;
    private float _pitch;

    void OnEnable()
    {
        var e  = transform.eulerAngles;
        _yaw   = e.y;
        _pitch = e.x > 180f ? e.x - 360f : e.x;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    void Update()
    {
        var mouse = Mouse.current;
        var kb    = Keyboard.current;
        if (mouse == null || kb == null) return;

        // Always rotate from mouse delta — FPS style, no button required
        Vector2 delta = mouse.delta.ReadValue();
        _yaw   += delta.x * lookSensitivity;
        _pitch -= delta.y * lookSensitivity;
        _pitch  = Mathf.Clamp(_pitch, -89f, 89f);
        transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);

        // Scroll adjusts base move speed
        float scroll = mouse.scroll.ReadValue().y;
        if (scroll != 0f)
            moveSpeed = Mathf.Max(0.5f, moveSpeed + scroll * 0.005f);

        // Move
        float speed = moveSpeed * (kb.shiftKey.isPressed ? sprintMultiplier : 1f);

        Vector3 dir = Vector3.zero;
        if (kb.wKey.isPressed) dir += transform.forward;
        if (kb.sKey.isPressed) dir -= transform.forward;
        if (kb.dKey.isPressed) dir += transform.right;
        if (kb.aKey.isPressed) dir -= transform.right;
        if (kb.eKey.isPressed) dir += Vector3.up;
        if (kb.qKey.isPressed) dir -= Vector3.up;

        if (dir.sqrMagnitude > 0f)
            transform.position += dir.normalized * (speed * Time.deltaTime);
    }
}
