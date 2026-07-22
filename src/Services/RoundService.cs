using System;
using EscapeGame.GameFlow;

namespace EscapeGame.Services;

/// <summary>
/// Адаптер <see cref="RoundManager"/u003e под <see cref="IRoundService"/u003e.
/// </summary>
public class RoundService : IRoundService
{
	private readonly RoundManager _manager;

	public RoundService(RoundManager manager)
	{
		_manager = manager;
	}

	public int RemainingSeconds => _manager?.RemainingSeconds ?? 0;
	public bool RoundActive => _manager?.RoundActive ?? false;

	public event Action<RoundResult> RoundEnded
	{
		add => _manager.RoundEnded += value;
		remove => _manager.RoundEnded -= value;
	}

	public void StartRound()
	{
		_manager?.StartRound();
	}

	public void NotifyEscaped(long prisonerId)
	{
		_manager?.NotifyEscaped(prisonerId);
	}

	public void Reset()
	{
		_manager?.Reset();
	}
}
