using Godot;

namespace EscapeGame;

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

	public override void _Ready()
	{
		Instance = this;

		Multiplayer.PeerConnected += OnPeerConnected;
		Multiplayer.PeerDisconnected += OnPeerDisconnected;
		Multiplayer.ConnectedToServer += OnConnectedToServer;
		Multiplayer.ConnectionFailed += OnConnectionFailed;
		Multiplayer.ServerDisconnected += OnServerDisconnected;
	}

	// Поднять сервер и сразу заспавнить самого хоста как игрока.
	public Error CreateHost()
	{
		var peer = new ENetMultiplayerPeer();
		Error err = peer.CreateServer(G.Port, G.MaxPlayers);
		if (err != Error.Ok)
		{
			GD.PrintErr("Не удалось создать сервер: ", err);
			return err;
		}

		Multiplayer.MultiplayerPeer = peer;
		GD.Print("Сервер запущен на порту ", G.Port);

		SpawnPlayer(Multiplayer.GetUniqueId());
		Connected?.Invoke();
		return Error.Ok;
	}

	// Подключиться клиентом к серверу по IP.
	public Error JoinServer(string address)
	{
		var peer = new ENetMultiplayerPeer();
		Error err = peer.CreateClient(address, G.Port);
		if (err != Error.Ok)
		{
			GD.PrintErr("Не удалось подключиться: ", err);
			return err;
		}

		Multiplayer.MultiplayerPeer = peer;
		return Error.Ok;
	}

	// Срабатывает у сервера, когда к нему подключается новый клиент.
	private void OnPeerConnected(long id)
	{
		if (Multiplayer.IsServer())
		{
			SpawnPlayer((int)id);
		}
	}

	// Срабатывает у всех, когда кто-то отключился. Удаляем узел
	// игрока только на сервере — MultiplayerSpawner сам разошлёт
	// удаление остальным клиентам.
	private void OnPeerDisconnected(long id)
	{
		if (!Multiplayer.IsServer())
		{
			return;
		}

		GetTree().Root.GetNodeOrNull($"{PlayersContainerPath}/{id}")?.QueueFree();
	}

	// У клиента: сработает, когда реально установилось соединение с сервером.
	private void OnConnectedToServer()
	{
		Connected?.Invoke();
	}

	private void OnConnectionFailed()
	{
		ConnectionError?.Invoke(G.Messages.ConnectionFailed);
	}

	private void OnServerDisconnected()
	{
		ConnectionError?.Invoke(G.Messages.ServerDisconnected);
	}

	// Вызывается ТОЛЬКО на сервере. Инстанцирование под MultiplayerSpawner
	// автоматически реплицируется на всех клиентов.
	private void SpawnPlayer(int id)
	{
		var scene = GD.Load<PackedScene>(PlayerScenePath);
		var player = scene.Instantiate<Player>();

		player.Name = id.ToString();
		player.Position = G.SpawnPosition;
		player.PlayerId = id; // назначит multiplayer authority (см. Player.cs)

		var playersNode = GetTree().Root.GetNode(PlayersContainerPath);
		playersNode.AddChild(player, true);
	}
}