using Godot;
using EscapeGame.Player;

namespace EscapeGame.Services;

/// <summary>
/// Запросы к серверному обработчику инвентаря.
/// </summary>
public interface IInventoryService
{
	void RequestInventorySync(long playerId);
	void RequestEquip(long playerId, int slotIndex);
	void RequestMoveSlot(long playerId, int fromIndex, int toIndex);
	void RequestDrop(long playerId, int slotIndex, Vector3 position);
	void BroadcastInventory(PlayerController player);
}
