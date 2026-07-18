using System;

namespace EscapeGame;

/// <summary>
/// Глобальное состояние игрового цикла.
/// </summary>
public static class GameState
{
	public static GamePhase CurrentPhase { get; private set; } = GamePhase.MainMenu;

	public static event Action PhaseChanged;

	public static void SetPhase(GamePhase phase)
	{
		if (CurrentPhase == phase)
		{
			return;
		}

		CurrentPhase = phase;
		PhaseChanged?.Invoke();
	}
}
