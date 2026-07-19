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
	public event System.Action<int> SlotDropRequested;

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
		// Ячейка — кнопка (ловит ЛКМ/ПКМ), поверх неё иконка и бейдж количества.
		// Иконку и число рисуем отдельными узлами, чтобы число было видно ПОВЕРХ
		// иконки в углу, а не пряталось под ней (как было с ExpandIcon).
		var button = new Button
		{
			CustomMinimumSize = new Vector2(CellSize, CellSize),
		};

		if (slot.IsEmpty)
		{
			button.Pressed += () => SlotSelected?.Invoke(index);
			AttachDropHandler(button, index);
			return button;
		}

		button.TooltipText = $"{slot.Item.Name} ({slot.Count})";

		var icon = new TextureRect
		{
			Texture = slot.Item.Icon,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		icon.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		icon.OffsetLeft = 6;
		icon.OffsetTop = 6;
		icon.OffsetRight = -6;
		icon.OffsetBottom = -6;
		button.AddChild(icon);

		// Бейдж количества в правом нижнем углу — виден для стака (>1).
		if (slot.Count > 1)
		{
			var count = new Label
			{
				Text = slot.Count.ToString(),
				HorizontalAlignment = HorizontalAlignment.Right,
				VerticalAlignment = VerticalAlignment.Bottom,
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};
			count.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			count.OffsetRight = -4;
			count.OffsetBottom = -2;
			count.AddThemeFontSizeOverride("font_size", 18);
			count.AddThemeColorOverride("font_color", Colors.White);
			count.AddThemeColorOverride("font_outline_color", Colors.Black);
			count.AddThemeConstantOverride("outline_size", 5);
			button.AddChild(count);
		}

		button.Pressed += () => SlotSelected?.Invoke(index);
		AttachDropHandler(button, index);
		return button;
	}

	// ПКМ по ячейке — выбросить предмет из этого слота.
	private void AttachDropHandler(Button button, int index)
	{
		button.GuiInput += @event =>
		{
			if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right })
			{
				SlotDropRequested?.Invoke(index);
			}
		};
	}
}
