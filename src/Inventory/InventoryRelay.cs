using System;
using System.Linq;
using Godot;
using EscapeGame.Player;
using EscapeGame.Services;

namespace EscapeGame.Inventory;

/// <summary>
/// Серверный посредник для синхронизации инвентаря между игроками.
/// Добавляется как autoload и имеет authority сервера.
/// </summary>
public partial class InventoryRelay : Node
{
    public static InventoryRelay Instance { get; private set; }

    public override void _Ready()
    {
        Instance = this;
    }

    private bool ValidateSender(long playerId)
    {
        long senderId = Multiplayer.GetRemoteSenderId();
        if (senderId == 0 || playerId == senderId)
        {
            return true;
        }

        GD.PushWarning($"[InventoryRelay] PlayerId mismatch: expected {senderId}, got {playerId}");
        return false;
    }

    // Клиент просит сервер прислать актуальный инвентарь. Нужно на старте: сервер
    // наполняет инвентарь в _Ready и рассылает его раньше, чем у клиента появляется
    // реплика игрока, поэтому первая рассылка теряется. Клиент дозапрашивает её сам.
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestInventorySync(long playerId)
    {
        if (!ServiceLocator.Network?.IsServer ?? false || !ValidateSender(playerId))
        {
            return;
        }

        PlayerController player = ServiceLocator.Players.Get(playerId);
        if (player != null)
        {
            BroadcastInventory(player);
        }
    }

    // Клиент просит сервер экипировать предмет по его идентификатору.
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestEquip(long playerId, string itemId)
    {
        if (!ServiceLocator.Network?.IsServer ?? false || !ValidateSender(playerId))
        {
            return;
        }

        PlayerController player = ServiceLocator.Players.Get(playerId);
        if (player == null)
        {
            return;
        }

        int slotIndex = FindSlotIndex(player.Inventory, itemId);
        if (slotIndex >= 0)
        {
            player.Inventory.TryEquip(slotIndex);
        }

        BroadcastInventory(player);
    }

    // Клиент просит сервер переместить/поменять местами два слота (drag-and-drop).
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestMoveSlot(long playerId, int fromIndex, int toIndex)
    {
        if (!ServiceLocator.Network?.IsServer ?? false || !ValidateSender(playerId))
        {
            return;
        }

        PlayerController player = ServiceLocator.Players.Get(playerId);
        if (player == null)
        {
            return;
        }

        player.Inventory.MoveSlot(fromIndex, toIndex);
        BroadcastInventory(player);
    }

    // Клиент просит сервер выбросить предмет по его идентификатору.
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestDrop(long playerId, string itemId, Vector3 position)
    {
        if (!ServiceLocator.Network?.IsServer ?? false || !ValidateSender(playerId))
        {
            return;
        }

        PlayerController player = ServiceLocator.Players.Get(playerId);
        if (player == null)
        {
            return;
        }

        int slotIndex = FindSlotIndex(player.Inventory, itemId);
        if (slotIndex < 0)
        {
            return;
        }

        InventorySlot slot = player.Inventory.Slots[slotIndex];
        if (slot.IsEmpty)
        {
            return;
        }

        // Анти-чит: не даём бросать слишком далеко от игрока.
        if (player.GlobalPosition.DistanceTo(position) > 3f)
        {
            position = player.GlobalPosition + Vector3.Up * 0.3f;
        }

        string droppedItemId = slot.Item.Id;
        int count = slot.Count;
        slot.Clear();
        BroadcastInventory(player);

        string nodeName = $"Drop_{_dropCounter++}";
        Rpc(nameof(SpawnWorldItem), droppedItemId, count, position, nodeName);
    }

    private static int FindSlotIndex(PlayerInventory inventory, string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            return -1;
        }

        for (int i = 0; i < inventory.Slots.Count; i++)
        {
            if (!inventory.Slots[i].IsEmpty && inventory.Slots[i].Item.Id == itemId)
            {
                return i;
            }
        }

        return -1;
    }

    // Сервер рассылает всем актуальное состояние инвентаря игрока.
    public void BroadcastInventory(PlayerController player)
    {
        if (!ServiceLocator.Network?.IsServer ?? false)
        {
            return;
        }

        Rpc(nameof(SyncInventory), (long)player.PlayerId,
            PackIds(player.Inventory), PackCounts(player.Inventory), player.Inventory.EquippedSlotIndex);
    }

    // Сервер рассылает актуальное состояние инвентаря игрока.
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SyncInventory(long playerId, string[] itemIds, int[] counts, int equippedIndex)
    {
        PlayerController player = ServiceLocator.Players.Get(playerId);
        if (player == null)
        {
            return;
        }

        player.UpdateInventory(itemIds, counts, equippedIndex);
    }

    // Путь к контейнеру выброшенных предметов в сцене (одинаков на всех пирах).
    private const string DroppedItemsPath = "/root/Main/DroppedItems";

    // Монотонный счётчик имён выброшенных предметов (только на сервере).
    private int _dropCounter;

    // Сервер: создать выброшенный предмет в мире у всех пиров.
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SpawnWorldItem(string itemId, int count, Vector3 position, string nodeName)
    {
        var container = GetNodeOrNull<Node3D>(DroppedItemsPath);
        if (container == null)
        {
            return;
        }

        InventoryItem data = ItemDatabase.Get(itemId);
        if (data?.WorldModel == null)
        {
            return;
        }

        var item = data.WorldModel.Instantiate<WorldItem>();
        item.Name = nodeName;
        item.ItemId = itemId;
        item.Count = count;
        container.AddChild(item);
        item.GlobalPosition = position;

        ModelBounds.ClampVisibleSize(item, G.DropMinVisibleSize, G.DropMaxVisibleSize);
    }

    // Сервер сообщает всем показать/спрятать предмет в мире (подбор/рематч).
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SetWorldItemActive(string itemPath, bool active)
    {
        WorldItem item = GetNodeOrNull<WorldItem>(itemPath);
        item?.SetActive(active);
    }

    // Сервер: убрать все выброшенные предметы у всех пиров.
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ClearWorldItems()
    {
        var container = GetNodeOrNull<Node3D>(DroppedItemsPath);
        if (container == null)
        {
            return;
        }

        foreach (Node child in container.GetChildren())
        {
            child.QueueFree();
        }
    }

    // Сервер инициирует очистку выброшенных предметов на всех пирах.
    public void BroadcastClearWorldItems()
    {
        if (!ServiceLocator.Network?.IsServer ?? false)
        {
            return;
        }

        Rpc(nameof(ClearWorldItems));
    }

    private static string[] PackIds(PlayerInventory inventory)
    {
        return inventory.Slots.Select(s => s.Item?.Id ?? string.Empty).ToArray();
    }

    private static int[] PackCounts(PlayerInventory inventory)
    {
        return inventory.Slots.Select(s => s.Count).ToArray();
    }
}
