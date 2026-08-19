using UnityEngine;

public static class UISprites
{
    private const int   PanelSize   = 64;
    private const float PanelRadius = 16f;
    private const int   CircleSize  = 64;

    private static Sprite _roundedRect;
    private static Sprite _circle;

    public static Sprite RoundedRect => _roundedRect != null ? _roundedRect : _roundedRect = MakeRounded(PanelSize, PanelRadius);
    public static Sprite Circle      => _circle      != null ? _circle      : _circle      = MakeCircle(CircleSize);

    private static Texture2D RoundedTexture(int size, float radius)
    {
        radius = Mathf.Clamp(radius, 1f, size * 0.5f);

        var tex    = NewTexture(size, size);
        var pixels = new Color32[size * size];
        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                float dy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                float d  = new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude
                           + Mathf.Min(Mathf.Max(dx, dy), 0f) - radius;

                pixels[y * size + x] = WhiteWithAlpha(Mathf.Clamp01(0.5f - d));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    private static Texture2D CircleTexture(int size)
    {
        var tex    = NewTexture(size, size);
        var pixels = new Color32[size * size];
        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude - (half - 1f);
                pixels[y * size + x] = WhiteWithAlpha(Mathf.Clamp01(0.5f - d));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    private static Sprite MakeRounded(int size, float radius)
    {
        float b = Mathf.Min(radius + 2f, size * 0.5f - 1f);
        return Sprite.Create(RoundedTexture(size, radius), new Rect(0f, 0f, size, size),
                             new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                             new Vector4(b, b, b, b));
    }

    private static Sprite MakeCircle(int size)
        => Sprite.Create(CircleTexture(size), new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));

    private static Texture2D NewTexture(int width, int height)
    {
        return new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            hideFlags  = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp
        };
    }

    private static Color32 WhiteWithAlpha(float a) => new(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
}
