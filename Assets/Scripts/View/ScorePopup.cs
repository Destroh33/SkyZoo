using TMPro;
using UnityEngine;

// A single floating score number spawned in world space above a scoring
// enclosure: drifts upward while oscillating side to side in a sine-wave
// squiggle, fades out, then destroys itself. Spawn via ScorePopup.Spawn(...).
public class ScorePopup : MonoBehaviour
{
    [SerializeField] private float riseHeight   = 1.5f;
    [SerializeField] private float duration     = 1.5f;
    [SerializeField] private float squiggleAmplitude = 0.08f;
    [SerializeField] private float squiggleFrequency = 1.5f;
    [SerializeField] private float fontSize     = 4f;

    private TextMeshPro _text;
    private Vector3     _origin;
    private float       _elapsed;
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
        _origin  = transform.position;
        _cam     = Camera.main;

        _text = gameObject.AddComponent<TextMeshPro>();
        _text.text      = (score >= 0 ? "+" : "") + score.ToString("0.#");
        _text.fontSize   = fontSize;
        _text.alignment   = TextAlignmentOptions.Center;
        _text.color       = Color.white;
        _text.fontStyle    = FontStyles.Bold;
    }

    void Update()
    {
        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / duration);

        float yOffset = riseHeight * t;
        float xOffset = Mathf.Sin(t * squiggleFrequency * Mathf.PI * 2f) * squiggleAmplitude * (1f - t);

        transform.position = _origin + new Vector3(xOffset, yOffset, 0f);

        if (_cam != null)
            transform.rotation = _cam.transform.rotation; // billboard toward the camera

        if (_text != null)
        {
            var c = _text.color;
            c.a = 1f - t;
            _text.color = c;
        }

        if (_elapsed >= duration) Destroy(gameObject);
    }
}
