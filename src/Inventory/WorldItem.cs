using Godot;
using EscapeGame.Interaction;

namespace EscapeGame.Inventory;

/// <summary>
/// Предмет, лежащий в мире. Реализует IInteractable: подбор — это его
/// взаимодействие. Регистрирует себя у ближайшего игрока по входу в область.
/// </summary>
public partial class WorldItem : Area3D, IInteractable
{
    [Export]
    public string ItemId { get; set; } = string.Empty;

    [Export]
    public int Count { get; set; } = 1;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    private void OnBodyEntered(Node3D body)
    {
        if (Multiplayer.MultiplayerPeer != null
            && body is PlayerController player && player.IsMultiplayerAuthority())
        {
            player.RegisterInteractable(this);
        }
    }

    private void OnBodyExited(Node3D body)
    {
        if (Multiplayer.MultiplayerPeer != null
            && body is PlayerController player && player.IsMultiplayerAuthority())
        {
            player.UnregisterInteractable(this);
        }
    }

    public string GetPrompt(PlayerController player) => "Подобрать";

    public bool CanInteract(PlayerController player) => true;

    // Только сервер (вызывается из InteractionRelay). Кладёт предмет в инвентарь,
    // рассылает новое состояние и удаляет предмет из мира, если он закончился.
    public void Interact(PlayerController player)
    {
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
            InventoryRelay.Instance?.Rpc(nameof(InventoryRelay.RemoveWorldItem), GetPath().ToString());
        }
    }
}
