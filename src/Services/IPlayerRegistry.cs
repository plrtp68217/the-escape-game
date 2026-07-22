using System.Collections.Generic;
using EscapeGame.Player;

namespace EscapeGame.Services;

/// <summary>
/// Реестр живых игроков в текущей сессии. Заменяет статический
/// <see cref="PlayerController.AllPlayers"/u003e.
/// </summary>
public interface IPlayerRegistry
{
	PlayerController Local { get; }
	IEnumerable<PlayerController> All { get; }
	PlayerController Get(long id);
	void Register(PlayerController player);
	void Unregister(long id);
}
