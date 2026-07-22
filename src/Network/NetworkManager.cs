using Godot;
using EscapeGame.Logging;
using EscapeGame.Player;

namespace EscapeGame.Network;

// Автозагружаемый (Autoload) синглтон, который отвечает за старт сервера,
// подключение клиентов и спавн игроков. Должен быть добавлен в
// Project Settings -> Autoload под именем "NetworkManager".
public partial class NetworkManager : Node
{
	public static NetworkManager Instance { get; private set; }

	public const int Port = G.Port;
	public const int MaxPlayers = G.MaxPlayers;

	private const string PlayerScenePath = R.PlayerScene;
	private static readonly NodePath PlayersContainerPath = R.PlayersContainer;

	public event System.Action Connected;
	public event System.Action<string> ConnectionError;

	// Последний известный статус соединения — для логирования переходов
	// (Disconnected → Connecting → Connected) в _Process. Так в лог попадает
	// весь ход подключения, включая зависания рукопожатия и таймауты.
	private MultiplayerPeer.ConnectionStatus _lastStatus = MultiplayerPeer.ConnectionStatus.Disconnected;

	public override void _Ready()
	{
		Instance = this;

		Multiplayer.PeerConnected += OnPeerConnected;
		Multiplayer.PeerDisconnected += OnPeerDisconnected;
		Multiplayer.ConnectedToServer += OnConnectedToServer;
		Multiplayer.ConnectionFailed += OnConnectionFailed;
		Multiplayer.ServerDisconnected += OnServerDisconnected;

		Log.Info(Log.Cat.Net, "NetworkManager готов, сигналы мультиплеера подключены");
	}

	// NetworkManager — autoload: его _ExitTree срабатывает только при реальном
	// выходе из приложения (в отличие от Main, который пересоздаётся при каждом
	// возврате в меню через ReloadCurrentScene). Здесь и закрываем файл лога.
	public override void _ExitTree()
	{
		Log.Close();
	}

	// Логируем каждый переход статуса соединения — это ловит всё, что происходит
	// при подключении клиента (ожидание, успех, обрыв), даже без явных сигналов.
	// Учитываем только реальный ENet-пир: по умолчанию Godot держит оффлайн-пир,
	// который всегда рапортует Connected и дал бы ложный переход на старте.
	public override void _Process(double delta)
	{
		MultiplayerPeer.ConnectionStatus status = Multiplayer.MultiplayerPeer is ENetMultiplayerPeer enet
			? enet.GetConnectionStatus()
			: MultiplayerPeer.ConnectionStatus.Disconnected;

		if (status != _lastStatus)
		{
			Log.Info(Log.Cat.Net, $"Статус соединения: {_lastStatus} → {status}");
			_lastStatus = status;
		}
	}

	// Поднять сервер и сразу заспавнить самого хоста как игрока.
	public Error CreateHost()
	{
		Log.Info(Log.Cat.Net, $"CreateHost: поднимаем сервер на порту {G.Port} (макс. игроков {G.MaxPlayers})");

		var peer = new ENetMultiplayerPeer();
		Error err = peer.CreateServer(G.Port, G.MaxPlayers);
		if (err != Error.Ok)
		{
			Log.Error(Log.Cat.Net, $"CreateHost: не удалось создать сервер (порт {G.Port}): {err}");
			return err;
		}

		Multiplayer.MultiplayerPeer = peer;
		int hostId = Multiplayer.GetUniqueId();
		Log.Info(Log.Cat.Net, $"CreateHost: сервер запущен, id хоста={hostId}");

		LobbyManager.Instance.RegisterHost();
		SpawnPlayer(hostId);
		Connected?.Invoke();
		return Error.Ok;
	}

