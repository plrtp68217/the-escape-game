using Godot;
using EscapeGame.Inventory;

namespace EscapeGame.UI;

/// <summary>
/// Экран инвентаря. Отображает сетку одинаковых ячеек с иконками. Первые
/// G.Hotbar.SlotCount слотов (зона хотбара) выделены цветом. Предметы можно
/// перетаскивать между слотами (drag-and-drop).
/// </summary>
public partial class InventoryUI : Control
{
	private const int CellSize = 72;

	// Подсветка зоны хотбара (верхний ряд слотов) — синеватый фон и рамка.
	private static readonly Color HotbarBg = new(0.16f, 0.28f, 0.42f, 0.85f);
	private static readonly Color HotbarBorder = new(0.4f, 0.65f, 1f);

	private GridContainer _grid;
	private Inventory.PlayerInventory _inventory;

	public event System.Action<int> SlotSelected;
	public event System.Action<int> SlotDropRequested;
	public event System.Action<int, int> SlotMoveRequested;

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
			Control cell = CreateCell(_inventory.Slots[i], i);
			_grid.AddChild(cell);
		}
	}

	private Control CreateCell(Inventory.InventorySlot slot, int index)
	{
		// Ячейка — кнопка (ловит ЛКМ/ПКМ + drag-and-drop), поверх неё иконка и
		// бейдж количества. Иконку и число рисуем отдельными узлами, чтобы число
		// было видно ПОВЕРХ иконки в углу, а не пряталось под ней.
		bool inHotbar = index < G.Hotbar.SlotCount;

		var button = new InventorySlotCell
		{
			CustomMinimumSize = new Vector2(CellSize, CellSize),
			SlotIndex = index,
			Empty = slot.IsEmpty,
			DragIcon = slot.IsEmpty ? null : slot.Item.Icon,
		};

		// Слоты зоны хотбара визуально отличаются по цвету (см. требование).
		if (inHotbar)
		{
			ApplyHotbarStyle(button);
		}

		button.Selected += i => SlotSelected?.Invoke(i);
		button.DropRequested += i => SlotDropRequested?.Invoke(i);
		button.MoveRequested += (from, to) => SlotMoveRequested?.Invoke(from, to);

		if (slot.IsEmpty)
		{
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

		return button;
	}

	// Заливает кнопку слота цветом зоны хотбара для всех состояний кнопки.
	private static void ApplyHotbarStyle(Button button)
	{
		foreach (string state in new[] { "normal", "hover", "pressed", "focus" })
		{
			var style = new StyleBoxFlat
			{
				BgColor = HotbarBg,
				BorderColor = HotbarBorder,
			};
			style.SetBorderWidthAll(2);
			style.SetCornerRadiusAll(4);
			button.AddThemeStyleboxOverride(state, style);
		}
	}
}

/// <summary>
/// Ячейка инвентаря: кнопка с поддержкой drag-and-drop. Левый клик выбирает
/// слот, правый — выбрасывает предмет, перетаскивание — перемещает/меняет слоты.
/// </summary>
public partial class InventorySlotCell : Button
{
	public int SlotIndex { get; set; }
	public bool Empty { get; set; }
	public Texture2D DragIcon { get; set; }

	public event System.Action<int> Selected;
	public event System.Action<int> DropRequested;
	public event System.Action<int, int> MoveRequested;

	public override void _Ready()
	{
		Pressed += () => Selected?.Invoke(SlotIndex);
		GuiInput += OnGuiInput;
	}

	private void OnGuiInput(InputEvent @event)
	{
		// ПКМ по ячейке — выбросить предмет из этого слота.
		if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right })
		{
			DropRequested?.Invoke(SlotIndex);
		}
	}

	// Начало перетаскивания: тянуть можно только непустой слот. Возвращаем индекс
	// исходного слота как полезную нагрузку и показываем иконку под курсором.
	public override Variant _GetDragData(Vector2 atPosition)
	{
		if (Empty)
		{
			return default;
		}

		var preview = new TextureRect
		{
			Texture = DragIcon,
			CustomMinimumSize = new Vector2(48, 48),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			Modulate = new Color(1f, 1f, 1f, 0.85f),
		};
		SetDragPreview(preview);

		return SlotIndex;
	}

	// Принимаем перетаскивание только от другого слота инвентаря (int-нагрузка).
	public override bool _CanDropData(Vector2 atPosition, Variant data)
	{
		return data.VariantType == Variant.Type.Int;
	}

	public override void _DropData(Vector2 atPosition, Variant data)
	{
		int from = data.AsInt32();
		if (from != SlotIndex)
		{
			MoveRequested?.Invoke(from, SlotIndex);
		}
	}
}
