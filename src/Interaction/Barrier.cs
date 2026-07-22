using System.Collections.Generic;
using Godot;
using EscapeGame.Player;
using EscapeGame.Services;

namespace EscapeGame.Interaction;

/// <summary>
/// Тип барьера — определяет, каким инструментом он преодолевается.
/// </summary>
public enum BarrierKind
{
    Vent,      // Вентиляция — отвёртка или лом
    Grate,     // Решётка — кусачки
    CodedDoor, // Кодовый замок — шифр или ключ-карта
    Rubble,    // Завал — лопата или взрывчатка (взрывчатка расходуется)
}

/// <summary>
/// Универсальный преодолимый барьер (вентиляция/решётка/кодовый замок/завал).
/// Заперт по умолчанию; открывается заключённым при наличии нужного инструмента.
/// Состояние — авторитет сервера, рассылается через SyncState. По образцу
/// <see cref="CellDoor"/> и общий паттерн IInteractable/InteractionRelay.
/// </summary>
public partial class Barrier : StaticBody3D, IInteractable
{
    // Все барьеры в сцене — чтобы при перезапуске раунда вернуть их все в
    // закрытое состояние, не завися от путей (ср. PlayerController.AllPlayers,
    // CellDoor.All).
    public static readonly HashSet<Barrier> All = new();

    [Export]
    public BarrierKind Kind { get; set; } = BarrierKind.Vent;

    private bool _open;

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

        All.Add(this);

        TintByKind();
        ApplyVisual();
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
        if (_open)
        {
            return string.Empty;
        }

        // Барьеры преодолевают только заключённые — надзирателю подсказку не
        // показываем.
        if (player.Role != PlayerRole.Prisoner)
        {
            return string.Empty;
        }

        return HasRequiredTool(player, out _)
            ? $"{ActionText} [F]"
            : NeedText;
    }

    public bool CanInteract(PlayerController player) => true;

    // Только сервер (вызывается из InteractionRelay). Открывает барьер, если у
    // заключённого есть нужный инструмент; взрывчатка при подрыве расходуется.
    public void Interact(PlayerController player)
    {
        if (_open || player.Role != PlayerRole.Prisoner)
        {
            return;
        }

        if (!HasRequiredTool(player, out string consumeId))
        {
            return;
        }

        if (consumeId != null && player.Inventory.RemoveOne(consumeId))
        {
            Inventory.InventoryRelay.Instance?.BroadcastInventory(player);
        }

        SetOpen(true);
    }

    // Возврат барьера в закрытое состояние при перезапуске раунда. Только сервер.
    public void ResetState()
    {
        if (!ServiceLocator.Network?.IsServer ?? false)
        {
            return;
        }

        SetOpen(false);
    }

    // Есть ли у игрока инструмент, открывающий этот барьер. consumeId != null,
    // если инструмент расходуется при использовании (взрывчатка для завала).
    private bool HasRequiredTool(PlayerController player, out string consumeId)
    {
        consumeId = null;
        Inventory.PlayerInventory inv = player.Inventory;

        switch (Kind)
        {
            case BarrierKind.Vent:
                return inv.Has(G.Tools.Screwdriver) || inv.Has(G.Tools.Crowbar);
            case BarrierKind.Grate:
                return inv.Has(G.Tools.Cutters);
            case BarrierKind.CodedDoor:
                return inv.Has(G.Tools.Keycard) || inv.Has(G.Tools.Cipher);
            case BarrierKind.Rubble:
                if (inv.Has(G.Tools.Shovel))
                {
                    return true;
                }
                if (inv.Has(G.Tools.Explosive))
                {
                    consumeId = G.Tools.Explosive;
                    return true;
                }
                return false;
            default:
                return false;
        }
    }

    private string ActionText => Kind switch
    {
        BarrierKind.Vent => "Вскрыть вентиляцию",
        BarrierKind.Grate => "Перерезать решётку",
        BarrierKind.CodedDoor => "Открыть кодовый замок",
        BarrierKind.Rubble => "Разобрать завал",
        _ => "Открыть",
    };

    private string NeedText => Kind switch
    {
        BarrierKind.Vent => "Вентиляция — нужна отвёртка или лом",
        BarrierKind.Grate => "Решётка — нужны кусачки",
        BarrierKind.CodedDoor => "Кодовый замок — нужен шифр или ключ-карта",
        BarrierKind.Rubble => "Завал — нужна лопата или взрывчатка",
        _ => "Заблокировано",
    };

    private void SetOpen(bool open)
    {
        Rpc(nameof(SyncState), open);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SyncState(bool open)
    {
        _open = open;
        ApplyVisual();
    }

    // Открытый барьер непроходим убираем и делаем невидимым.
    private void ApplyVisual()
    {
        _collision.Disabled = _open;
        _visual.Visible = !_open;
    }

    // Красим барьер по типу, чтобы их было легко различать на уровне.
    private void TintByKind()
    {
        Color color = Kind switch
        {
            BarrierKind.Vent => new Color(0.55f, 0.55f, 0.6f),   // серый металл
            BarrierKind.Grate => new Color(0.3f, 0.32f, 0.36f),  // тёмная решётка
            BarrierKind.CodedDoor => new Color(0.2f, 0.4f, 0.7f),// синяя дверь
            BarrierKind.Rubble => new Color(0.4f, 0.32f, 0.24f), // бурый завал
            _ => new Color(0.5f, 0.5f, 0.5f),
        };

        var material = new StandardMaterial3D { AlbedoColor = color };
        if (_visual is MeshInstance3D mesh)
        {
            mesh.MaterialOverride = material;
        }
    }
}
