using System;
using System.Collections.Generic;
using EscapeGame.Network;

namespace EscapeGame.Services;

/// <summary>
/// Адаптер <see cref="LobbyManager"/u003e под <see cref="ILobbyService"/u003e.
/// </summary>
public class LobbyService : ILobbyService
{
	private readonly LobbyManager _manager;

	public LobbyService(LobbyManager manager)
	{
		_manager = manager;
	}

	public IReadOnlyDictionary<long, LobbyPlayerInfo> Players => _manager?.Players;
	public bool IsHost => _manager?.IsHost ?? false;
	public bool IsGameStarted => _manager?.IsGameStarted ?? false;

	public event Action LobbyUpdated
	{
		add => _manager.LobbyUpdated += value;
		remove => _manager.LobbyUpdated -= value;
	}

	public event Action GameStarted
	{
		add => _manager.GameStarted += value;
		remove => _manager.GameStarted -= value;
	}

	public event Action JoinRejectedGameInProgress
	{
		add => _manager.JoinRejectedGameInProgress += value;
		remove => _manager.JoinRejectedGameInProgress -= value;
	}
}
