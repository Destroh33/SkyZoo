using System;
using UnityEngine;
using UnityEngine.UI;

public class ScoreMote : MonoBehaviour
{
    private RectTransform _rect;
    private Image         _image;
    private Vector2       _from;
    private Vector2       _to;
    private Vector2       _control;
    private float         _elapsed;
    private float         _duration;
    private float         _delay;
    private float         _spin;
    private Action        _onArrive;

    public static ScoreMote Spawn(RectTransform layer, Vector2 from, Vector2 to, Sprite sprite,
                                  Color tint, float size, float duration, float delay, Action onArrive)
    {
        var go = new GameObject("ScoreMote", typeof(RectTransform));
        go.transform.SetParent(layer, false);

        var mote = go.AddComponent<ScoreMote>();
        mote._rect = go.GetComponent<RectTransform>();
        mote._rect.anchorMin = mote._rect.anchorMax = mote._rect.pivot = new Vector2(0.5f, 0.5f);
        mote._rect.sizeDelta = new Vector2(size, size);
        mote._rect.anchoredPosition = from;
        mote._rect.localScale = Vector3.zero;

        mote._image = go.AddComponent<Image>();
        mote._image.sprite         = sprite;
        mote._image.color          = tint;
        mote._image.preserveAspect = true;
        mote._image.raycastTarget  = false;

        var mid = (from + to) * 0.5f;
        mote._from     = from;
        mote._to       = to;
        mote._control  = mid + new Vector2(UnityEngine.Random.Range(-140f, 140f),
                                           Mathf.Abs(to.y - from.y) * 0.25f + UnityEngine.Random.Range(60f, 190f));
        mote._duration = duration;
        mote._delay    = delay;
        mote._spin     = UnityEngine.Random.Range(-220f, 220f);
        mote._onArrive = onArrive;

        return mote;
    }

    void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        if (_delay > 0f)
        {
            _delay -= dt;
            return;
        }

        _elapsed += dt;
        float t = Mathf.Clamp01(_elapsed / _duration);
        float e = t * t;

        var a = Vector2.Lerp(_from, _control, e);
        var b = Vector2.Lerp(_control, _to, e);
        _rect.anchoredPosition = Vector2.Lerp(a, b, e);
        _rect.localEulerAngles = new Vector3(0f, 0f, _spin * _elapsed);

        float pop   = Mathf.Clamp01(_elapsed / 0.12f);
        float scale = UiSpring.EaseOutBack(pop) * Mathf.Lerp(1f, 0.55f, e);
        _rect.localScale = new Vector3(scale, scale, 1f);

        var color = _image.color;
        color.a = 1f - Mathf.Clamp01((t - 0.82f) / 0.18f);
        _image.color = color;

        if (t < 1f) return;

        _onArrive?.Invoke();
        Destroy(gameObject);
    }
}
