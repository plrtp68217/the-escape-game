using System;
using Godot;

namespace EscapeGame.Inventory;

/// <summary>
/// Рисует простые, но узнаваемые placeholder-иконки предметов, пока нет
/// настоящих текстур. У каждого предмета — свой силуэт (крест аптечки, капсула
/// таблетки, шприц, топор, ключ), а не просто цветной квадрат.
/// </summary>
public static class ItemIconRenderer
{
    private const int Size = 64;

    private static readonly Color Background = new(0.12f, 0.12f, 0.15f);

    public static Texture2D CreatePlaceholder(string id)
    {
        var image = Image.CreateEmpty(Size, Size, false, Image.Format.Rgba8);
        image.Fill(Background);

        string key = id.ToLowerInvariant();
        DrawBorder(image, AccentFor(key));

        switch (key)
        {
            case "health":
                DrawHealth(image);
                break;
            case "pill":
                DrawPill(image);
                break;
            case "syringe":
                DrawSyringe(image);
                break;
            case "axe":
                DrawAxe(image);
                break;
            case "key":
                DrawKey(image);
                break;
            default:
                FillRect(image, 16, 16, 32, 32, AccentFor(key));
                break;
        }

        var texture = new ImageTexture();
        texture.SetImage(image);
        return texture;
    }

    private static Color AccentFor(string key) => key switch
    {
        "axe" => new Color(0.7f, 0.7f, 0.75f),
        "health" => new Color(0.9f, 0.25f, 0.25f),
        "pill" => new Color(0.35f, 0.8f, 0.45f),
        "syringe" => new Color(0.55f, 0.8f, 0.95f),
        "key" => new Color(0.92f, 0.78f, 0.2f),
        _ => Colors.Gray
    };

    // Аптечка: красный планшет с белым крестом.
    private static void DrawHealth(Image image)
    {
        FillRect(image, 12, 12, 40, 40, new Color(0.85f, 0.2f, 0.2f));
        FillRect(image, 28, 18, 8, 28, Colors.White); // вертикаль креста
        FillRect(image, 18, 28, 28, 8, Colors.White);  // горизонталь креста
    }

    // Таблетка: горизонтальная капсула, левая половина зелёная, правая белая.
    private static void DrawPill(Image image)
    {
        var left = new Color(0.3f, 0.75f, 0.4f);
        var right = new Color(0.92f, 0.92f, 0.92f);
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                if (InCapsule(x, y))
                {
                    image.SetPixel(x, y, x < 32 ? left : right);
                }
            }
        }
    }

    // Шприц: диагональный корпус, поршень и игла.
    private static void DrawSyringe(Image image)
    {
        var body = new Color(0.8f, 0.9f, 0.98f);
        var metal = new Color(0.75f, 0.78f, 0.82f);
        DrawLine(image, new Vector2(16, 48), new Vector2(44, 20), 10f, body);
        DrawLine(image, new Vector2(44, 20), new Vector2(54, 10), 3f, metal);   // игла
        DrawLine(image, new Vector2(12, 52), new Vector2(22, 42), 6f, metal);   // поршень
    }

    // Топор: деревянная рукоять по диагонали и стальное лезвие сверху.
    private static void DrawAxe(Image image)
    {
        DrawLine(image, new Vector2(20, 52), new Vector2(42, 16), 6f, new Color(0.5f, 0.35f, 0.2f));
        FillRect(image, 34, 10, 18, 16, new Color(0.72f, 0.72f, 0.78f));
        FillRect(image, 32, 14, 4, 8, new Color(0.72f, 0.72f, 0.78f));
    }

    // Ключ: золотое кольцо, стержень и зубцы.
    private static void DrawKey(Image image)
    {
        var gold = new Color(0.92f, 0.78f, 0.2f);
        FillCircle(image, new Vector2(22, 22), 11f, gold);
        FillCircle(image, new Vector2(22, 22), 5f, Background); // отверстие
        DrawLine(image, new Vector2(28, 28), new Vector2(50, 50), 5f, gold);
        FillRect(image, 44, 44, 4, 10, gold); // зубец
        FillRect(image, 38, 38, 4, 8, gold);  // зубец
    }

    // ── Примитивы рисования по пикселям ───────────────────────────────

    private static bool InCapsule(int x, int y)
    {
        // Капсула = прямоугольник со скруглениями (круги на концах).
        const int top = 25, height = 14, left = 14, right = 50;
        if (y < top || y >= top + height)
        {
            return false;
        }

        if (x >= left && x <= right)
        {
            return true;
        }

        float cy = top + height / 2f;
        var end = x < left ? new Vector2(left, cy) : new Vector2(right, cy);
        return new Vector2(x, y).DistanceTo(end) <= height / 2f;
    }

    private static void DrawBorder(Image image, Color color)
    {
        FillRect(image, 0, 0, Size, 3, color);
        FillRect(image, 0, Size - 3, Size, 3, color);
        FillRect(image, 0, 0, 3, Size, color);
        FillRect(image, Size - 3, 0, 3, Size, color);
    }

    private static void FillRect(Image image, int x, int y, int w, int h, Color color)
    {
        for (int py = y; py < y + h; py++)
        {
            for (int px = x; px < x + w; px++)
            {
                SetPixelSafe(image, px, py, color);
            }
        }
    }

    private static void FillCircle(Image image, Vector2 center, float radius, Color color)
    {
        for (int py = 0; py < Size; py++)
        {
            for (int px = 0; px < Size; px++)
            {
                if (new Vector2(px, py).DistanceTo(center) <= radius)
                {
                    SetPixelSafe(image, px, py, color);
                }
            }
        }
    }

    private static void DrawLine(Image image, Vector2 a, Vector2 b, float thickness, Color color)
    {
        float half = thickness / 2f;
        for (int py = 0; py < Size; py++)
        {
            for (int px = 0; px < Size; px++)
            {
                if (DistanceToSegment(new Vector2(px, py), a, b) <= half)
                {
                    SetPixelSafe(image, px, py, color);
                }
            }
        }
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lengthSq = ab.LengthSquared();
        if (lengthSq <= 0.0001f)
        {
            return p.DistanceTo(a);
        }

        float t = Mathf.Clamp((p - a).Dot(ab) / lengthSq, 0f, 1f);
        return p.DistanceTo(a + ab * t);
    }

    private static void SetPixelSafe(Image image, int x, int y, Color color)
    {
        if (x >= 0 && y >= 0 && x < Size && y < Size)
        {
            image.SetPixel(x, y, color);
        }
    }
}
