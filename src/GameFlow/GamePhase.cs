namespace EscapeGame.GameFlow;

/// <summary>
/// Текущая фаза игрового цикла.
/// </summary>
public enum GamePhase
{
	MainMenu,
	Lobby,
	Gameplay,
	Paused,
	Inventory,
	RoundOver
}
