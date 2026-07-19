using Godot;

namespace EscapeGame.UI;

/// <summary>
/// Экран итога раунда: показывает победителя и кнопку возврата в меню.
/// Видимостью управляет ScreenManager по фазе RoundOver.
/// </summary>
public partial class RoundOverMenu : Control
{
    private Label _resultLabel;

    public event System.Action LeaveRequested;

    public override void _Ready()
    {
        _resultLabel = GetNode<Label>("ResultLabel");
        GetNode<Button>("LeaveButton").Pressed += () => LeaveRequested?.Invoke();
    }

    public void SetResult(string text)
    {
        _resultLabel.Text = text;
    }
}
