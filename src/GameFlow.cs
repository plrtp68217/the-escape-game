using System.Linq;
using Godot;

namespace EscapeGame;

/// <summary>
/// Управляет жизненным циклом игровой сессии: меню, лобби, игра, пауза,
/// возврат в меню. Не занимается сетью напрямую — только координирует
/// фазы и вспомогательные вещи вроде режима мыши и подсказок.
/// </summary>
public partial class GameFlow : Node
{
	private UI.UIManager _ui;
	private bool _initialized;
	private bool _inventoryOpen;
	private bool _inventoryBound;

	// Сообщение, которое нужно показать в меню после перезагрузки сцены.
	// Статическое, т.к. переживает пересоздание узла GameFlow.
	private static string _pendingStatus = string.Empty;

	public void Initialize(UI.UIManager ui)
	{
		_ui = ui;
		Inventory.ItemDatabase.RegisterDefaults();

		_ui.HostRequested += StartHost;
		_ui.JoinRequested += JoinServer;
		_ui.LeaveRequested += () => ReturnToMenu();
		_ui.ReadyToggled += OnReadyToggled;
		_ui.StartRequested += OnStartRequested;
		_ui.InventorySlotSelected += OnInventorySlotSelected;

		NetworkManager.Instance.Connected += OnNetworkConnected;
		NetworkManager.Instance.ConnectionError += OnNetworkError;

		LobbyManager.Instance.GameStarted += OnGameStarted;
		LobbyManager.Instance.JoinRejectedGameInProgress += OnJoinRejected;

		Input.MouseMode = Input.MouseModeEnum.Visible;
		GameState.SetPhase(GamePhase.MainMenu);

		if (!string.IsNullOrEmpty(_pendingStatus))
		{
			_ui.SetStatus(_pendingStatus);
			_pendingStatus = string.Empty;
		}

		_initialized = true;
	}

	public override void _Ready()
	{
		if (!_initialized)
		{
			GD.PushError("GameFlow must be initialized by Main before _Ready.");
		}
	}

	public override void _Process(double delta)
	{
		PlayerController local = PlayerController.LocalPlayer;
		if (local == null)
		{
			return;
		}

		if (!_inventoryBound)
		{
			_ui.Inventory.Bind(local.Inventory);
			local.InventoryChanged += () => _ui.Inventory.Refresh();
			_inventoryBound = true;
		}

		// Подсказка взаимодействия показывается только в самой игре.
		_ui.SetInteractPrompt(
			GameState.CurrentPhase == GamePhase.Gameplay ? local.GetInteractPrompt() : string.Empty);
	}

	public override void _ExitTree()
	{
		if (_ui != null)
		{
			_ui.HostRequested -= StartHost;
			_ui.JoinRequested -= JoinServer;
			_ui.LeaveRequested -= () => ReturnToMenu();
			_ui.ReadyToggled -= OnReadyToggled;
			_ui.StartRequested -= OnStartRequested;
			_ui.InventorySlotSelected -= OnInventorySlotSelected;
		}

		if (NetworkManager.Instance != null)
		{
			NetworkManager.Instance.Connected -= OnNetworkConnected;
			NetworkManager.Instance.ConnectionError -= OnNetworkError;
		}

		if (LobbyManager.Instance != null)
		{
			LobbyManager.Instance.GameStarted -= OnGameStarted;
			LobbyManager.Instance.JoinRejectedGameInProgress -= OnJoinRejected;
		}
	}

	private void StartHost()
	{
		Error err = NetworkManager.Instance.CreateHost();
		if (err != Error.Ok)
		{
			_ui.SetStatus($"{G.Messages.ServerStartError}: {err}");
			return;
		}

		EnterLobby(_ui.PlayerName);
	}

	private void JoinServer(string address)
	{
		Error err = NetworkManager.Instance.JoinServer(address);
		_ui.SetStatus(err != Error.Ok ? $"{G.Messages.JoinError}: {err}" : G.Messages.Joining);
	}

	private void OnNetworkConnected()
	{
		EnterLobby(_ui.PlayerName);
	}

	private void EnterLobby(string playerName)
	{
		GameState.SetPhase(GamePhase.Lobby);
		LobbyManager.Instance.SendPlayerName(playerName);
		Input.MouseMode = Input.MouseModeEnum.Visible;
	}

	private void OnReadyToggled(bool ready)
	{
		LobbyManager.Instance.SendReady(ready);
	}

	private void OnStartRequested()
	{
		if (!LobbyManager.Instance.IsHost)
		{
			return;
		}

		LobbyManager.Instance.StartGame();
	}

