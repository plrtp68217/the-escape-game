using Godot;

namespace EscapeGame.UI;

/// <summary>
/// Меню паузы, показываемое по нажатию Escape во время игры.
/// </summary>
public partial class PauseMenu : Control
{
    public event System.Action LeaveRequested;
    public event System.Action SettingsRequested;

    public override void _Ready()
    {
        GameState.PhaseChanged += OnPhaseChanged;
        OnPhaseChanged();

        GetNode<Button>("LeaveButton").Pressed += () => LeaveRequested?.Invoke();
        GetNode<Button>("SettingsButton").Pressed += () => SettingsRequested?.Invoke();
    }

    public override void _ExitTree()
    {
        GameState.PhaseChanged -= OnPhaseChanged;
    }

    private void OnPhaseChanged()
    {
        Visible = GameState.CurrentPhase == GamePhase.Paused;
    }
}
