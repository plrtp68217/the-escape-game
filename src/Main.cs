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
	private EscapeGame.UI.Scoreboard _scoreboard;

	// Сообщение, которое нужно показать в меню после перезагрузки сцены.
	// Статическое, т.к. переживает пересоздание узла Main.
	private static string _pendingStatus = string.Empty;

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
		_lobbyMenu.LeaveRequested += OnLobbyLeave;

		_scoreboard = GetNode<EscapeGame.UI.Scoreboard>(R.UI.Scoreboard);
		_scoreboard.Visible = false;

		GetNode<Button>(R.UI.HostButton).Pressed += OnHostPressed;
		GetNode<Button>(R.UI.JoinButton).Pressed += OnJoinPressed;
		GetNode<Button>(R.UI.PauseLeaveButton).Pressed += OnPauseLeave;

		Input.MouseMode = Input.MouseModeEnum.Visible;
		GameState.SetPhase(GamePhase.MainMenu);

		NetworkManager.Instance.Connected += OnNetworkConnected;
		NetworkManager.Instance.ConnectionError += OnNetworkError;

		LobbyManager.Instance.GameStarted += OnGameStarted;
		LobbyManager.Instance.JoinRejectedGameInProgress += OnJoinRejected;

		// Восстановить сообщение, переданное через перезагрузку сцены
		// (например, «Игра уже идёт» или причина отключения).
		if (!string.IsNullOrEmpty(_pendingStatus))
		{
			_statusLabel.Text = _pendingStatus;
			_pendingStatus = string.Empty;
		}
	}

	public override void _ExitTree()
	{
		// Обязательно отписываемся от событий автозагрузок (они переживают
		// перезагрузку сцены), иначе останутся ссылки на уничтоженный узел.
		if (NetworkManager.Instance != null)
		{
			NetworkManager.Instance.Connected -= OnNetworkConnected;
			NetworkManager.Instance.ConnectionError -= OnNetworkError;
		}

		if (LobbyManager.Instance != null)
		{
			LobbyManager.Instance.GameStarted -= OnGameStarted;
			LobbyManager.Instance.JoinRejectedGameInProgress -= OnJoinRejected;
		}
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
		// Соединение не удалось / сервер отключился — возвращаемся в меню
		// с текстом причины.
		ReturnToMenu(reason);
	}

	// Клиент подключился, но игра уже идёт — сервер отклонил вход.
	private void OnJoinRejected()
	{
		ReturnToMenu(G.Messages.GameInProgress);
	}

	// Игрок нажал «Выйти» в лобби.
	private void OnLobbyLeave()
	{
		ReturnToMenu();
	}

	// Игрок нажал «Выйти в меню» в меню паузы.
	private void OnPauseLeave()
	{
		ReturnToMenu();
	}

	// Разрываем соединение и возвращаемся в чистое меню, перезагружая сцену.
	// Перезагрузка гарантированно убирает игроков, камеру и остатки мира,
	// иначе после выхода игрок остаётся стоять на карте.
	private void ReturnToMenu(string status = "")
	{
		NetworkManager.Instance.Disconnect();
		LobbyManager.Instance.Reset();

		_pendingStatus = status;
		GameState.SetPhase(GamePhase.MainMenu);
		Input.MouseMode = Input.MouseModeEnum.Visible;

		// Откладываем перезагрузку: ReturnToMenu часто вызывается из
		// обработчика сигнала (нажатие кнопки / сетевое событие), а
		// уничтожать текущую сцену прямо в нём нельзя.
		GetTree().CallDeferred(SceneTree.MethodName.ReloadCurrentScene);
	}

	public override void _Input(InputEvent @event)
	{
		if (GameState.CurrentPhase != GamePhase.Gameplay && GameState.CurrentPhase != GamePhase.Paused)
		{
			return;
		}

		// Табло игроков по удержанию TAB во время игры.
		if (@event is InputEventKey scoreKey && !scoreKey.Echo && scoreKey.Keycode == G.ScoreboardKey)
		{
			if (GameState.CurrentPhase == GamePhase.Gameplay)
			{
				_scoreboard.SetShown(scoreKey.Pressed);
			}
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
				_scoreboard.SetShown(false);
				GameState.SetPhase(GamePhase.Paused);

				Input.MouseMode = Input.MouseModeEnum.Visible;
			}
		}
	}
}
