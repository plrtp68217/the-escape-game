using Godot;

namespace EscapeGame.UI;

/// <summary>
/// Создаёт UI-экраны и публикует события от них. Не знает про сеть или
/// игровой цикл — только связывает интерфейс с внешними слушателями.
/// </summary>
public partial class UIManager : Node
{
    private const string UIRootScenePath = "res://scenes/ui/ui_root.tscn";

    private ScreenManager _screenManager;
    private MainMenu _mainMenu;
    private LobbyMenu _lobbyMenu;
    private PauseMenu _pauseMenu;
    private Scoreboard _scoreboard;
    private Label _tipLabel;

    public event System.Action HostRequested;
    public event System.Action<string> JoinRequested;
    public event System.Action<bool> ReadyToggled;
    public event System.Action StartRequested;
    public event System.Action LeaveRequested;

    public string PlayerName => _mainMenu.PlayerName;

    public override void _Ready()
    {
        InstantiateUI();

        _mainMenu.HostRequested += () => HostRequested?.Invoke();
        _mainMenu.JoinRequested += address => JoinRequested?.Invoke(address);

        _lobbyMenu.LeaveRequested += () => LeaveRequested?.Invoke();
        _lobbyMenu.ReadyToggled += ready => ReadyToggled?.Invoke(ready);
        _lobbyMenu.StartRequested += () => StartRequested?.Invoke();

        _pauseMenu.LeaveRequested += () => LeaveRequested?.Invoke();

        _scoreboard.SetVisibilitySource(() => GameState.CurrentPhase == GamePhase.Gameplay);
    }

    public override void _ExitTree()
    {
        if (_mainMenu != null)
        {
            _mainMenu.HostRequested -= () => HostRequested?.Invoke();
            _mainMenu.JoinRequested -= address => JoinRequested?.Invoke(address);
        }

        if (_lobbyMenu != null)
        {
            _lobbyMenu.LeaveRequested -= () => LeaveRequested?.Invoke();
            _lobbyMenu.ReadyToggled -= ready => ReadyToggled?.Invoke(ready);
            _lobbyMenu.StartRequested -= () => StartRequested?.Invoke();
        }

        if (_pauseMenu != null)
        {
            _pauseMenu.LeaveRequested -= () => LeaveRequested?.Invoke();
        }
    }

    public void SetStatus(string text)
    {
        _mainMenu.SetStatus(text);
    }

    public void ShowTip(bool visible)
    {
        _tipLabel.Visible = visible;
    }

    private void InstantiateUI()
    {
        var scene = GD.Load<PackedScene>(UIRootScenePath);
        _screenManager = scene.Instantiate<ScreenManager>();
        _screenManager.Name = "UIRoot";
        AddChild(_screenManager);

        _mainMenu = _screenManager.GetNode<MainMenu>("MainMenu");
        _lobbyMenu = _screenManager.GetNode<LobbyMenu>("LobbyMenu");
        _pauseMenu = _screenManager.GetNode<PauseMenu>("PauseMenu");
        _scoreboard = _screenManager.GetNode<Scoreboard>("Scoreboard");
        _tipLabel = _screenManager.GetNode<Label>("TipLabel");
    }
}
