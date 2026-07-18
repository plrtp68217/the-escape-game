using Godot;

namespace EscapeGame.Inventory;

/// <summary>
/// Создаёт простые placeholder-иконки для предметов, пока нет настоящих текстур.
/// </summary>
public static class ItemIconRenderer
{
    private static readonly Color AxeColor = new(0.6f, 0.6f, 0.6f);
    private static readonly Color HealthColor = new(0.9f, 0.2f, 0.2f);
    private static readonly Color PillColor = new(0.2f, 0.7f, 0.3f);
    private static readonly Color PillsColor = new(0.3f, 0.5f, 0.9f);
    private static readonly Color SyringeColor = new(0.8f, 0.7f, 0.2f);

    public static Texture2D CreatePlaceholder(string id)
    {
        var image = Image.CreateEmpty(64, 64, false, Image.Format.Rgba8);

        Color color = id.ToLowerInvariant() switch
        {
            "axe" => AxeColor,
            "health" => HealthColor,
            "pill" => PillColor,
            "pills" => PillsColor,
            "syringe" => SyringeColor,
            _ => Colors.Gray
        };

        image.Fill(color);
        image.FillRect(new Rect2I(4, 4, 56, 56), color.Lightened(0.2f));

        var texture = new ImageTexture();
        texture.SetImage(image);
        return texture;
    }
}
