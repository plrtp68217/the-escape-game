using System.Linq;
using Godot;

namespace EscapeGame.Interaction;

/// <summary>
/// Серверный посредник для взаимодействий. Клиент присылает путь к объекту,
/// сервер проверяет и выполняет действие. Добавляется как autoload
/// (см. project.godot) и имеет authority сервера. По образцу InventoryRelay.
/// </summary>
public partial class InteractionRelay : Node
{
    public static InteractionRelay Instance { get; private set; }

    public override void _Ready()
    {
        Instance = this;
    }

    // Клиент просит сервер провести взаимодействие с объектом по его пути.
    // CallLocal = true, чтобы хост тоже мог вызывать локально при самоотправке.
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestInteract(long playerId, string interactablePath)
    {
        if (!Multiplayer.IsServer())
        {
            return;
        }

        PlayerController player = PlayerController.AllPlayers.Values
            .FirstOrDefault(p => p.PlayerId == playerId);
        if (player == null)
        {
            return;
        }

        Node node = GetNodeOrNull(interactablePath);
        if (node is not IInteractable interactable || !node.IsInsideTree())
        {
            return;
        }

        if (!interactable.CanInteract(player))
        {
            return;
        }

        interactable.Interact(player);
    }

    // Заключённый бьёт топором по двери (ЛКМ). Клиент присылает путь к двери,
    // сервер проверяет и наносит удар. Отдельно от RequestInteract, т.к. это
    // атака по ЛКМ, а не взаимодействие по F.
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestAxeHit(long playerId, string doorPath)
    {
        if (!Multiplayer.IsServer())
        {
            return;
        }

        PlayerController player = PlayerController.AllPlayers.Values
            .FirstOrDefault(p => p.PlayerId == playerId);
        if (player == null)
        {
            return;
        }

        Node node = GetNodeOrNull(doorPath);
        if (node is CellDoor door && node.IsInsideTree())
        {
            door.HitWithAxe(player);
        }
    }
}