	private void OnGameStarted()
	{
		GameState.SetPhase(GamePhase.Gameplay);
		ApplyRoles();
		_ui.ShowTip(true);
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	// Раздаёт роли всем игрокам на этом пире: меняет модель у каждого, а своего
	// локального игрока ставит на точку спавна его роли. Позицию задаёт только
	// authority — MultiplayerSynchronizer разошлёт её остальным.
	private void ApplyRoles()
	{
		foreach (PlayerController player in PlayerController.AllPlayers.Values)
		{
			if (LobbyManager.Instance.Players.TryGetValue(player.PlayerId, out var info))
			{
				player.ApplyRole(info.Role);
			}
		}

		PlayerController local = PlayerController.LocalPlayer;
		if (local == null
			|| !LobbyManager.Instance.Players.TryGetValue(local.PlayerId, out var localInfo))
		{
			return;
		}

		local.GlobalPosition = ComputeSpawn(local.PlayerId, localInfo.Role);
		_ui.SetTip(localInfo.Role == PlayerRole.Warden ? "Ты — Надзиратель" : "Ты — Заключённый");
	}

	// Надзиратель встаёт на свою точку, заключённые выстраиваются в ряд по X
	// вокруг общей точки. Индекс считается по отсортированному списку id, поэтому
	// одинаков на всех пирах.
	private static Vector3 ComputeSpawn(int playerId, PlayerRole role)
	{
		if (role == PlayerRole.Warden)
		{
			return G.WardenSpawn;
		}

		var prisonerIds = LobbyManager.Instance.Players
			.Where(pair => pair.Value.Role == PlayerRole.Prisoner)
			.Select(pair => pair.Key)
			.OrderBy(id => id)
			.ToList();

		int index = prisonerIds.IndexOf(playerId);
		if (index < 0)
		{
			index = 0;
		}

		float offset = (index - (prisonerIds.Count - 1) / 2f) * G.PrisonerSpawnSpacing;
		return G.PrisonerSpawn + new Vector3(offset, 0f, 0f);
	}

	private void OnNetworkError(string reason)
	{
		ReturnToMenu(reason);
	}

	private void OnJoinRejected()
	{
		ReturnToMenu(G.Messages.GameInProgress);
	}

	public void ReturnToMenu(string status = "")
	{
		NetworkManager.Instance.Disconnect();
		LobbyManager.Instance.Reset();

		_pendingStatus = status;
		_inventoryBound = false;
		GameState.SetPhase(GamePhase.MainMenu);
		Input.MouseMode = Input.MouseModeEnum.Visible;
		_ui.ShowTip(false);

		GetTree().CallDeferred(SceneTree.MethodName.ReloadCurrentScene);
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
		{
			return;
		}

		if (keyEvent.Keycode == G.PauseKey)
		{
			if (
				GameState.CurrentPhase == GamePhase.Gameplay
				|| GameState.CurrentPhase == GamePhase.Paused
			)
			{
				TogglePause();
			}
			return;
		}

		if (keyEvent.Keycode == Key.E)
		{
			if (
				GameState.CurrentPhase == GamePhase.Gameplay
				|| GameState.CurrentPhase == GamePhase.Inventory
			)
			{
				ToggleInventory();
			}
			return;
		}
	}

	private void ToggleInventory()
	{
		if (_inventoryOpen)
		{
			_ui.Inventory.Close();
			_inventoryOpen = false;
			GameState.SetPhase(GamePhase.Gameplay);
			Input.MouseMode = Input.MouseModeEnum.Captured;
			_ui.ShowTip(true);
		}
		else
		{
			_ui.Inventory.Open();
			_inventoryOpen = true;
			GameState.SetPhase(GamePhase.Inventory);
			Input.MouseMode = Input.MouseModeEnum.Visible;
			_ui.ShowTip(false);
		}
	}

	private void OnInventorySlotSelected(int slotIndex)
	{
		if (PlayerController.LocalPlayer == null)
		{
			return;
		}

		Inventory.InventoryRelay.Instance?.RpcId(1, nameof(Inventory.InventoryRelay.RequestEquip),
			(long)PlayerController.LocalPlayer.PlayerId, slotIndex);
	}

	private void TogglePause()
	{
		if (GameState.CurrentPhase == GamePhase.Paused)
		{
			GameState.SetPhase(GamePhase.Gameplay);
			_ui.ShowTip(true);
			Input.MouseMode = Input.MouseModeEnum.Captured;
		}
		else
		{
			GameState.SetPhase(GamePhase.Paused);
			_ui.ShowTip(false);
			Input.MouseMode = Input.MouseModeEnum.Visible;
		}
	}
}
