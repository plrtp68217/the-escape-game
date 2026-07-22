using System.Collections.Generic;
using System.Linq;
using EscapeGame.Player;

namespace EscapeGame.Services;

/// <summary>
/// Реестр игроков. Заменяет статический словарь в <see cref="PlayerController"/u003e.
/// </summary>
public class PlayerRegistry : IPlayerRegistry
{
	private readonly Dictionary<long, PlayerController> _players = new();

	public PlayerController Local { get; private set; }
	public IEnumerable<PlayerController> All => _players.Values;

	public PlayerController Get(long id)
	{
		_players.TryGetValue(id, out PlayerController player);
		return player;
	}

	public void Register(PlayerController player)
	{
		if (player == null)
		{
			return;
		}

		_players[player.PlayerId] = player;

		if (player.IsMultiplayerAuthority())
		{
			Local = player;
		}
	}

	public void Unregister(long id)
	{
		_players.Remove(id);

		if (Local != null && Local.PlayerId == id)
		{
			Local = null;
		}
	}
}
