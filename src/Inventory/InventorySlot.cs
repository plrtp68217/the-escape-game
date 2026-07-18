namespace EscapeGame.Inventory;

/// <summary>
/// Один слот инвентаря: предмет и количество.
/// </summary>
public class InventorySlot
{
    public InventoryItem Item { get; private set; }
    public int Count { get; private set; }

    public bool IsEmpty => Item == null || Count == 0;

    public bool CanAccept(InventoryItem item)
    {
        return IsEmpty || Item.Id == item.Id;
    }

    public int Add(InventoryItem item, int count)
    {
        if (!CanAccept(item))
        {
            return count;
        }

        if (IsEmpty)
        {
            Item = item;
        }

        int free = Item.MaxStack - Count;
        int added = int.Min(count, free);
        Count += added;

        if (Count == 0)
        {
            Item = null;
        }

        return count - added;
    }

    public int Remove(int count)
    {
        if (IsEmpty)
        {
            return 0;
        }

        int removed = int.Min(count, Count);
        Count -= removed;

        if (Count == 0)
        {
            Item = null;
        }

        return removed;
    }

    public void Set(InventoryItem item, int count)
    {
        Item = item;
        Count = count;

        if (Count <= 0)
        {
            Clear();
        }
    }

    public void Clear()
    {
        Item = null;
        Count = 0;
    }
}
