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

    // Клиент просит сервер прислать актуальный инвентарь. Нужно на старте: сервер
    // наполняет инвентарь в _Ready и рассылает его раньше, чем у клиента появляется
    // реплика игрока, поэтому первая рассылка теряется. Клиент дозапрашивает её сам.
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestInventorySync(long playerId)
    {
        if (!Multiplayer.IsServer())
        {
            return;
        }

        PlayerController player = FindPlayerController(playerId);
        if (player != null)
        {
            BroadcastInventory(player);
        }
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

    // Путь к контейнеру выброшенных предметов в сцене (одинаков на всех пирах).
    private const string DroppedItemsPath = "/root/Main/DroppedItems";

    // Монотонный счётчик имён выброшенных предметов (только на сервере) — чтобы
    // имена узлов совпадали на всех пирах и работал путь для RemoveWorldItem.
    private int _dropCounter;

    // Клиент просит сервер выбросить содержимое слота. Позицию считает клиент
    // (он знает, куда смотрит игрок), сервер её валидирует по своей позиции.
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestDrop(long playerId, int slotIndex, Vector3 position)
    {
        if (!Multiplayer.IsServer())
        {
            return;
        }

        PlayerController player = FindPlayerController(playerId);
        if (player == null || slotIndex < 0 || slotIndex >= player.Inventory.Slots.Count)
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

        string itemId = slot.Item.Id;
        int count = slot.Count;
        slot.Clear();
        BroadcastInventory(player);

        string nodeName = $"Drop_{_dropCounter++}";
        Rpc(nameof(SpawnWorldItem), itemId, count, position, nodeName);
    }

    // Сервер: создать выброшенный предмет в мире у всех пиров (ручная репликация,
    // как и удаление через RemoveWorldItem — так пути узлов совпадают везде).
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

        // Крохотные модели (пилюля) увеличиваем до видимого размера, иначе на полу
        // их не разглядеть. Масштабируется и триггер подбора — это ок.
        ModelBounds.EnsureMinVisibleSize(item, G.DropMinVisibleSize);
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
