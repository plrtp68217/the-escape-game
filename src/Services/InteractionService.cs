using Godot;
using EscapeGame.Interaction;

namespace EscapeGame.Services;

/// <summary>
/// Адаптер <see cref="InteractionRelay"/u003e под <see cref="IInteractionService"/u003e.
/// </summary>
public class InteractionService : IInteractionService
{
	private readonly InteractionRelay _relay;

	public InteractionService(InteractionRelay relay)
	{
		_relay = relay;
	}

	public void RequestInteract(long playerId, string interactablePath)
	{
		_relay?.RpcId(1, nameof(InteractionRelay.RequestInteract), playerId, interactablePath);
	}

	public void RequestAxeHit(long playerId, string doorPath)
	{
		_relay?.RpcId(1, nameof(InteractionRelay.RequestAxeHit), playerId, doorPath);
	}
}
