using System;
using EscapeGame.GameFlow;

namespace EscapeGame.Services;

/// <summary>
/// Абстракция над менеджером раунда.
/// </summary>
public interface IRoundService
{
	int RemainingSeconds { get; }
	bool RoundActive { get; }

	event Action<RoundResult> RoundEnded;

	void StartRound();
	void NotifyEscaped(long prisonerId);
	void Reset();
}
