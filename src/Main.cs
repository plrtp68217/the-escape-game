using Godot;

namespace EscapeGame;

public partial class Main : Node3D
{
	private ColorRect _pauseMenu;
	private Label _tipLabel;
	private Control _networkMenu;
	private LineEdit _nameInput;
	private LineEdit _ipInput;
	private Label _statusLabel;
	private EscapeGame.UI.LobbyMenu _lobbyMenu;

	public override void _Ready()
	{
		_pauseMenu = GetNode<ColorRect>(R.UI.PauseMenu);
		_pauseMenu.Visible = false;

		_tipLabel = GetNode<Label>(R.UI.TipLabel);
		_tipLabel.Visible = false;

		_networkMenu = GetNode<Control>(R.UI.NetworkMenu);
		_nameInput = GetNode<LineEdit>(R.UI.NameInput);
		_ipInput = GetNode<LineEdit>(R.UI.IpInput);
		_statusLabel = GetNode<Label>(R.UI.StatusLabel);

		_lobbyMenu = GetNode<EscapeGame.UI.LobbyMenu>(R.UI.LobbyMenu);
		_lobbyMenu.Visible = false;

		GetNode<Button>(R.UI.HostButton).Pressed += OnHostPressed;
		GetNode<Button>(R.UI.JoinButton).Pressed += OnJoinPressed;

		Input.MouseMode = Input.MouseModeEnum.Visible;
		GameState.SetPhase(GamePhase.MainMenu);

		NetworkManager.Instance.Connected += OnNetworkConnected;
		NetworkManager.Instance.ConnectionError += OnNetworkError;

		LobbyManager.Instance.GameStarted += OnGameStarted;
	}

	private string GetPlayerName()
	{
		string name = _nameInput.Text?.Trim() ?? string.Empty;
		return string.IsNullOrWhiteSpace(name) ? G.Messages.DefaultPlayerName : name;
	}

	private void OnHostPressed()
	{
		Error err = NetworkManager.Instance.CreateHost();
		if (err != Error.Ok)
		{
			_statusLabel.Text = $"{G.Messages.ServerStartError}: {err}";
			return;
		}

		EnterLobby();
		LobbyManager.Instance.SendPlayerName(GetPlayerName());
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
		EnterLobby();
		LobbyManager.Instance.SendPlayerName(GetPlayerName());
	}

	private void EnterLobby()
	{
		_networkMenu.Visible = false;
		_lobbyMenu.ResetState();
		_lobbyMenu.Visible = true;
		GameState.SetPhase(GamePhase.Lobby);

		Input.MouseMode = Input.MouseModeEnum.Visible;
	}

	private void OnGameStarted()
	{
		GameState.SetPhase(GamePhase.Gameplay);
		_lobbyMenu.Visible = false;
		_tipLabel.Visible = true;
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	private void OnNetworkError(string reason)
	{
		_statusLabel.Text = reason;
	}

	public override void _Input(InputEvent @event)
	{
		if (GameState.CurrentPhase != GamePhase.Gameplay && GameState.CurrentPhase != GamePhase.Paused)
		{
			return;
		}

		if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == G.PauseKey)
		{
			if (_pauseMenu.Visible)
			{
				_pauseMenu.Visible = false;
				_tipLabel.Visible = true;
				GameState.SetPhase(GamePhase.Gameplay);

				Input.MouseMode = Input.MouseModeEnum.Captured;
			}
			else
			{
				_pauseMenu.Visible = true;
				_tipLabel.Visible = false;
				GameState.SetPhase(GamePhase.Paused);

				Input.MouseMode = Input.MouseModeEnum.Visible;
			}
		}
	}
}
