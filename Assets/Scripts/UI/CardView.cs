using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class CardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public CardData     Data      { get; private set; }
    public CardInstance Instance  { get; private set; }
    public int          SlotIndex { get; set; }

    private CardBinder _binder;
    private Action<CardView> _onClick;

    private Vector2 _restPos;
    private float   _restRot;

    private float _hoverLift;
    private float _selectLift;
    private float _hoverScale;
    private float _selectScale;

    private Vector2 _pos;
    private Vector2 _posVel;
    private float   _rot;
    private float   _rotVel;
    private float   _scale = 1f;
    private float   _scaleVel;
    private float   _glowAlpha;

    private bool  _hovered;
    private bool  _selected;
    private bool  _dealt;
    private bool  _interactable = true;
    private float _dealDelay;

    private const float PosStiffness   = 320f;
    private const float PosDamping     = 22f;
    private const float RotStiffness   = 260f;
    private const float RotDamping     = 20f;
    private const float ScaleStiffness = 620f;
    private const float ScaleDamping   = 17f;

    public void Init(CardBinder binder, CardData data, CardInstance instance,
                     float hoverLift, float selectLift, float hoverScale, float selectScale,
                     Action<CardView> onClick)
    {
        _binder      = binder;
        Data         = data;
        Instance     = instance;
        _hoverLift   = hoverLift;
        _selectLift  = selectLift;
        _hoverScale  = hoverScale;
        _selectScale = selectScale;
        _onClick     = onClick;

        SlotIndex = transform.GetSiblingIndex();
    }

    public void SetSlot(Vector2 restPos, float restRotation, int dealIndex)
    {
        _restPos = restPos;
        _restRot = restRotation;

        if (_dealt) return;

        _dealt     = true;
        _pos       = restPos + new Vector2(0f, -260f);
        _rot       = restRotation - 25f;
        _scale     = 0.7f;
        _glowAlpha = 0f;
        _dealDelay = dealIndex * 0.05f;

        if (_binder.group != null) _binder.group.alpha = 0f;
        Apply();
    }

    public void SetSelected(bool selected)
    {
        if (_selected == selected) return;
        _selected = selected;
        if (selected) _scaleVel += 6f;
    }

    public void SetInteractable(bool value)
    {
        _interactable = value;
        if (!value) _hovered = false;
        if (_binder.frame != null) _binder.frame.raycastTarget = value;
    }

    void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        if (_dealDelay > 0f)
        {
            _dealDelay -= dt;
            return;
        }

        if (_binder.group != null)
            _binder.group.alpha = Mathf.MoveTowards(_binder.group.alpha, _interactable ? 1f : 0.5f, dt * 6f);

        bool lifted = _hovered || _selected;

        Vector2 targetPos = _restPos;
        targetPos.y += _selected ? _selectLift : (_hovered ? _hoverLift : 0f);

        float targetRot   = lifted ? 0f : _restRot;
        float targetScale = _selected ? _selectScale : (_hovered ? _hoverScale : 1f);

        Spring(ref _pos.x, ref _posVel.x, targetPos.x, PosStiffness, PosDamping, dt);
        Spring(ref _pos.y, ref _posVel.y, targetPos.y, PosStiffness, PosDamping, dt);
        Spring(ref _rot,   ref _rotVel,   targetRot,   RotStiffness, RotDamping, dt);
        Spring(ref _scale, ref _scaleVel, targetScale, ScaleStiffness, ScaleDamping, dt);

        _glowAlpha = Mathf.MoveTowards(_glowAlpha, _selected ? 1f : (_hovered ? 0.45f : 0f), dt * 7f);

        if (_binder.frame != null)
            _binder.frame.color = Color.Lerp(_binder.frameColor, _binder.selectedColor,
                                             _selected ? 1f : (_hovered ? 0.35f : 0f));

        Apply();
    }

    private void Apply()
    {
        var rt = _binder.rect;
        rt.anchoredPosition = _pos;
        rt.localEulerAngles = new Vector3(0f, 0f, _rot);
        rt.localScale       = new Vector3(_scale, _scale, 1f);

        if (_binder.glow == null) return;
        _binder.glow.color = new Color(_binder.glowColor.r, _binder.glowColor.g,
                                       _binder.glowColor.b, _glowAlpha);
    }

    private static void Spring(ref float value, ref float velocity, float target, float stiffness, float damping, float dt)
    {
        velocity += (target - value) * stiffness * dt;
        velocity *= Mathf.Exp(-damping * dt);
        value    += velocity * dt;
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (_hovered || !_interactable) return;
        _hovered   = true;
        _scaleVel += 3.5f;
        transform.SetAsLastSibling();
        Sfx.CardHover();
    }

    public void OnPointerExit(PointerEventData e)
    {
        _hovered = false;
        if (!_selected) RestoreSiblingOrder();
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (!_interactable) return;
        _scaleVel += 8f;
        _onClick?.Invoke(this);
    }

    public void RestoreSiblingOrder() => transform.SetSiblingIndex(SlotIndex);
}
