using EscapeGame.Combat;

namespace EscapeGame.Services;

/// <summary>
/// Адаптер <see cref="CombatRelay"/u003e под <see cref="ICombatService"/u003e.
/// </summary>
public class CombatService : ICombatService
{
	private readonly CombatRelay _relay;

	public CombatService(CombatRelay relay)
	{
		_relay = relay;
	}

	public void RequestAttack(long attackerId, long targetId)
	{
		_relay?.RpcId(1, nameof(CombatRelay.RequestAttack), attackerId, targetId);
	}

	public void RequestUseItem(long playerId, int slotIndex)
	{
		_relay?.RpcId(1, nameof(CombatRelay.RequestUseItem), playerId, slotIndex);
	}

	public void RequestRevive(long reviverId)
	{
		_relay?.RpcId(1, nameof(CombatRelay.RequestRevive), reviverId);
	}

	public void ResetAll()
	{
		_relay?.ResetAll();
	}
}
