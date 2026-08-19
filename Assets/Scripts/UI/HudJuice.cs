using UnityEngine;
using UnityEngine.UI;

public class HudJuice : MonoBehaviour
{
    [Header("Parts")]
    [SerializeField] private RectTransform content;
    [SerializeField] private Image screenFlash;

    [Header("Feel")]
    [SerializeField] private float shakeDecay = 5.5f;
    [SerializeField] private float shakeSpeed = 42f;
    [SerializeField] private float flashDecay = 2.4f;

    private float _shake;
    private float _seed;
    private float _flashAmount;
    private Color _flashColor;

    void Awake()
    {
        if (content == null) content = (RectTransform)transform;
        _seed = Random.Range(0f, 100f);
    }

    public void Shake(float amount) => _shake = Mathf.Max(_shake, amount);

    public void Flash(Color color, float amount = 0.35f)
    {
        _flashColor  = color;
        _flashAmount = Mathf.Max(_flashAmount, amount);
    }

    void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        float t  = Time.unscaledTime * shakeSpeed + _seed;

        if (_shake > 0.001f)
        {
            _shake = Mathf.Max(0f, _shake - _shake * shakeDecay * dt - dt * 0.5f);
            content.anchoredPosition = new Vector2((Mathf.PerlinNoise(t, 0f) - 0.5f) * 2f * _shake,
                                                   (Mathf.PerlinNoise(0f, t) - 0.5f) * 2f * _shake);
        }
        else if (content.anchoredPosition != Vector2.zero)
        {
            content.anchoredPosition = Vector2.zero;
        }

        if (screenFlash == null || _flashAmount <= 0f) return;

        _flashAmount = Mathf.Max(0f, _flashAmount - dt * flashDecay);
        var color = _flashColor;
        color.a = _flashAmount;
        screenFlash.color = color;
    }

    public static HudJuice Build(RectTransform contentRoot, Transform flashParent)
    {
        var juice = contentRoot.gameObject.AddComponent<HudJuice>();
        juice.content = contentRoot;

        var flashGO = CardFactory.Stretch(flashParent, "ScreenFlash", 0f);
        flashGO.transform.SetAsFirstSibling();

        juice.screenFlash = flashGO.AddComponent<Image>();
        juice.screenFlash.color         = new Color(1f, 1f, 1f, 0f);
        juice.screenFlash.raycastTarget = false;

        return juice;
    }
}
