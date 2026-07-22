using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using EscapeGame.Player;

namespace EscapeGame.Network;

/// <summary>
/// Хранит список игроков, их готовность и инициирует старт игры.
/// Авторитет сервера.
/// </summary>
public partial class LobbyManager : Node
{
	public static LobbyManager Instance { get; private set; }

	public event Action LobbyUpdated;
	public event Action GameStarted;

	/// <summary>
	/// Срабатывает у клиента, если он подключился к серверу,
	/// на котором игра уже идёт. Клиента нужно вернуть в меню.
	/// </summary>
	public event Action JoinRejectedGameInProgress;

	private readonly Dictionary<long, LobbyPlayerInfo> _players = new();

	public IReadOnlyDictionary<long, LobbyPlayerInfo> Players => _players;

	private bool _isGameStarted;

	// Кто был надзирателем в прошлом раунде — чтобы при перезапуске роль по
	// возможности досталась другому игроку.
	private long _lastWardenId;

	public override void _Ready()
	{
		Instance = this;

		Multiplayer.PeerConnected += OnPeerConnected;
		Multiplayer.PeerDisconnected += OnPeerDisconnected;
	}

	public void RegisterHost()
	{
		long hostId = Multiplayer.GetUniqueId();
		var info = new LobbyPlayerInfo
		{
			Id = hostId,
			Name = $"{G.Messages.DefaultPlayerName} {hostId}",
			IsReady = false
		};
		_players[hostId] = info;
		LobbyUpdated?.Invoke();
	}

	public bool IsHost => Multiplayer.MultiplayerPeer != null && Multiplayer.IsServer();

	public bool IsGameStarted => _isGameStarted;

	public bool CanStartGame()
	{
		if (!Multiplayer.IsServer())
		{
			return false;
		}

		return _players.Count > 0 && _players.Values.All(p => p.IsReady);
	}

	private void OnPeerConnected(long id)
	{
		if (!Multiplayer.IsServer())
		{
			return;
		}

		// Поздний игрок: игра уже началась. Сообщаем ему об этом и
		// не регистрируем в лобби.
		if (_isGameStarted)
		{
			RpcId(id, nameof(NotifyGameInProgress));
			return;
		}

		var info = new LobbyPlayerInfo
		{
			Id = id,
			Name = $"{G.Messages.DefaultPlayerName} {id}",
			IsReady = false
		};
		_players[id] = info;

		Rpc(nameof(RegisterPlayer), id, info.Name);
		SyncFullLobbyTo(id);
	}

	private void OnPeerDisconnected(long id)
	{
		if (!Multiplayer.IsServer())
		{
			return;
		}

		_players.Remove(id);
		Rpc(nameof(UnregisterPlayer), id);
	}

	private void SyncFullLobbyTo(long targetId)
	{
		foreach (var player in _players.Values)
		{
			RpcId(targetId, nameof(RegisterPlayer), player.Id, player.Name);
			RpcId(targetId, nameof(SyncPlayerReady), player.Id, player.IsReady);
		}
	}

	[Rpc(
		MultiplayerApi.RpcMode.Authority,
		TransferMode = MultiplayerPeer.TransferModeEnum.Reliable
	)]
	private void RegisterPlayer(long id, string name)
	{
		_players[id] = new LobbyPlayerInfo
		{
			Id = id,
			Name = name,
			IsReady = false
		};
		LobbyUpdated?.Invoke();
	}

