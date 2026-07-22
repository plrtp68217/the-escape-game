using Godot;
using EscapeGame.GameFlow;
using EscapeGame.Player;
using EscapeGame.Services;

namespace EscapeGame.Core;

/// <summary>
/// Зона выхода. Когда заключённый в неё попадает, сервер засчитывает побег.
/// </summary>
public partial class ExitZone : Area3D
{
    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node3D body)
    {
        // Физический сигнал может прийти, когда сетевого пира ещё/уже нет
        // (меню, момент отключения). IsServer() без пира сыпет ошибкой — выходим.
        if (!(ServiceLocator.Network?.HasPeer ?? false) || !(ServiceLocator.Network?.IsServer ?? false))
        {
            return;
        }

        if (body is PlayerController player && player.Role == PlayerRole.Prisoner)
        {
            RoundManager.Instance?.NotifyEscaped(player.PlayerId);
        }
    }
}
