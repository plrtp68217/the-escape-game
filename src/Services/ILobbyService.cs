using System;
using System.Collections.Generic;
using EscapeGame.Network;

namespace EscapeGame.Services;

/// <summary>
/// Абстракция над лобби. Позволяет компонентам узнавать роли и готовность
/// игроков без зависимости от <see cref="LobbyManager"/u003e.
/// </summary>
public interface ILobbyService
{
	IReadOnlyDictionary<long, LobbyPlayerInfo> Players { get; }
	bool IsHost { get; }
	bool IsGameStarted { get; }

	event Action LobbyUpdated;
	event Action GameStarted;
	event Action JoinRejectedGameInProgress;
}
