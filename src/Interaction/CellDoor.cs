using System.Collections.Generic;
using Godot;
using EscapeGame.Inventory;
using EscapeGame.Player;
using EscapeGame.Services;

namespace EscapeGame.Interaction;

/// <summary>
/// Дверь камеры. Заперта по умолчанию. Заключённый открывает её ключом или
/// выбивает топором за несколько ударов; надзиратель может запирать и закрывать.
/// Состояние — авторитет сервера, рассылается всем через SyncState.
/// </summary>
public partial class CellDoor : StaticBody3D, IInteractable
{
    // Все двери в сцене — по образцу PlayerController.AllPlayers. Нужен, чтобы
    // при перезапуске раунда запереть их все, не завися от путей в сцене.
    public static readonly HashSet<CellDoor> All = new();

    [Export]
    public bool Locked { get; set; } = true;

    private bool _isOpen;
    private int _health = G.Door.Health;

    private CollisionShape3D _collision;
    private Node3D _visual;
    private Area3D _zone;
    private Tween _doorTween;

    public override void _Ready()
    {
        _collision = GetNode<CollisionShape3D>("CollisionShape3D");
        _visual = GetNode<Node3D>("MeshInstance3D");
        _zone = GetNode<Area3D>("InteractZone");

        _zone.BodyEntered += OnBodyEntered;
        _zone.BodyExited += OnBodyExited;

        All.Add(this);

        ApplyVisual();
    }

    public override void _ExitTree()
    {
        All.Remove(this);
    }

    // Возврат двери в исходное состояние (заперта, закрыта, цела) при
    // перезапуске раунда. Только сервер; состояние рассылается через SyncState.
    public void ResetState()
    {
        if (!ServiceLocator.Network?.IsServer ?? false)
        {
            return;
        }

        _health = G.Door.Health;
        SetState(isOpen: false, locked: true);
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
            return "ЛКМ — выбить топором";
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

        // Заперта: открыть можно только ключом (расходуется). Топором дверь
        // выбивают ударами по ЛКМ — см. HitWithAxe.
        if (player.Inventory.RemoveOne(G.Door.KeyItemId))
        {
            Inventory.InventoryRelay.Instance?.BroadcastInventory(player);
            SetState(isOpen: true, locked: false);
        }
    }

    // Удар топором по двери (только сервер, вызывается из InteractionRelay,
    // когда заключённый бьёт по ЛКМ). Промежуточные удары считаем на сервере,
    // рассылаем состояние только когда дверь реально распахнулась, но на каждый
    // удар шлём отдельную реакцию — чтобы прогресс было видно.
    public void HitWithAxe(PlayerController player)
    {
        if (player.Role != PlayerRole.Prisoner || _isOpen || !Locked)
        {
            return;
        }

        if (!IsAxeEquipped(player))
        {
            return;
        }

        _health--;
        if (_health <= 0)
        {
            SetState(isOpen: true, locked: false);
        }
        else
        {
            Rpc(nameof(PlayStruck));
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
        bool wasOpen = _isOpen;
        _isOpen = isOpen;
        Locked = locked;

        // Проходимость меняем сразу, а визуал доигрываем анимацией.
        _collision.Disabled = _isOpen;

        if (isOpen && !wasOpen)
        {
            AnimateOpen();
        }
        else if (!isOpen && wasOpen)
        {
            AnimateClose();
        }
        else
        {
            // Дверь осталась закрытой (например, щёлкнул замок) — короткий толчок.
            _visual.Visible = true;
            ShakeVisual();
        }
    }

    // Реакция на удар топором, который ещё не выбил дверь (сервер → все пиры).
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void PlayStruck()
    {
        ShakeVisual();
    }

    // Начальное состояние двери (без анимации).
    private void ApplyVisual()
    {
        _collision.Disabled = _isOpen;
        _visual.Visible = !_isOpen;
    }

    // Распахивание: дверь резко поворачивается и исчезает.
    private void AnimateOpen()
    {
        _doorTween?.Kill();
        _visual.Visible = true;
        _visual.Rotation = Vector3.Zero;

        _doorTween = CreateTween();
        _doorTween.TweenProperty(_visual, "rotation:y", Mathf.DegToRad(100f), 0.25)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        _doorTween.TweenCallback(Callable.From(() =>
        {
            _visual.Visible = false;
            _visual.Rotation = Vector3.Zero;
        }));
    }

    // Захлопывание: дверь появляется с лёгким «ударом» (толчок туда-обратно).
    private void AnimateClose()
    {
        _doorTween?.Kill();
        _visual.Visible = true;
        _visual.Rotation = new Vector3(0f, Mathf.DegToRad(-20f), 0f);

        _doorTween = CreateTween();
        _doorTween.TweenProperty(_visual, "rotation:y", 0f, 0.15)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    // Короткая дрожь двери — удар топором или щелчок замка.
    private void ShakeVisual()
    {
        _doorTween?.Kill();
        _visual.Rotation = Vector3.Zero;

        _doorTween = CreateTween();
        _doorTween.TweenProperty(_visual, "rotation:z", Mathf.DegToRad(3f), 0.03);
        _doorTween.TweenProperty(_visual, "rotation:z", Mathf.DegToRad(-2f), 0.05);
        _doorTween.TweenProperty(_visual, "rotation:z", 0f, 0.05);
    }
}
