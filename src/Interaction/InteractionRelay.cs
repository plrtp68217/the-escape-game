using System;
using Godot;
using EscapeGame.Player;
using EscapeGame.Services;

namespace EscapeGame.Interaction;

/// <summary>
/// Серверный посредник для взаимодействий с объектами мира. Авторитет
/// сервера: клиенты посылают запросы вида <see cref="InteractionRequest"/u003e,
/// сервер валидирует и выполняет. Добавление нового вида взаимодействия
/// не требует менять сигнатуру сервиса — только регистрацию обработчика здесь.
/// </summary>
public partial class InteractionRelay : Node
{
    public static InteractionRelay Instance { get; private set; }

    public override void _Ready()
    {
        Instance = this;
    }

    // Универсальный RPC: клиенты шлют запрос, сервер диспатчит по Kind.
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ExecuteInteraction(int kind, long playerId, string targetPath, string payload)
    {
        if (!ServiceLocator.Network?.IsServer ?? false)
        {
            return;
        }

        long senderId = Multiplayer.GetRemoteSenderId();
        if (senderId != 0 && playerId != senderId)
        {
            GD.PushWarning($"[InteractionRelay] PlayerId mismatch: expected {senderId}, got {playerId}");
            return;
        }

        var request = new InteractionRequest((InteractionKind)kind, playerId, targetPath, payload);
        Dispatch(request);
    }

    private void Dispatch(InteractionRequest request)
    {
        Node target = GetNodeOrNull(request.TargetPath);
        PlayerController player = ServiceLocator.Players.Get(request.PlayerId);
        if (player == null || target == null || !target.IsInsideTree())
        {
            return;
        }

        switch (request.Kind)
        {
            case InteractionKind.Use:
                if (target is IInteractable interactable
                    && interactable.CanInteract(player))
                {
                    interactable.Interact(player);
                }
                break;

            case InteractionKind.AxeHit:
                if (target is IAxeHittable hittable)
                {
                    hittable.HitWithAxe(player);
                }
                break;

            default:
                GD.PushWarning($"[InteractionRelay] Неизвестный вид взаимодействия: {request.Kind}");
                break;
        }
    }
}
