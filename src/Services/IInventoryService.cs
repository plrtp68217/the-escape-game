using Godot;
using EscapeGame.Player;

namespace EscapeGame.Services;

/// <summary>
/// Запросы к серверному обработчику инвентаря.
/// Экипировка и выброс работают по идентификатору предмета, а не по индексу
/// слота — так UI и игровая логика не зависят от порядка слотов в инвентаре.
/// Перемещение слотов остаётся UI-операцией и выражается индексами.
/// </summary>
public interface IInventoryService
{
	void RequestInventorySync(long playerId);
	void RequestEquip(long playerId, string itemId);
	void RequestMoveSlot(long playerId, int fromIndex, int toIndex);
	void RequestDrop(long playerId, string itemId, Vector3 position);
	void BroadcastInventory(PlayerController player);
	void BroadcastClearWorldItems();
}
