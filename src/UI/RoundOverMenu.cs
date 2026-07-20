using Godot;

namespace EscapeGame.UI;

/// <summary>
/// Экран итога раунда: показывает победителя, кнопку перезапуска (только у
/// хоста) и кнопку возврата в меню. Видимостью управляет ScreenManager по фазе
/// RoundOver.
/// </summary>
public partial class RoundOverMenu : Control
{
    private Label _resultLabel;
    private Button _rematchButton;
    private Label _waitLabel;

    public event System.Action LeaveRequested;
    public event System.Action RematchRequested;

    public override void _Ready()
    {
        _resultLabel = GetNode<Label>("ResultLabel");
        _rematchButton = GetNode<Button>("RematchButton");
        _waitLabel = GetNode<Label>("WaitLabel");

        _rematchButton.Pressed += () => RematchRequested?.Invoke();
        GetNode<Button>("LeaveButton").Pressed += () => LeaveRequested?.Invoke();
    }

    public void SetResult(string text)
    {
        _resultLabel.Text = text;
    }

    // Перезапуск доступен только хосту; клиенты видят надпись ожидания.
    public void ConfigureButtons(bool isHost)
    {
        _rematchButton.Visible = isHost;
        _waitLabel.Visible = !isHost;
    }
}
