using Godot;

public partial class Main : Node3D
{
	private ColorRect _pauseMenu; 
	private Label _tipLabel;

	public override void _Ready()
	{
		_pauseMenu = GetNode<ColorRect>("UserInterface/PauseMenu");
		_pauseMenu.Visible = false;

		_tipLabel = GetNode<Label>("UserInterface/TipLabel");
		_tipLabel.Visible = true;

		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
		{
			if (_pauseMenu.Visible)
			{
				_pauseMenu.Visible = false;
				_tipLabel.Visible = true;

				Input.MouseMode = Input.MouseModeEnum.Captured;
			}
			else
			{
				_pauseMenu.Visible = true;
				_tipLabel.Visible = false;

				Input.MouseMode = Input.MouseModeEnum.Visible;
			}
		}
	}
}