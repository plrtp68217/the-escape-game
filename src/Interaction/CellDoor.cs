using Godot;

namespace EscapeGame.Interaction;

/// <summary>
/// Дверь камеры. Заперта по умолчанию. Заключённый открывает её ключом или
/// выбивает топором за несколько ударов; надзиратель может запирать и закрывать.
/// Состояние — авторитет сервера, рассылается всем через SyncState.
/// </summary>
public partial class CellDoor : StaticBody3D, IInteractable
{
    [Export]
    public bool Locked { get; set; } = true;

    private bool _isOpen;
    private int _health = G.Door.Health;

    private CollisionShape3D _collision;
    private Node3D _visual;
    private Area3D _zone;

    public override void _Ready()
    {
        _collision = GetNode<CollisionShape3D>("CollisionShape3D");
        _visual = GetNode<Node3D>("MeshInstance3D");
        _zone = GetNode<Area3D>("InteractZone");

        _zone.BodyEntered += OnBodyEntered;
        _zone.BodyExited += OnBodyExited;

        ApplyVisual();
    }

    private void OnBodyEntered(Node3D body)
    {
        if (body is PlayerController player && player.IsMultiplayerAuthority())
        {
            player.RegisterInteractable(this);
        }
    }

    private void OnBodyExited(Node3D body)
    {
        if (body is PlayerController player && player.IsMultiplayerAuthority())
        {
            player.UnregisterInteractable(this);
        }
    }

    public string GetPrompt(PlayerController player)
    {
        if (player.Role == PlayerRole.Warden)
        {
            if (_isOpen)
            {
                return "Закрыть дверь";
            }
            return Locked ? "Отпереть дверь" : "Запереть дверь";
        }

        if (_isOpen)
        {
            return string.Empty;
        }
        if (!Locked)
        {
            return "Открыть";
        }
        if (player.Inventory.Has(G.Door.KeyItemId))
        {
            return "Открыть ключом";
        }
        if (IsAxeEquipped(player))
        {
            return "Выбить топором";
        }
        return "Заперто — нужен ключ или топор";
    }

    public bool CanInteract(PlayerController player) => true;

    // Только сервер (вызывается из InteractionRelay).
    public void Interact(PlayerController player)
    {
        if (player.Role == PlayerRole.Warden)
        {
            WardenAction();
            return;
        }

        PrisonerAction(player);
    }

    private void WardenAction()
    {
        if (_isOpen)
        {
            // Закрыть и снова запереть (заодно "чиним" выбитую дверь).
            _health = G.Door.Health;
            SetState(isOpen: false, locked: true);
            return;
        }

        // Переключаем замок закрытой двери.
        SetState(isOpen: false, locked: !Locked);
    }

    private void PrisonerAction(PlayerController player)
    {
        if (_isOpen)
        {
            return;
        }

        if (!Locked)
        {
            SetState(isOpen: true, locked: false);
            return;
        }

        // Заперта: сначала пробуем ключ (расходуется).
        if (player.Inventory.RemoveOne(G.Door.KeyItemId))
        {
            Inventory.InventoryRelay.Instance?.BroadcastInventory(player);
            SetState(isOpen: true, locked: false);
            return;
        }

        // Иначе выбиваем топором. Промежуточные удары считаем на сервере,
        // рассылаем состояние только когда дверь реально распахнулась.
        if (IsAxeEquipped(player))
        {
            _health--;
            if (_health <= 0)
            {
                SetState(isOpen: true, locked: false);
            }
        }
    }

    private static bool IsAxeEquipped(PlayerController player)
    {
        return player.Inventory.EquippedSlot?.Item?.Id == G.Door.AxeItemId;
    }

    private void SetState(bool isOpen, bool locked)
    {
        Rpc(nameof(SyncState), isOpen, locked);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SyncState(bool isOpen, bool locked)
    {
        _isOpen = isOpen;
        Locked = locked;
        ApplyVisual();
    }

    private void ApplyVisual()
    {
        _collision.Disabled = _isOpen;
        _visual.Visible = !_isOpen;
    }
}
