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
        BroadcastInventory(player);
    }

    // Сервер рассылает всем актуальное состояние инвентаря игрока. Общий путь
    // для подбора, экипировки и любых других серверных изменений инвентаря.
    public void BroadcastInventory(PlayerController player)
    {
        if (!Multiplayer.IsServer())
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
