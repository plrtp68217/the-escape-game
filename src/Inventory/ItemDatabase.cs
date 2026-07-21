using System.Collections.Generic;
using Godot;
using EscapeGame.Logging;

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

        // Инструменты для преодоления барьеров (Веха 7). Многоразовые, кроме
        // взрывчатки (расходуется при подрыве завала).
        RegisterItem("screwdriver", "Отвёртка", "res://scenes/items/screwdriver.tscn", 1);
        RegisterItem("crowbar", "Лом", "res://scenes/items/crowbar.tscn", 1);
        RegisterItem("cutters", "Кусачки", "res://scenes/items/cutters.tscn", 1);
        RegisterItem("keycard", "Ключ-карта", "res://scenes/items/keycard.tscn", 1);
        RegisterItem("cipher", "Шифр", "res://scenes/items/cipher.tscn", 1);
        RegisterItem("shovel", "Лопата", "res://scenes/items/shovel.tscn", 1);
        RegisterItem("explosive", "Взрывчатка", "res://scenes/items/explosive.tscn", 3);
    }

    private static void RegisterItem(string id, string name, string scenePath, int maxStack)
    {
        var scene = GD.Load<PackedScene>(scenePath);
        if (scene == null)
        {
            Log.Error(Log.Cat.Inventory, $"ItemDatabase: не удалось загрузить сцену предмета {scenePath}");
        }

        Texture2D icon = ItemIconRenderer.CreatePlaceholder(id);
        Register(new InventoryItem(id, name, icon, scene, maxStack: maxStack));
    }
}
