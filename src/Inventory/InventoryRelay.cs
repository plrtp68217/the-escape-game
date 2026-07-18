using System.Linq;
using Godot;
using EscapeGame;

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

    // Клиент просит сервер подобрать предмет. CallLocal = true, чтобы хост
    // тоже мог вызывать метод локально при самоотправке.
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestPickup(long playerId, string itemPath)
    {
        if (!Multiplayer.IsServer())
        {
            return;
        }

        PlayerController player = FindPlayerController(playerId);
        if (player == null)
        {
            return;
        }

        WorldItem item = GetNodeOrNull<WorldItem>(itemPath);
        if (item == null || !item.IsInsideTree())
        {
            return;
        }

        InventoryItem data = ItemDatabase.Get(item.ItemId);
        if (data == null)
        {
            return;
        }

        int remaining = player.Inventory.AddItem(data, item.Count);
        if (remaining == item.Count)
        {
            return;
        }

        item.Count = remaining;
        if (item.Count <= 0)
        {
            Rpc(nameof(RemoveWorldItem), itemPath);
        }

        Rpc(nameof(SyncInventory), playerId, PackIds(player.Inventory), PackCounts(player.Inventory), player.Inventory.EquippedSlotIndex);
    }

    // Клиент просит сервер экипировать слот.
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestEquip(long playerId, int slotIndex)
    {
        if (!Multiplayer.IsServer())
        {
            return;
        }

        PlayerController player = FindPlayerController(playerId);
        if (player == null)
        {
            return;
        }

        player.Inventory.TryEquip(slotIndex);
        Rpc(nameof(SyncInventory), playerId, PackIds(player.Inventory), PackCounts(player.Inventory), player.Inventory.EquippedSlotIndex);
    }

    // Сервер рассылает актуальное состояние инвентаря игрока.
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SyncInventory(long playerId, string[] itemIds, int[] counts, int equippedIndex)
    {
        PlayerController player = FindPlayerController(playerId);
        if (player == null)
        {
            return;
        }

        player.UpdateInventory(itemIds, counts, equippedIndex);
    }

    // Сервер сообщает всем, что предмет в мире нужно удалить.
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RemoveWorldItem(string itemPath)
    {
        WorldItem item = GetNodeOrNull<WorldItem>(itemPath);
        item?.QueueFree();
    }

    private static PlayerController FindPlayerController(long playerId)
    {
        return PlayerController.AllPlayers.Values.FirstOrDefault(p => p.PlayerId == playerId);
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
