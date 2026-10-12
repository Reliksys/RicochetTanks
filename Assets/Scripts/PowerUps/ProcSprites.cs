using System.Collections.Generic;
using UnityEngine;

// Картинки и звук бонусов, нарисованные кодом (чтобы не зависеть от художника и не настраивать импорт).
public static class ProcSprites
{
    const int Size = 64;
    const float PixelsPerUnit = 80f; // значок 64 px = 0.8 клетки

    static readonly Dictionary<PowerUpType, Sprite> badges = new Dictionary<PowerUpType, Sprite>();
    static Sprite ring;
    static AudioClip pickup;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        badges.Clear();
        ring = null;
        pickup = null;
    }

    // Круглый значок: тёмная обводка, цветной круг, белый символ
    public static Sprite Badge(PowerUpType type)
    {
        if (badges.TryGetValue(type, out Sprite cached) && cached != null) return cached;

        Color main = PowerUp.ColorOf(type);
        Color dark = new Color(0.07f, 0.08f, 0.06f, 1f);
        Color light = Color.Lerp(main, Color.white, 0.35f);
        var pixels = new Color[Size * Size];

        for (int py = 0; py < Size; py++)
            for (int px = 0; px < Size; px++)
            {
                // координаты -1..1 (центр пикселя)
                float x = (px + 0.5f) / Size * 2f - 1f;
                float y = (py + 0.5f) / Size * 2f - 1f;
                float r = Mathf.Sqrt(x * x + y * y);
                Color c = Color.clear;
                if (r <= 0.98f) c = dark;
                if (r <= 0.86f) c = Color.Lerp(main, light, Mathf.Clamp01(y * 0.5f + 0.5f) * 0.6f);
                if (r <= 0.86f && Symbol(type, x, y)) c = Color.white;
                pixels[py * Size + px] = c;
            }

        Sprite s = MakeSprite(pixels, "PowerUp_" + type);
        badges[type] = s;
        return s;
    }

    static bool Symbol(PowerUpType type, float x, float y)
    {
        switch (type)
        {
            case PowerUpType.Repair: // плюс
                return (Mathf.Abs(x) < 0.13f && Mathf.Abs(y) < 0.48f) || (Mathf.Abs(y) < 0.13f && Mathf.Abs(x) < 0.48f);

            case PowerUpType.Shield: // геральдический щит
            {
                if (y > 0.42f || y < -0.55f) return false;
                float half = y > -0.05f ? 0.40f : 0.40f * (y + 0.55f) / 0.5f;
                bool outer = Mathf.Abs(x) < half;
                float innerHalf = y > -0.05f ? 0.26f : 0.26f * (y + 0.38f) / 0.33f;
                bool inner = y < 0.28f && y > -0.38f && Mathf.Abs(x) < innerHalf;
                return outer && !inner; // контур щита
            }

            case PowerUpType.Speed: // две «галочки» вправо »
                return Chevron(x + 0.2f, y) || Chevron(x - 0.22f, y);

            default: // три снаряда
                for (int i = -1; i <= 1; i++)
                {
                    float cx = i * 0.3f;
                    if (Mathf.Abs(x - cx) < 0.09f && y > -0.45f && y < 0.18f) return true;
                    float dx = x - cx, dy = y - 0.18f;
                    if (dy >= 0f && dx * dx + dy * dy < 0.09f * 0.09f * 2.2f) return true;
                }
                return false;
        }
    }

    static bool Chevron(float x, float y) => Mathf.Abs(y) < 0.42f && Mathf.Abs(x + 0.75f * Mathf.Abs(y)) < 0.12f;

    // Кольцо щита вокруг танка (диаметр ~1.3 клетки)
    public static Sprite Ring()
    {
        if (ring != null) return ring;
        const int size = 128;
        var pixels = new Color[size * size];
        for (int py = 0; py < size; py++)
            for (int px = 0; px < size; px++)
            {
                float x = (px + 0.5f) / size * 2f - 1f;
                float y = (py + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(x * x + y * y);
                float edge = Mathf.Clamp01(1f - Mathf.Abs(r - 0.9f) / 0.08f);       // яркий край
                float fill = r < 0.9f ? 0.18f * r : 0f;                             // лёгкая заливка
                pixels[py * size + px] = new Color(1f, 1f, 1f, Mathf.Max(edge, fill));
            }
        ring = MakeSprite(pixels, "ShieldRing", size, 100f);
        return ring;
    }

    static Sprite MakeSprite(Color[] pixels, string name, int size = Size, float ppu = PixelsPerUnit)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = name,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        tex.SetPixels(pixels);
        tex.Apply();
        Sprite s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), ppu);
        s.name = name;
        return s;
    }

    // Короткий восходящий «дзынь» из трёх нот
    public static AudioClip PickupSound()
    {
        if (pickup != null) return pickup;
        const int rate = 44100;
        float[] notes = { 660f, 880f, 1320f };
        const float noteLen = 0.07f;
        int total = Mathf.CeilToInt(rate * noteLen * notes.Length + rate * 0.12f);
        var data = new float[total];
        for (int i = 0; i < total; i++)
        {
            float t = i / (float)rate;
            int n = Mathf.Min(notes.Length - 1, (int)(t / noteLen));
            float local = t - n * noteLen;
            float env = Mathf.Exp(-local * (n == notes.Length - 1 ? 14f : 30f));
            float phase = t * notes[n];
            float wave = Mathf.Sin(phase * Mathf.PI * 2f) * 0.7f + (Mathf.Repeat(phase, 1f) < 0.5f ? 0.15f : -0.15f);
            data[i] = wave * env * 0.5f;
        }
        pickup = AudioClip.Create("PowerUpPickup", total, 1, rate, false);
        pickup.SetData(data, 0);
        return pickup;
    }
}
