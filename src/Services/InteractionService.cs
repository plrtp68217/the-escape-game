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

	public void Execute(InteractionRequest request)
	{
		_relay?.RpcId(1, nameof(InteractionRelay.ExecuteInteraction),
			(int)request.Kind, request.PlayerId, request.TargetPath, request.Payload);
	}
}
