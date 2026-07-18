using Godot;

namespace EscapeGame.UI;

/// <summary>
/// Управляет переключением UI-экранов в зависимости от текущей игровой фазы.
/// </summary>
public partial class ScreenManager : CanvasLayer
{
	private Control _mainMenuScreen;
	private Control _lobbyScreen;
	private Control _pauseScreen;
	private Control _scoreboardScreen;
	private Control _inventoryScreen;

	public override void _Ready()
	{
		_mainMenuScreen = GetNodeOrNull<Control>("MainMenu");
		_lobbyScreen = GetNodeOrNull<Control>("LobbyMenu");
		_pauseScreen = GetNodeOrNull<Control>("PauseMenu");
		_scoreboardScreen = GetNodeOrNull<Control>("Scoreboard");
		_inventoryScreen = GetNodeOrNull<Control>("Inventory");

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

		SetScreen(_mainMenuScreen, phase == GamePhase.MainMenu);
		SetScreen(_lobbyScreen, phase == GamePhase.Lobby);
		SetScreen(_pauseScreen, phase == GamePhase.Paused);
		SetScreen(_inventoryScreen, phase == GamePhase.Inventory);
		SetScreen(_scoreboardScreen, false);
	}

	private static void SetScreen(Control screen, bool visible)
	{
		if (screen != null)
		{
			screen.Visible = visible;
		}
	}
}
