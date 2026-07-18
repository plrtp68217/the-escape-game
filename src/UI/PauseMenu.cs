using Godot;

namespace EscapeGame.UI;

/// <summary>
/// Меню паузы, показываемое по нажатию Escape во время игры.
/// </summary>
public partial class PauseMenu : Control
{
    public event System.Action LeaveRequested;

    public override void _Ready()
    {
        GameState.PhaseChanged += OnPhaseChanged;
        OnPhaseChanged();

        GetNode<Button>("LeaveButton").Pressed += () => LeaveRequested?.Invoke();
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
