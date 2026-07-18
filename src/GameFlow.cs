using Godot;

namespace EscapeGame;

/// <summary>
/// Управляет жизненным циклом игровой сессии: меню, лобби, игра, пауза,
/// возврат в меню. Не занимается сетью напрямую — только координирует
/// фазы и вспомогательные вещи вроде режима мыши и подсказок.
/// </summary>
public partial class GameFlow : Node
{
    private UI.UIManager _ui;
    private bool _initialized;

    // Сообщение, которое нужно показать в меню после перезагрузки сцены.
    // Статическое, т.к. переживает пересоздание узла GameFlow.
    private static string _pendingStatus = string.Empty;

    public void Initialize(UI.UIManager ui)
    {
        _ui = ui;

        _ui.HostRequested += StartHost;
        _ui.JoinRequested += JoinServer;
        _ui.LeaveRequested += () => ReturnToMenu();
        _ui.ReadyToggled += OnReadyToggled;
        _ui.StartRequested += OnStartRequested;

        NetworkManager.Instance.Connected += OnNetworkConnected;
        NetworkManager.Instance.ConnectionError += OnNetworkError;

        LobbyManager.Instance.GameStarted += OnGameStarted;
        LobbyManager.Instance.JoinRejectedGameInProgress += OnJoinRejected;

        Input.MouseMode = Input.MouseModeEnum.Visible;
        GameState.SetPhase(GamePhase.MainMenu);

        if (!string.IsNullOrEmpty(_pendingStatus))
        {
            _ui.SetStatus(_pendingStatus);
            _pendingStatus = string.Empty;
        }

        _initialized = true;
    }

    public override void _Ready()
    {
        if (!_initialized)
        {
            GD.PushError("GameFlow must be initialized by Main before _Ready.");
        }
    }

    public override void _ExitTree()
    {
        if (_ui != null)
        {
            _ui.HostRequested -= StartHost;
            _ui.JoinRequested -= JoinServer;
            _ui.LeaveRequested -= () => ReturnToMenu();
            _ui.ReadyToggled -= OnReadyToggled;
            _ui.StartRequested -= OnStartRequested;
        }

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

    private void StartHost()
    {
        Error err = NetworkManager.Instance.CreateHost();
        if (err != Error.Ok)
        {
            _ui.SetStatus($"{G.Messages.ServerStartError}: {err}");
            return;
        }

        EnterLobby(_ui.PlayerName);
    }

    private void JoinServer(string address)
    {
        Error err = NetworkManager.Instance.JoinServer(address);
        _ui.SetStatus(err != Error.Ok ? $"{G.Messages.JoinError}: {err}" : G.Messages.Joining);
    }

    private void OnNetworkConnected()
    {
        EnterLobby(_ui.PlayerName);
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
        _ui.ShowTip(true);
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    private void OnNetworkError(string reason)
    {
        ReturnToMenu(reason);
    }

    private void OnJoinRejected()
    {
        ReturnToMenu(G.Messages.GameInProgress);
    }

    public void ReturnToMenu(string status = "")
    {
        NetworkManager.Instance.Disconnect();
        LobbyManager.Instance.Reset();

        _pendingStatus = status;
        GameState.SetPhase(GamePhase.MainMenu);
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _ui.ShowTip(false);

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
            _ui.ShowTip(true);
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
        else
        {
            GameState.SetPhase(GamePhase.Paused);
            _ui.ShowTip(false);
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }
}
