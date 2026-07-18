using Godot;

namespace EscapeGame;

public partial class Main : Node3D
{
    private EscapeGame.UI.ScreenManager _screenManager;
    private EscapeGame.UI.MainMenu _mainMenu;
    private EscapeGame.UI.LobbyMenu _lobbyMenu;
    private EscapeGame.UI.PauseMenu _pauseMenu;
    private EscapeGame.UI.Scoreboard _scoreboard;

    private Label _tipLabel;

    // Сообщение, которое нужно показать в меню после перезагрузки сцены.
    // Статическое, т.к. переживает пересоздание узла Main.
    private static string _pendingStatus = string.Empty;

    public override void _Ready()
    {
        InstantiateUI();

        _tipLabel = _screenManager.GetNode<Label>(R.UI.TipLabel);

        _mainMenu.HostRequested += OnHostPressed;
        _mainMenu.JoinRequested += OnJoinPressed;

        _lobbyMenu.LeaveRequested += () => ReturnToMenu();
        _lobbyMenu.ReadyToggled += OnReadyToggled;
        _lobbyMenu.StartRequested += OnStartRequested;

        _pauseMenu.LeaveRequested += () => ReturnToMenu();

        _scoreboard.SetVisibilitySource(() => GameState.CurrentPhase == GamePhase.Gameplay);

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
            _mainMenu.SetStatus(_pendingStatus);
            _pendingStatus = string.Empty;
        }
    }

    private void InstantiateUI()
    {
        var scene = GD.Load<PackedScene>(R.UIRootScene);
        _screenManager = scene.Instantiate<EscapeGame.UI.ScreenManager>();
        _screenManager.Name = "UIRoot";
        AddChild(_screenManager);

        _mainMenu = _screenManager.GetNode<EscapeGame.UI.MainMenu>(R.UI.MainMenu);
        _lobbyMenu = _screenManager.GetNode<EscapeGame.UI.LobbyMenu>(R.UI.LobbyMenu);
        _pauseMenu = _screenManager.GetNode<EscapeGame.UI.PauseMenu>(R.UI.PauseMenu);
        _scoreboard = _screenManager.GetNode<EscapeGame.UI.Scoreboard>(R.UI.Scoreboard);
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

    private void OnHostPressed()
    {
        Error err = NetworkManager.Instance.CreateHost();
        if (err != Error.Ok)
        {
            _mainMenu.SetStatus($"{G.Messages.ServerStartError}: {err}");
            return;
        }

        EnterLobby(_mainMenu.PlayerName);
    }

    private void OnJoinPressed(string address)
    {
        Error err = NetworkManager.Instance.JoinServer(address);
        _mainMenu.SetStatus(
            err != Error.Ok ? $"{G.Messages.JoinError}: {err}" : G.Messages.Joining
        );
    }

    private void OnNetworkConnected()
    {
        EnterLobby(_mainMenu.PlayerName);
    }

    private void EnterLobby(string playerName)
    {
        GameState.SetPhase(GamePhase.Lobby);
        LobbyManager.Instance.SendPlayerName(playerName);
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    private void OnReadyToggled(bool ready)
    {
        LobbyManager.Instance.SendReady(ready);
    }

    private void OnStartRequested()
    {
        if (!LobbyManager.Instance.IsHost)
        {
            return;
        }

        LobbyManager.Instance.StartGame();
    }

    private void OnGameStarted()
    {
        GameState.SetPhase(GamePhase.Gameplay);
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
        _tipLabel.Visible = false;

        // Откладываем перезагрузку: ReturnToMenu часто вызывается из
        // обработчика сигнала (нажатие кнопки / сетевое событие), а
        // уничтожать текущую сцену прямо в нём нельзя.
        GetTree().CallDeferred(SceneTree.MethodName.ReloadCurrentScene);
    }

    public override void _Input(InputEvent @event)
    {
        if (
            GameState.CurrentPhase != GamePhase.Gameplay
            && GameState.CurrentPhase != GamePhase.Paused
        )
        {
            return;
        }

        if (
            @event is InputEventKey keyEvent
            && keyEvent.Pressed
            && !keyEvent.Echo
            && keyEvent.Keycode == G.PauseKey
        )
        {
            TogglePause();
        }
    }

    private void TogglePause()
    {
        if (GameState.CurrentPhase == GamePhase.Paused)
        {
            GameState.SetPhase(GamePhase.Gameplay);
            _tipLabel.Visible = true;
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
        else
        {
            GameState.SetPhase(GamePhase.Paused);
            _tipLabel.Visible = false;
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }
}
