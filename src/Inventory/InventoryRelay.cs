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

    // Клиент просит сервер переместить/поменять местами два слота (drag-and-drop).
    // Как и экипировка — сервер авторитетно меняет инвентарь и рассылает результат.
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestMoveSlot(long playerId, int fromIndex, int toIndex)
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

        player.Inventory.MoveSlot(fromIndex, toIndex);
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
    // имена узлов совпадали на всех пирах и работал путь для SetWorldItemActive.
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
    // как и показ/скрытие через SetWorldItemActive — так пути узлов совпадают везде).
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

        // Приводим модель к видимому размеру: крохотные (пилюля) увеличиваем,
        // гигантские (нативный масштаб GLB) уменьшаем. Масштабируется и триггер
        // подбора — это ок.
        ModelBounds.ClampVisibleSize(item, G.DropMinVisibleSize, G.DropMaxVisibleSize);
    }

    // Сервер сообщает всем показать/спрятать предмет в мире (подбор/рематч). Узел
    // не удаляем — так его можно вернуть новым раундом (см. WorldItem.ResetForRound).
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SetWorldItemActive(string itemPath, bool active)
    {
        WorldItem item = GetNodeOrNull<WorldItem>(itemPath);
        item?.SetActive(active);
    }

    // Сервер: убрать все выброшенные предметы у всех пиров. Вызывается при
    // рематче (Веха 10), иначе брошенные за раунд предметы копятся на карте.
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
        if (!Multiplayer.IsServer())
        {
            return;
        }

        Rpc(nameof(ClearWorldItems));
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
