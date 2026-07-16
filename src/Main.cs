using Godot;

public partial class Main : Node3D
{
	private ColorRect _pauseMenu;
	private Label _tipLabel;
	private Control _networkMenu;
	private LineEdit _ipInput;
	private Label _statusLabel;

	public override void _Ready()
	{
		_pauseMenu = GetNode<ColorRect>("UserInterface/PauseMenu");
		_pauseMenu.Visible = false;

		_tipLabel = GetNode<Label>("UserInterface/TipLabel");
		_tipLabel.Visible = false;

		_networkMenu = GetNode<Control>("UserInterface/NetworkMenu");
		_ipInput = GetNode<LineEdit>("UserInterface/NetworkMenu/VBoxContainer/IpInput");
		_statusLabel = GetNode<Label>("UserInterface/NetworkMenu/VBoxContainer/StatusLabel");

		GetNode<Button>("UserInterface/NetworkMenu/VBoxContainer/HostButton").Pressed += OnHostPressed;
		GetNode<Button>("UserInterface/NetworkMenu/VBoxContainer/JoinButton").Pressed += OnJoinPressed;

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
			_statusLabel.Text = $"Ошибка старта сервера: {err}";
		}
	}

	private void OnJoinPressed()
	{
		string address = string.IsNullOrWhiteSpace(_ipInput.Text) ? "127.0.0.1" : _ipInput.Text.Trim();
		Error err = NetworkManager.Instance.JoinServer(address);
		_statusLabel.Text = err != Error.Ok ? $"Ошибка подключения: {err}" : "Подключение...";
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