using System.Collections.Generic;
using Godot;
using EscapeGame.Interaction;
using EscapeGame.Player;
using EscapeGame.Services;

namespace EscapeGame.Inventory;

/// <summary>
/// Предмет, лежащий в мире. Реализует IInteractable: подбор — это его
/// взаимодействие. Регистрирует себя у ближайшего игрока по входу в область.
/// </summary>
public partial class WorldItem : Area3D, IInteractable
{
    // Все предметы в мире — по образцу CellDoor.All / Barrier.All. Нужен, чтобы
    // при рематче вернуть подобранные предметы на места (сцена не перезагружается).
    public static readonly HashSet<WorldItem> All = new();

    [Export]
    public string ItemId { get; set; } = string.Empty;

    [Export]
    public int Count { get; set; } = 1;

    // Исходное количество (для восстановления при рематче) и «активность»: подобранный
    // предмет не удаляется, а прячется и отключает подбор, чтобы его можно было
    // вернуть новым раундом без пересоздания узла (пути узлов остаются валидны).
    private int _initialCount = 1;
    private bool _active = true;

    // Доступен ли предмет для наведения/подбора (виден и не подобран). Нужен для
    // прицельного подбора: локальный игрок перебирает WorldItem.All и берёт тот,
    // на который смотрит (см. PlayerController.FindAimedItem).
    public bool IsAvailable => _active && Count > 0;

    public override void _Ready()
    {
        // В сценах предметов Count иногда сериализован как null → грузится нулём,
        // из-за чего подбор клал 0 штук и «ничего не помещалось». Нормализуем.
        if (Count <= 0)
        {
            Count = 1;
        }

        _initialCount = Count;
        All.Add(this);

        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;

        // Нормализуем видимый размер ЛЮБОГО предмета в мире (не только выброшенного):
        // разложенные в сцене пикапы идут в нативном масштабе GLB, из-за чего, напр.,
        // пилюля выходит гигантской. ClampVisibleSize идемпотентен — предметы, уже
        // попадающие в диапазон, не трогает (тот же зажим, что и при выбросе).
        ModelBounds.ClampVisibleSize(this, G.DropMinVisibleSize, G.DropMaxVisibleSize);
    }

    public override void _ExitTree()
    {
        All.Remove(this);
    }

    private void OnBodyEntered(Node3D body)
    {
        if (Multiplayer.MultiplayerPeer != null
            && body is PlayerController player && player.IsMultiplayerAuthority())
        {
            player.Interaction?.RegisterInteractable(this);
        }
    }

    private void OnBodyExited(Node3D body)
    {
        if (Multiplayer.MultiplayerPeer != null
            && body is PlayerController player && player.IsMultiplayerAuthority())
        {
            player.Interaction?.UnregisterInteractable(this);
        }
    }

    public string GetPrompt(PlayerController player)
    {
        InventoryItem data = ItemDatabase.Get(ItemId);
        string name = data?.Name ?? "предмет";
        return $"Подобрать {name} [F]";
    }

    public bool CanInteract(PlayerController player) => true;

    // Только сервер (вызывается из InteractionRelay). Кладёт предмет в инвентарь,
    // рассылает новое состояние и прячет предмет из мира, если он закончился.
    public void Interact(PlayerController player)
    {
        if (!_active)
        {
            return;
        }

        InventoryItem data = ItemDatabase.Get(ItemId);
        if (data == null)
        {
            return;
        }

        int remaining = player.Inventory.AddItem(data, Count);
        if (remaining == Count)
        {
            return; // ничего не поместилось
        }

        Count = remaining;
        InventoryRelay.Instance?.BroadcastInventory(player);

        if (Count <= 0)
        {
            // Не удаляем, а прячем — чтобы вернуть предмет при рематче (ResetForRound).
            InventoryRelay.Instance?.Rpc(nameof(InventoryRelay.SetWorldItemActive), GetPath().ToString(), false);
        }
    }

    // Показывает/прячет предмет на всех пирах (вызывается из InventoryRelay).
    // Спрятанный не виден и не ловит вход в область (нельзя подобрать).
    public void SetActive(bool active)
    {
        _active = active;
        Visible = active;
        Monitoring = active;
        Monitorable = active;

        // Коллизию тоже выключаем: intersect_ray видит шейпы независимо от monitoring,
        // и без этого луч прицела продолжал «попадать» в уже подобранный (спрятанный)
        // предмет — подсказка «Подобрать…» оставалась висеть после подбора.
        foreach (Node child in GetChildren())
        {
            if (child is CollisionShape3D shape)
            {
                shape.Disabled = !active;
            }
        }
    }

    // Только сервер: вернуть предмет к исходному состоянию при рематче — исходное
    // количество и показ на всех пирах.
    public void ResetForRound()
    {
        if (!ServiceLocator.Network?.IsServer ?? false)
        {
            return;
        }

        Count = _initialCount;
        InventoryRelay.Instance?.Rpc(nameof(InventoryRelay.SetWorldItemActive), GetPath().ToString(), true);
    }
}
