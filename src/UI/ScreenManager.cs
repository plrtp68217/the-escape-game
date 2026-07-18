using Godot;

namespace EscapeGame.UI;

/// <summary>
/// Управляет переключением UI-экранов в зависимости от текущей игровой фазы.
/// </summary>
public partial class ScreenManager : CanvasLayer
{
    public Control MainMenuScreen { get; private set; }
    public Control LobbyScreen { get; private set; }
    public Control PauseScreen { get; private set; }
    public Control ScoreboardScreen { get; private set; }

    public override void _Ready()
    {
        MainMenuScreen = GetNodeOrNull<Control>("MainMenu");
        LobbyScreen = GetNodeOrNull<Control>("LobbyMenu");
        PauseScreen = GetNodeOrNull<Control>("PauseMenu");
        ScoreboardScreen = GetNodeOrNull<Control>("Scoreboard");

        GameState.PhaseChanged += OnPhaseChanged;
        OnPhaseChanged();
    }

    public override void _ExitTree()
    {
        GameState.PhaseChanged -= OnPhaseChanged;
    }

    private void OnPhaseChanged()
    {
        var phase = GameState.CurrentPhase;

        SetScreen(MainMenuScreen, phase == GamePhase.MainMenu);
        SetScreen(LobbyScreen, phase == GamePhase.Lobby);
        SetScreen(PauseScreen, phase == GamePhase.Paused);
        SetScreen(ScoreboardScreen, false);
    }

    private static void SetScreen(Control screen, bool visible)
    {
        if (screen != null)
        {
            screen.Visible = visible;
        }
    }
}
