using Godot;

namespace EscapeGame;

public partial class Main : Node3D
{
	private ColorRect _pauseMenu;
	private Label _tipLabel;
	private Control _networkMenu;
	private LineEdit _ipInput;
	private Label _statusLabel;

	public override void _Ready()
	{
		_pauseMenu = GetNode<ColorRect>(R.UI.PauseMenu);
		_pauseMenu.Visible = false;

		_tipLabel = GetNode<Label>(R.UI.TipLabel);
		_tipLabel.Visible = false;

		_networkMenu = GetNode<Control>(R.UI.NetworkMenu);
		_ipInput = GetNode<LineEdit>(R.UI.IpInput);
		_statusLabel = GetNode<Label>(R.UI.StatusLabel);

		GetNode<Button>(R.UI.HostButton).Pressed +=
			OnHostPressed;
		GetNode<Button>(R.UI.JoinButton).Pressed +=
			OnJoinPressed;

		// Пока не подключились - мышь свободна, чтобы можно было нажимать кнопки.
		Input.MouseMode = Input.MouseModeEnum.Visible;

		NetworkManager.Instance.Connected += OnNetworkConnected;
		NetworkManager.Instance.ConnectionError += OnNetworkError;
	}

	private void OnHostPressed()
	{
		Error err = NetworkManager.Instance.CreateHost();
		if (err != Error.Ok)
		{
			_statusLabel.Text = $"{G.Messages.ServerStartError}: {err}";
		}
	}

	private void OnJoinPressed()
	{
		string address = string.IsNullOrWhiteSpace(_ipInput.Text)
			? G.DefaultAddress
			: _ipInput.Text.Trim();
		Error err = NetworkManager.Instance.JoinServer(address);
		_statusLabel.Text = err != Error.Ok ? $"{G.Messages.JoinError}: {err}" : G.Messages.Joining;
	}

	private void OnNetworkConnected()
	{
		_networkMenu.Visible = false;
		_tipLabel.Visible = true;
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	private void OnNetworkError(string reason)
	{
		_statusLabel.Text = reason;
	}

	public override void _Input(InputEvent @event)
	{
		// Пока не подключились к игре, пауза не нужна.
		if (_networkMenu.Visible)
		{
			return;
		}

		if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == G.PauseKey)
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
