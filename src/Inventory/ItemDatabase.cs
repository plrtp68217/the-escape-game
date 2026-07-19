using System.Collections.Generic;
using Godot;

namespace EscapeGame.Inventory;

/// <summary>
/// Статический реестр всех предметов в игре.
/// </summary>
public static class ItemDatabase
{
    private static readonly Dictionary<string, InventoryItem> _items = new();

    public static void Register(InventoryItem item)
    {
        _items[item.Id] = item;
    }

    public static InventoryItem Get(string id)
    {
        return _items.TryGetValue(id, out InventoryItem item) ? item : null;
    }

    public static void RegisterDefaults()
    {
        if (_items.Count > 0)
        {
            return;
        }

        RegisterItem("axe", "Топор", "res://scenes/items/axe.tscn", 1);
        RegisterItem("health", "Аптечка", "res://scenes/items/health.tscn", 5);
        RegisterItem("pill", "Таблетка", "res://scenes/items/pill.tscn", 10);
        RegisterItem("syringe", "Шприц", "res://scenes/items/syringe.tscn", 5);
        RegisterItem("key", "Ключ", "res://scenes/items/key.tscn", 5);
    }

    private static void RegisterItem(string id, string name, string scenePath, int maxStack)
    {
        var scene = GD.Load<PackedScene>(scenePath);
        if (scene == null)
        {
            GD.PrintErr($"ItemDatabase: не удалось загрузить сцену предмета {scenePath}");
        }

        Texture2D icon = ItemIconRenderer.CreatePlaceholder(id);
        Register(new InventoryItem(id, name, icon, scene, maxStack: maxStack));
    }
}