	// Подключиться клиентом к серверу по IP.
	public Error JoinServer(string address)
	{
		Log.Info(Log.Cat.Net, $"JoinServer: попытка подключения к {address}:{G.Port}");

		var peer = new ENetMultiplayerPeer();
		Error err = peer.CreateClient(address, G.Port);
		if (err != Error.Ok)
		{
			// Синхронная ошибка: неверный адрес, занятый порт, нет сокета и т.п.
			Log.Error(Log.Cat.Net, $"JoinServer: не удалось создать клиента для {address}:{G.Port}: {err}");
			return err;
		}

		Multiplayer.MultiplayerPeer = peer;
		Log.Info(Log.Cat.Net, $"JoinServer: клиент создан, ждём установки соединения с {address}:{G.Port}");
		return Error.Ok;
	}

	// Разорвать текущее сетевое соединение (и как хост, и как клиент)
	// и вернуться в оффлайн-состояние.
	public void Disconnect()
	{
		if (Multiplayer.MultiplayerPeer != null)
		{
			Log.Info(Log.Cat.Net, $"Disconnect: закрываем соединение (был статус {_lastStatus})");
			Multiplayer.MultiplayerPeer.Close();
			Multiplayer.MultiplayerPeer = null;
			_lastStatus = MultiplayerPeer.ConnectionStatus.Disconnected;
		}
	}

	// Срабатывает у сервера, когда к нему подключается новый клиент.
	private void OnPeerConnected(long id)
	{
		Log.Info(Log.Cat.Net, $"PeerConnected: пир {id} (я сервер={Multiplayer.IsServer()})");

		if (!Multiplayer.IsServer())
		{
			return;
		}

		// Игра уже идёт — не спавним позднего игрока. LobbyManager сам
		// отправит ему уведомление, что игра уже началась.
		if (LobbyManager.Instance != null && LobbyManager.Instance.IsGameStarted)
		{
			Log.Warning(Log.Cat.Net, $"PeerConnected: пир {id} отклонён — игра уже идёт (поздний джойн)");
			return;
		}

		SpawnPlayer((int)id);
	}

	// Срабатывает у всех, когда кто-то отключился. Удаляем узел
	// игрока только на сервере — MultiplayerSpawner сам разошлёт
	// удаление остальным клиентам.
	private void OnPeerDisconnected(long id)
	{
		Log.Info(Log.Cat.Net, $"PeerDisconnected: пир {id} (я сервер={Multiplayer.IsServer()})");

		if (!Multiplayer.IsServer())
		{
			return;
		}

		Node playerNode = GetTree().Root.GetNodeOrNull($"{PlayersContainerPath}/{id}");
		if (playerNode != null)
		{
			Log.Debug(Log.Cat.Net, $"PeerDisconnected: удаляем узел игрока {id}");
			playerNode.QueueFree();
		}
		else
		{
			Log.Warning(Log.Cat.Net, $"PeerDisconnected: узел игрока {id} не найден");
		}
	}

	// У клиента: сработает, когда реально установилось соединение с сервером.
	private void OnConnectedToServer()
	{
		Log.Info(Log.Cat.Net, $"ConnectedToServer: соединение установлено, мой id={Multiplayer.GetUniqueId()}");
		Connected?.Invoke();
	}

	private void OnConnectionFailed()
	{
		Log.Error(Log.Cat.Net, "ConnectionFailed: не удалось подключиться к серверу (нет ответа/таймаут/отказ)");
		ConnectionError?.Invoke(G.Messages.ConnectionFailed);
	}

	private void OnServerDisconnected()
	{
		Log.Warning(Log.Cat.Net, "ServerDisconnected: сервер разорвал соединение");
		ConnectionError?.Invoke(G.Messages.ServerDisconnected);
	}

	// Вызывается ТОЛЬКО на сервере. Инстанцирование под MultiplayerSpawner
	// автоматически реплицируется на всех клиентов.
	private void SpawnPlayer(int id)
	{
		Log.Info(Log.Cat.Net, $"SpawnPlayer: спавним игрока {id}");

		var scene = GD.Load<PackedScene>(PlayerScenePath);
		var player = scene.Instantiate<PlayerController>();

		player.Name = id.ToString();
		player.Position = G.SpawnPosition;
		player.PlayerId = id; // authority выводится из Name в _EnterTree (см. PlayerController)

		var playersNode = GetTree().Root.GetNode(PlayersContainerPath);
		playersNode.AddChild(player, true);
	}
}