	[Rpc(
		MultiplayerApi.RpcMode.Authority,
		TransferMode = MultiplayerPeer.TransferModeEnum.Reliable
	)]
	private void UnregisterPlayer(long id)
	{
		_players.Remove(id);
		LobbyUpdated?.Invoke();
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void SetPlayerName(string name)
	{
		if (!Multiplayer.IsServer())
		{
			return;
		}

		long id = Multiplayer.GetRemoteSenderId();
		if (id == 0)
		{
			id = Multiplayer.GetUniqueId();
		}

		if (string.IsNullOrWhiteSpace(name))
		{
			name = $"{G.Messages.DefaultPlayerName} {id}";
		}

		if (_players.TryGetValue(id, out var info))
		{
			info.Name = name.Trim();
			UpdatePlayerName(id, info.Name);
			Rpc(nameof(UpdatePlayerName), id, info.Name);
		}
	}

	[Rpc(
		MultiplayerApi.RpcMode.Authority,
		TransferMode = MultiplayerPeer.TransferModeEnum.Reliable
	)]
	private void UpdatePlayerName(long id, string name)
	{
		if (_players.TryGetValue(id, out var info))
		{
			info.Name = name;
			LobbyUpdated?.Invoke();
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void SetPlayerReady(bool ready)
	{
		if (!Multiplayer.IsServer())
		{
			return;
		}

		long id = Multiplayer.GetRemoteSenderId();
		if (id == 0)
		{
			id = Multiplayer.GetUniqueId();
		}

		if (_players.TryGetValue(id, out var info))
		{
			info.IsReady = ready;
			SyncPlayerReady(id, ready);
			Rpc(nameof(SyncPlayerReady), id, ready);
		}
	}

	[Rpc(
		MultiplayerApi.RpcMode.Authority,
		TransferMode = MultiplayerPeer.TransferModeEnum.Reliable
	)]
	private void SyncPlayerReady(long id, bool ready)
	{
		if (_players.TryGetValue(id, out var info))
		{
			info.IsReady = ready;
			LobbyUpdated?.Invoke();
		}
	}

	[Rpc(
		MultiplayerApi.RpcMode.Authority,
		TransferMode = MultiplayerPeer.TransferModeEnum.Reliable
	)]
	private void GameStarting()
	{
		GameStarted?.Invoke();
	}

	// Сервер вызывает у позднего клиента, чтобы сообщить, что игра уже идёт.
	[Rpc(
		MultiplayerApi.RpcMode.Authority,
		TransferMode = MultiplayerPeer.TransferModeEnum.Reliable
	)]
	private void NotifyGameInProgress()
	{
		JoinRejectedGameInProgress?.Invoke();
	}

	public void SendPlayerName(string name)
	{
		if (Multiplayer.IsServer())
		{
			SetPlayerName(name);
			return;
		}

		RpcId(1, nameof(SetPlayerName), name);
	}

	public void SendReady(bool ready)
	{
		if (Multiplayer.IsServer())
		{
			SetPlayerReady(ready);
			return;
		}

		RpcId(1, nameof(SetPlayerReady), ready);
	}

	public void StartGame()
	{
		if (!Multiplayer.IsServer() || _isGameStarted)
		{
			return;
		}

		_isGameStarted = true;

		// Сначала раздаём и рассылаем роли (reliable-ordered гарантирует, что
		// они дойдут до клиентов раньше, чем GameStarting), затем запускаем игру.
		AssignRoles();
		BroadcastStart();
	}

	// Перезапуск раунда с теми же игроками (без выхода в лобби). Только сервер:
	// заново раздаёт роли и запускает новый раунд у всех тем же путём, что и
	// первый старт. Сброс мира (двери, инвентарь, здоровье) делает вызывающая
	// сторона до этого вызова.
	public void StartRematch()
	{
		if (!Multiplayer.IsServer())
		{
			return;
		}

		AssignRoles();
		BroadcastStart();
	}

	// Рассылает всем пирам сигнал старта раунда (локально + по сети).
	private void BroadcastStart()
	{
		GameStarting();
		Rpc(nameof(GameStarting));
	}

	// Только сервер. Один игрок становится надзирателем, остальные —
	// заключённые. При наличии более одного игрока прошлый надзиратель по
	// возможности исключается, чтобы роль менялась между раундами.
	private void AssignRoles()
	{
		var ids = _players.Keys.ToList();
		if (ids.Count == 0)
		{
			return;
		}

		var candidates = ids.Count > 1
			? ids.Where(id => id != _lastWardenId).ToList()
			: ids;
		if (candidates.Count == 0)
		{
			candidates = ids;
		}

		long wardenId = candidates[(int)(GD.Randi() % (uint)candidates.Count)];
		_lastWardenId = wardenId;

		foreach (long id in ids)
		{
			PlayerRole role = id == wardenId ? PlayerRole.Warden : PlayerRole.Prisoner;
			SyncPlayerRole(id, (int)role);
			Rpc(nameof(SyncPlayerRole), id, (int)role);
		}
	}

	// Enum передаётся как int — так он гарантированно сериализуется в RPC.
	[Rpc(
		MultiplayerApi.RpcMode.Authority,
		TransferMode = MultiplayerPeer.TransferModeEnum.Reliable
	)]
	private void SyncPlayerRole(long id, int role)
	{
		if (_players.TryGetValue(id, out var info))
		{
			info.Role = (PlayerRole)role;
			LobbyUpdated?.Invoke();
		}
	}

	public void Reset()
	{
		_players.Clear();
		_isGameStarted = false;
		_lastWardenId = 0;
		LobbyUpdated?.Invoke();
	}
}
