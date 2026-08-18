using UnityEngine;

public static class UISprites
{
    private const int   PanelSize   = 64;
    private const float PanelRadius = 16f;
    private const int   CircleSize  = 64;

    private static Sprite _roundedRect;
    private static Sprite _circle;

    public static Sprite RoundedRect => _roundedRect != null ? _roundedRect : _roundedRect = BuildRoundedRect();
    public static Sprite Circle      => _circle      != null ? _circle      : _circle      = BuildCircle();

    private static Sprite BuildRoundedRect()
    {
        var tex    = NewTexture(PanelSize, PanelSize);
        var pixels = new Color32[PanelSize * PanelSize];
        float half = PanelSize * 0.5f;

        for (int y = 0; y < PanelSize; y++)
        {
            for (int x = 0; x < PanelSize; x++)
            {
                float dx = Mathf.Abs(x + 0.5f - half) - (half - PanelRadius);
                float dy = Mathf.Abs(y + 0.5f - half) - (half - PanelRadius);
                float d  = new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude
                           + Mathf.Min(Mathf.Max(dx, dy), 0f) - PanelRadius;

                pixels[y * PanelSize + x] = WhiteWithAlpha(Mathf.Clamp01(0.5f - d));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        float b = PanelRadius + 2f;
        return Sprite.Create(tex, new Rect(0f, 0f, PanelSize, PanelSize), new Vector2(0.5f, 0.5f),
                             100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
    }

    private static Sprite BuildCircle()
    {
        var tex    = NewTexture(CircleSize, CircleSize);
        var pixels = new Color32[CircleSize * CircleSize];
        float half = CircleSize * 0.5f;

        for (int y = 0; y < CircleSize; y++)
        {
            for (int x = 0; x < CircleSize; x++)
            {
                float d = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude - (half - 1f);
                pixels[y * CircleSize + x] = WhiteWithAlpha(Mathf.Clamp01(0.5f - d));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0f, 0f, CircleSize, CircleSize), new Vector2(0.5f, 0.5f));
    }

    private static Texture2D NewTexture(int width, int height)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            hideFlags  = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp
        };
        return tex;
    }

    private static Color32 WhiteWithAlpha(float a) => new(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
}
