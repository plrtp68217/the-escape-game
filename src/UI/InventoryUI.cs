using Godot;

namespace EscapeGame.UI;

/// <summary>
/// Экран инвентаря. Отображает сетку одинаковых ячеек с иконками.
/// </summary>
public partial class InventoryUI : Control
{
	private const int CellSize = 72;

	private GridContainer _grid;
	private Inventory.PlayerInventory _inventory;

	public event System.Action<int> SlotSelected;

	public override void _Ready()
	{
		Visible = false;

		_grid = GetNode<GridContainer>("Panel/VBoxContainer/GridContainer");
	}

	public void Bind(Inventory.PlayerInventory inventory)
	{
		_inventory = inventory;
		Refresh();
	}

	public void Toggle()
	{
		Visible = !Visible;
		if (Visible)
		{
			Refresh();
		}
	}

	public void Open()
	{
		Visible = true;
		Refresh();
	}

	public void Close()
	{
		Visible = false;
	}

	public void Refresh()
	{
		if (_inventory == null)
		{
			return;
		}

		foreach (Node child in _grid.GetChildren())
		{
			child.QueueFree();
		}

		_grid.Columns = _inventory.Width;

		for (int i = 0; i < _inventory.Slots.Count; i++)
		{
			var slot = _inventory.Slots[i];
			int index = i;

			Control cell = CreateCell(slot, index);
			_grid.AddChild(cell);
		}
	}

	private Control CreateCell(Inventory.InventorySlot slot, int index)
	{
		var button = new Button
		{
			CustomMinimumSize = new Vector2(CellSize, CellSize),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			ClipText = true,
			ExpandIcon = true,
			IconAlignment = HorizontalAlignment.Center,
		};

		if (!slot.IsEmpty)
		{
			button.Text = slot.Count.ToString();
			button.Icon = slot.Item.Icon;
		}

		button.Pressed += () => SlotSelected?.Invoke(index);
		return button;
	}
}
