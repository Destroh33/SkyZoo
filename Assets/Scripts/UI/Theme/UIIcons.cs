using System;
using UnityEngine;

public static class UIIcons
{
    public enum Kind { Mana, Score, Quota, Day, Path, Money }

    public const int DefaultSize = 96;

    private static readonly Sprite[] Cache = new Sprite[6];

    public static Sprite Mana  => Get(Kind.Mana);
    public static Sprite Score => Get(Kind.Score);
    public static Sprite Quota => Get(Kind.Quota);
    public static Sprite Day   => Get(Kind.Day);
    public static Sprite Path  => Get(Kind.Path);
    public static Sprite Money => Get(Kind.Money);

    public static Sprite Get(Kind kind)
    {
        int i = (int)kind;
        if (Cache[i] != null) return Cache[i];

        var tex = Render(kind, DefaultSize);
        return Cache[i] = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                                        new Vector2(0.5f, 0.5f));
    }

    public static Texture2D Render(Kind kind, int size)
    {
        var shape = ShapeFor(kind);

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags  = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp
        };

        var pixels = new Color32[size * size];
        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2((x + 0.5f - half) / half, (y + 0.5f - half) / half);
                float alpha = Mathf.Clamp01(0.5f - shape(p) * half);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    private static Func<Vector2, float> ShapeFor(Kind kind) => kind switch
    {
        Kind.Mana  => Drop,
        Kind.Score => p => Star(p, 5, 0.30f, 0.62f),
        Kind.Quota => Check,
        Kind.Day   => Sun,
        Kind.Path  => Footprints,
        _          => Coin
    };

    private static float Drop(Vector2 p)
    {
        p.y -= 0.12f;
        float bulb  = p.magnitude - 0.52f;
        float taper = Mathf.Max(Mathf.Abs(p.x) - 0.52f * Mathf.Clamp01(1f - p.y / 0.78f), p.y - 0.78f);
        return Mathf.Min(bulb, Mathf.Max(taper, -p.y));
    }

    private static float Star(Vector2 p, int points, float inner, float outer)
    {
        float angle = Mathf.Atan2(p.y, p.x) - Mathf.PI * 0.5f;
        float wave  = 0.5f + 0.5f * Mathf.Cos(angle * points);
        return p.magnitude - Mathf.Lerp(inner, outer, wave * wave);
    }

    private static float Sun(Vector2 p)
    {
        float angle = Mathf.Atan2(p.y, p.x);
        float rays  = 0.40f + 0.26f * Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 4f)), 6f);
        return Mathf.Min(p.magnitude - 0.34f, p.magnitude - rays);
    }

    private static float Check(Vector2 p)
    {
        float a = Segment(p, new Vector2(-0.52f, 0.02f), new Vector2(-0.12f, -0.40f));
        float b = Segment(p, new Vector2(-0.12f, -0.40f), new Vector2(0.55f, 0.46f));
        return Mathf.Min(a, b) - 0.15f;
    }

    private static float Footprints(Vector2 p)
    {
        float a = Ellipse(p - new Vector2(-0.26f, 0.20f), 0.20f, 0.30f);
        float b = Ellipse(p - new Vector2(0.26f, -0.22f), 0.20f, 0.30f);
        return Mathf.Min(a, b);
    }

    private static float Coin(Vector2 p)
    {
        float disc = p.magnitude - 0.62f;
        float rim  = Mathf.Abs(p.magnitude - 0.44f) - 0.05f;
        return Mathf.Max(disc, -rim);
    }

    private static float Ellipse(Vector2 p, float rx, float ry)
    {
        var scaled = new Vector2(p.x / rx, p.y / ry);
        return (scaled.magnitude - 1f) * Mathf.Min(rx, ry);
    }

    private static float Segment(Vector2 p, Vector2 a, Vector2 b)
    {
        var pa = p - a;
        var ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
        return (pa - ba * h).magnitude;
    }
}
