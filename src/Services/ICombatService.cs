using Godot;

namespace EscapeGame.Services;

/// <summary>
/// Запросы к серверному обработчику боя и лечения.
/// </summary>
public interface ICombatService
{
	void RequestAttack(long attackerId, long targetId);
	void RequestUseItem(long playerId, int slotIndex);
	void RequestRevive(long reviverId);
	void ResetAll();
}
