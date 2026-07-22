using Godot;
using EscapeGame.Inventory;
using EscapeGame.Player;

namespace EscapeGame.Services;

/// <summary>
/// Адаптер <see cref="InventoryRelay"/u003e под <see cref="IInventoryService"/u003e.
/// </summary>
public class InventoryService : IInventoryService
{
	private readonly InventoryRelay _relay;

	public InventoryService(InventoryRelay relay)
	{
		_relay = relay;
	}

	public void RequestInventorySync(long playerId)
	{
		_relay?.RpcId(1, nameof(InventoryRelay.RequestInventorySync), playerId);
	}

	public void RequestEquip(long playerId, int slotIndex)
	{
		_relay?.RpcId(1, nameof(InventoryRelay.RequestEquip), playerId, slotIndex);
	}

	public void RequestMoveSlot(long playerId, int fromIndex, int toIndex)
	{
		_relay?.RpcId(1, nameof(InventoryRelay.RequestMoveSlot), playerId, fromIndex, toIndex);
	}

	public void RequestDrop(long playerId, int slotIndex, Vector3 position)
	{
		_relay?.RpcId(1, nameof(InventoryRelay.RequestDrop), playerId, slotIndex, position);
	}

	public void BroadcastInventory(PlayerController player)
	{
		_relay?.BroadcastInventory(player);
	}
}
