using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

public class ScorePopup : MonoBehaviour
{
    [SerializeField] private float riseHeight        = 1.9f;
    [SerializeField] private float duration          = 1.6f;
    [SerializeField] private float squiggleAmplitude = 0.10f;
    [SerializeField] private float squiggleFrequency = 1.5f;
    [SerializeField] private float fontSize          = 4f;

    [Header("Juice")]
    [SerializeField] private float popInFraction   = 0.18f;
    [SerializeField] private float shrinkFraction  = 0.22f;
    [SerializeField] private float holdFraction    = 0.55f;
    [SerializeField] private float overshoot       = 1.9f;
    [SerializeField] private float tiltDegrees     = 9f;
    [SerializeField] private float bigScore        = 25f;
    [SerializeField] private float bigScoreScale   = 1.55f;

    private static readonly Color SmallColor = new(1f, 1f, 1f, 1f);
    private static readonly Color MidColor   = new(1f, 0.92f, 0.45f, 1f);
    private static readonly Color BigColor   = new(1f, 0.62f, 0.22f, 1f);

    private static readonly int ZTestMode    = Shader.PropertyToID("_ZTestMode");
    private static readonly int OutlineWidth = Shader.PropertyToID("_OutlineWidth");
    private static readonly int OutlineColor = Shader.PropertyToID("_OutlineColor");

    private static Material _onTopMaterial;
    private static int      _waveStep;
    private static float    _lastSpawnTime = -99f;

    private TextMeshPro _text;
    private Vector3     _origin;
    private float       _elapsed;
    private float       _phase;
    private float       _tilt;
    private float       _drift;
    private float       _baseScale = 1f;
    private Color       _tint;
    private Camera      _cam;

    public static ScorePopup Spawn(Vector3 worldPos, float score, Transform parent)
    {
        var go = new GameObject("ScorePopup");
        go.transform.SetParent(parent, worldPositionStays: true);
        go.transform.position = worldPos;

        var popup = go.AddComponent<ScorePopup>();
        popup.Init(score);
        return popup;
    }

    private void Init(float score)
    {
        _origin = transform.position;
        _cam    = Camera.main;

        _phase = Random.Range(0f, Mathf.PI * 2f);
        _tilt  = Random.Range(-tiltDegrees, tiltDegrees);
        _drift = Random.Range(-0.12f, 0.12f);

        float weight = Mathf.Clamp01(Mathf.Abs(score) / Mathf.Max(0.01f, bigScore));
        _baseScale = Mathf.Lerp(1f, bigScoreScale, weight);
        _tint      = weight < 0.5f
            ? Color.Lerp(SmallColor, MidColor, weight * 2f)
            : Color.Lerp(MidColor, BigColor, (weight - 0.5f) * 2f);

        _text = gameObject.AddComponent<TextMeshPro>();
        _text.text      = (score >= 0 ? "+" : "") + score.ToString("0.#");
        _text.fontSize  = fontSize;
        _text.alignment = TextAlignmentOptions.Center;
        _text.color     = _tint;
        _text.fontStyle = FontStyles.Bold;

        DrawOnTop();

        transform.localScale = Vector3.zero;

        if (Time.time - _lastSpawnTime > 1f) _waveStep = 0;
        _lastSpawnTime = Time.time;
        Sfx.ScorePop(_waveStep++);
    }

    private void DrawOnTop()
    {
        if (_onTopMaterial == null)
        {
            _onTopMaterial = new Material(_text.fontSharedMaterial)
            {
                name      = "ScorePopup_OnTop",
                hideFlags = HideFlags.HideAndDontSave
            };

            if (_onTopMaterial.HasProperty(ZTestMode))
                _onTopMaterial.SetFloat(ZTestMode, (float)CompareFunction.Always);

            if (_onTopMaterial.HasProperty(OutlineWidth))
            {
                _onTopMaterial.EnableKeyword("OUTLINE_ON");
                _onTopMaterial.SetFloat(OutlineWidth, 0.22f);
                _onTopMaterial.SetColor(OutlineColor, new Color(0.05f, 0.05f, 0.08f, 1f));
            }

            _onTopMaterial.renderQueue = (int)RenderQueue.Overlay;
        }

        _text.fontSharedMaterial = _onTopMaterial;

        var meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.sortingOrder  = 32000;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows    = false;
        }
    }

    void Update()
    {
        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / duration);

        float rise    = riseHeight * (1f - (1f - t) * (1f - t));
        float wobble  = Mathf.Sin(_phase + t * squiggleFrequency * Mathf.PI * 2f)
                        * squiggleAmplitude * (1f - t);

        transform.position = _origin + new Vector3(wobble + _drift * t, rise, 0f);

        if (_cam != null)
            transform.rotation = _cam.transform.rotation * Quaternion.Euler(0f, 0f, _tilt * (1f - t));

        float scale = _baseScale * ScaleCurve(t);
        transform.localScale = new Vector3(scale, scale, scale);

        if (_text != null)
        {
            var c = _tint;
            c.a = Alpha(t);
            _text.color = c;
        }

        if (_elapsed >= duration) Destroy(gameObject);
    }

    private float ScaleCurve(float t)
    {
        if (t < popInFraction)
            return EaseOutBack(t / popInFraction);

        float shrinkStart = 1f - shrinkFraction;
        if (t > shrinkStart)
        {
            float x = (t - shrinkStart) / shrinkFraction;
            return Mathf.Lerp(1f, 0.7f, x * x);
        }

        return 1f;
    }

    private float Alpha(float t)
    {
        if (t < popInFraction) return Mathf.Clamp01(t / popInFraction * 2f);
        if (t < holdFraction)  return 1f;
        return 1f - (t - holdFraction) / (1f - holdFraction);
    }

    private float EaseOutBack(float x)
    {
        float c1 = overshoot;
        float c3 = c1 + 1f;
        float p  = x - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }
}
