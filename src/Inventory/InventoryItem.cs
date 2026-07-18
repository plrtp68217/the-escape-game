using Godot;

namespace EscapeGame.Inventory;

/// <summary>
/// Данные одного типа предмета.
/// </summary>
public class InventoryItem
{
    public string Id { get; }
    public string Name { get; }
    public Texture2D Icon { get; }
    public PackedScene WorldModel { get; }
    public int MaxStack { get; }

    public InventoryItem(
        string id,
        string name,
        Texture2D icon = null,
        PackedScene worldModel = null,
        int maxStack = 64)
    {
        Id = id;
        Name = name;
        Icon = icon;
        WorldModel = worldModel;
        MaxStack = maxStack > 0 ? maxStack : 1;
    }
}
