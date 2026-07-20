using System.Collections.Generic;

namespace EscapeGame.Inventory;

/// <summary>
/// Инвентарь игрока: сетка слотов с поддержкой стаков.
/// </summary>
public class PlayerInventory
{
    public int Width { get; set; } = 4;
    public int Height { get; set; } = 4;

    private readonly List<InventorySlot> _slots;

    public IReadOnlyList<InventorySlot> Slots => _slots;

    public PlayerInventory(int width = 4, int height = 4)
    {
        Width = width;
        Height = height;

        int capacity = Width * Height;
        _slots = new List<InventorySlot>(capacity);

        for (int i = 0; i < capacity; i++)
        {
            _slots.Add(new InventorySlot());
        }
    }

    // Полностью очищает инвентарь и снимает экипировку. Используется при
    // перезапуске раунда, чтобы вернуть стартовый набор.
    public void Clear()
    {
        foreach (InventorySlot slot in _slots)
        {
            slot.Clear();
        }
        EquippedSlotIndex = -1;
    }

    public int AddItem(InventoryItem item, int count)
    {
        if (item == null)
        {
            return count;
        }

        int remaining = count;

        foreach (InventorySlot slot in _slots)
        {
            if (slot.Item != null && slot.Item.Id == item.Id && slot.Count < item.MaxStack)
            {
                remaining = slot.Add(item, remaining);
                if (remaining == 0)
                {
                    return 0;
                }
            }
        }

        foreach (InventorySlot slot in _slots)
        {
            if (slot.IsEmpty)
            {
                remaining = slot.Add(item, remaining);
                if (remaining == 0)
                {
                    return 0;
                }
            }
        }

        return remaining;
    }

    // Есть ли в инвентаре хотя бы один предмет с таким id.
    public bool Has(string itemId)
    {
        foreach (InventorySlot slot in _slots)
        {
            if (slot.Item?.Id == itemId && slot.Count > 0)
            {
                return true;
            }
        }
        return false;
    }

    // Убирает одну единицу предмета. Возвращает true, если предмет нашёлся.
    public bool RemoveOne(string itemId)
    {
        foreach (InventorySlot slot in _slots)
        {
            if (slot.Item?.Id == itemId && slot.Count > 0)
            {
                slot.Remove(1);
                return true;
            }
        }
        return false;
    }

    public bool TryEquip(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Count || _slots[slotIndex].IsEmpty)
        {
            return false;
        }

        EquippedSlotIndex = slotIndex;
        return true;
    }

    public int EquippedSlotIndex { get; private set; } = -1;

    public InventorySlot EquippedSlot => EquippedSlotIndex >= 0 ? _slots[EquippedSlotIndex] : null;
}
