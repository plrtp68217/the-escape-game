using Godot;
using EscapeGame.Services;

namespace EscapeGame.Player;

/// <summary>
/// Управление инвентарём и хотбаром: экипировка, выброс, переключение слотов.
/// </summary>
public partial class PlayerInventoryController : Node
{
	private PlayerController _player;
	private Marker3D _hand;
	private Vector3 _handBaseRotation;

	private IInventoryService _inventoryService;

	public override void _Ready()
	{
		_player = GetParent<PlayerController>();
		_hand = _player.GetNodeOrNull<Marker3D>("Pivot/Hand");
		_inventoryService = ServiceLocator.Inventory;
		if (_hand != null)
		{
			_handBaseRotation = _hand.Rotation;
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (!IsActive())
		{
			return;
		}

		if (@event.IsActionPressed("drop") && _player.VitalState == PlayerVitalState.Alive)
		{
			RequestDropEquipped();
		}

		if (@event is InputEventMouseButton { Pressed: true } wheel
			&& Input.GetMouseMode() == Input.MouseModeEnum.Captured)
		{
			if (wheel.ButtonIndex == MouseButton.WheelUp)
			{
				CycleHotbar(-1);
			}
			else if (wheel.ButtonIndex == MouseButton.WheelDown)
			{
				CycleHotbar(1);
			}
		}

		if (@event is InputEventKey { Pressed: true, Echo: false } numberKey
			&& Input.GetMouseMode() == Input.MouseModeEnum.Captured)
		{
			int hotbarSlot = HotbarSlotFromKey(numberKey.Keycode);
			if (hotbarSlot >= 0)
			{
				EquipHotbarSlot(hotbarSlot);
			}
		}
	}

	public void RefreshEquippedModel()
	{
		EquipItem(_player.Inventory.EquippedSlot?.Item);
	}

	public void EquipItem(Inventory.InventoryItem item)
	{
		if (_hand == null)
		{
			return;
		}

		foreach (Node child in _hand.GetChildren())
		{
			child.QueueFree();
		}

		if (item?.WorldModel == null)
		{
			return;
		}

		Node3D model = item.WorldModel.Instantiate<Node3D>();

		if (model is Inventory.WorldItem heldItem)
		{
			heldItem.Monitoring = false;
			heldItem.Monitorable = false;
		}

		_hand.AddChild(model);
		NormalizeHeldModel(model);
	}

	public void RequestDropEquipped()
	{
		if (_player.VitalState != PlayerVitalState.Alive)
		{
			return;
		}

		Inventory.InventorySlot slot = _player.Inventory.EquippedSlot;
		if (slot == null || slot.IsEmpty)
		{
			return;
		}

		RequestDropSlot(_player.Inventory.EquippedSlotIndex);
	}

	public void RequestDropSlot(int slotIndex)
	{
		Vector3 forward = -_player.GlobalTransform.Basis.Z;
		Vector3 position = _player.GlobalPosition + forward * G.DropDistance + Vector3.Up * G.DropHeight;

		_inventoryService?.RequestDrop((long)_player.PlayerId, slotIndex, position);
	}

	public void CycleHotbar(int direction)
	{
		int count = Mathf.Min(G.Hotbar.SlotCount, _player.Inventory.Slots.Count);
		if (count <= 0)
		{
			return;
		}

		int current = _player.Inventory.EquippedSlotIndex;
		int start = current >= 0 && current < count ? current : (direction > 0 ? -1 : 0);

		for (int step = 1; step <= count; step++)
		{
			int index = ((start + direction * step) % count + count) % count;
			if (!_player.Inventory.Slots[index].IsEmpty)
			{
				_inventoryService?.RequestEquip((long)_player.PlayerId, index);
				return;
			}
		}
	}

	public void EquipHotbarSlot(int index)
	{
		if (_player.VitalState != PlayerVitalState.Alive)
		{
			return;
		}

		int count = Mathf.Min(G.Hotbar.SlotCount, _player.Inventory.Slots.Count);
		if (index < 0 || index >= count || _player.Inventory.Slots[index].IsEmpty)
		{
			return;
		}

		_inventoryService?.RequestEquip((long)_player.PlayerId, index);
	}

	private static int HotbarSlotFromKey(Key key) => key switch
	{
		Key.Key1 => 0,
		Key.Key2 => 1,
		Key.Key3 => 2,
		Key.Key4 => 3,
		Key.Key5 => 4,
		_ => -1,
	};

	private void NormalizeHeldModel(Node3D model)
	{
		Inventory.ModelBounds.FitVisibleSize(model, 0.25f);

		if (Inventory.ModelBounds.TryComputeWorldAabb(model, out Aabb scaled))
		{
			model.GlobalPosition += _hand.GlobalPosition - scaled.GetCenter();
		}
	}

	private bool IsActive()
	{
		if (GameFlow.GameState.CurrentPhase != GameFlow.GamePhase.Gameplay
			&& GameFlow.GameState.CurrentPhase != GameFlow.GamePhase.Inventory)
		{
			return false;
		}

		return ServiceLocator.Network?.HasPeer ?? false
			&& _player.IsMultiplayerAuthority();
	}
}
