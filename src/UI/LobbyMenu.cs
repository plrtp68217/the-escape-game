using System;
using System.Linq;
using Godot;

namespace EscapeGame.UI;

public partial class LobbyMenu : Control
{
    private Label _playerListLabel;
    private Button _readyButton;
    private Button _startButton;
    private Button _leaveButton;
    private Label _statusLabel;

    private bool _isReady;

    public event Action<bool> ReadyToggled;
    public event Action StartRequested;
    public event Action LeaveRequested;

    public override void _Ready()
    {
        _playerListLabel = GetNode<Label>("VBoxContainer/PlayerListLabel");
        _readyButton = GetNode<Button>("VBoxContainer/ReadyButton");
        _startButton = GetNode<Button>("VBoxContainer/StartButton");
        _leaveButton = GetNode<Button>("VBoxContainer/LeaveButton");
        _statusLabel = GetNode<Label>("VBoxContainer/StatusLabel");

        _readyButton.Pressed += OnReadyPressed;
        _startButton.Pressed += OnStartPressed;
        _leaveButton.Pressed += OnLeavePressed;

        GameState.PhaseChanged += OnPhaseChanged;
        LobbyManager.Instance.LobbyUpdated += Refresh;
        LobbyManager.Instance.GameStarted += OnGameStarted;

        OnPhaseChanged();
        Refresh();
    }

    public override void _ExitTree()
    {
        GameState.PhaseChanged -= OnPhaseChanged;

        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.LobbyUpdated -= Refresh;
            LobbyManager.Instance.GameStarted -= OnGameStarted;
        }
    }

    private void OnPhaseChanged()
    {
        if (GameState.CurrentPhase == GamePhase.Lobby)
        {
            Visible = true;
            ResetState();
        }
        else
        {
            Visible = false;
        }
    }

    private void OnReadyPressed()
    {
        _isReady = !_isReady;
        ReadyToggled?.Invoke(_isReady);
        _readyButton.Text = _isReady ? "Не готов" : "Готов";
    }

    private void OnStartPressed()
    {
        StartRequested?.Invoke();
    }

    private void OnGameStarted()
    {
        Visible = false;
    }

    private void OnLeavePressed()
    {
        LeaveRequested?.Invoke();
    }

    private void Refresh()
    {
        var lines = LobbyManager
            .Instance.Players.Values.Select(p =>
                $"{(p.IsReady ? "[Готов] " : "[Ожидание] ")}{p.Name}"
            )
            .ToArray();

        _playerListLabel.Text = lines.Length > 0 ? string.Join("\n", lines) : "Нет игроков";

        if (LobbyManager.Instance.IsHost)
        {
            _startButton.Disabled = false;
            bool canStart = LobbyManager.Instance.CanStartGame();
            _statusLabel.Text = canStart
                ? "Все готовы. Можно начинать."
                : "Хост может начать игру в любой момент";
        }
        else
        {
            _startButton.Visible = false;
            _statusLabel.Text = "";
        }
    }

    public void ResetState()
    {
        _isReady = false;
        _readyButton.Text = "Готов";
        _startButton.Visible = LobbyManager.Instance.IsHost;
        _statusLabel.Text = "";
        Refresh();
    }
}
