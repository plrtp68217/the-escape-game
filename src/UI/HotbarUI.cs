using Godot;

namespace EscapeGame.UI;

/// <summary>
/// Хотбар быстрого доступа внизу экрана: первые G.Hotbar.SlotCount слотов
/// инвентаря. Показывается только в игре, экипированный слот подсвечен.
/// Переключение — колесом мыши (см. PlayerController.CycleHotbar).
/// </summary>
public partial class HotbarUI : Control
{
	private const int CellWidth = 64;
	private const int CellHeight = 74;

	private static readonly Color EquippedBorder = new(1f, 0.85f, 0.2f);
	private static readonly Color IdleBorder = new(1f, 1f, 1f, 0.25f);

	private HBoxContainer _row;
	private Inventory.PlayerInventory _inventory;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		_row = GetNode<HBoxContainer>("Row");
	}

	public void Bind(Inventory.PlayerInventory inventory)
	{
		_inventory = inventory;
		Refresh();
	}

	public void Refresh()
	{
		if (_inventory == null || _row == null)
		{
			return;
		}

		foreach (Node child in _row.GetChildren())
		{
			child.QueueFree();
		}

		int count = Mathf.Min(G.Hotbar.SlotCount, _inventory.Slots.Count);
		for (int i = 0; i < count; i++)
		{
			_row.AddChild(CreateCell(_inventory.Slots[i], i, i == _inventory.EquippedSlotIndex));
		}
	}

	private Control CreateCell(Inventory.InventorySlot slot, int index, bool equipped)
	{
		var panel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(CellWidth, CellHeight),
			MouseFilter = MouseFilterEnum.Ignore,
		};

		var style = new StyleBoxFlat
		{
			BgColor = new Color(0f, 0f, 0f, 0.5f),
			BorderColor = equipped ? EquippedBorder : IdleBorder,
		};
		style.SetBorderWidthAll(equipped ? 3 : 1);
		style.SetCornerRadiusAll(4);
		style.ContentMarginTop = 4;
		style.ContentMarginBottom = 4;
		style.ContentMarginLeft = 4;
		style.ContentMarginRight = 4;
		panel.AddThemeStyleboxOverride("panel", style);

		var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		panel.AddChild(box);

		// Номер клавиши быстрого выбора (1..N) в левом верхнем углу ячейки.
		var keyLabel = new Label
		{
			Text = (index + 1).ToString(),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		keyLabel.SetAnchorsPreset(LayoutPreset.TopLeft);
		keyLabel.OffsetLeft = 4;
		keyLabel.OffsetTop = 2;
		keyLabel.AddThemeFontSizeOverride("font_size", 12);
		keyLabel.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0.7f));
		keyLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
		keyLabel.AddThemeConstantOverride("outline_size", 3);
		panel.AddChild(keyLabel);

		var icon = new TextureRect
		{
			CustomMinimumSize = new Vector2(44, 44),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		if (!slot.IsEmpty)
		{
			icon.Texture = slot.Item.Icon;
		}
		box.AddChild(icon);

		var label = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			ClipText = true,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		label.AddThemeFontSizeOverride("font_size", 11);
		if (!slot.IsEmpty)
		{
			label.Text = slot.Count > 1 ? $"{slot.Item.Name} x{slot.Count}" : slot.Item.Name;
		}
		box.AddChild(label);

		return panel;
	}
}
