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

		RoundManager.Instance.RoundEnded += OnRoundEnded;

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

		// Подсказка, таймер и здоровье показываются только в самой игре.
		bool inGameplay = GameState.CurrentPhase == GamePhase.Gameplay;
		_ui.SetInteractPrompt(inGameplay ? local.GetInteractPrompt() : string.Empty);
		_ui.SetTimer(
			inGameplay && RoundManager.Instance.RoundActive
				? FormatTime(RoundManager.Instance.RemainingSeconds)
				: string.Empty);
		_ui.SetHealth(inGameplay ? FormatHealth(local) : string.Empty);
	}

	private static string FormatHealth(PlayerController player)
	{
		return player.VitalState == PlayerVitalState.Downed
			? "ПОВЕРЖЕН"
			: $"HP: {player.Health}";
	}

	private static string FormatTime(int seconds)
	{
		if (seconds < 0)
		{
			seconds = 0;
		}
		return $"{seconds / 60}:{seconds % 60:00}";
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

		if (RoundManager.Instance != null)
		{
			RoundManager.Instance.RoundEnded -= OnRoundEnded;
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
		RoundManager.Instance.StartRound();
		Combat.CombatRelay.Instance.ResetAll();
		_ui.ShowTip(true);
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	private void OnRoundEnded(RoundResult result)
	{
		GameState.SetPhase(GamePhase.RoundOver);
		_ui.ShowRoundResult(result == RoundResult.PrisonersWin
			? "Заключённые сбежали!"
			: "Надзиратель победил!");
		_ui.ShowTip(false);
		_ui.SetInteractPrompt(string.Empty);
		_ui.SetTimer(string.Empty);
		_ui.SetHealth(string.Empty);
		Input.MouseMode = Input.MouseModeEnum.Visible;
	}

	// Раздаёт роли всем игрокам на этом пире: меняет модель у каждого, а своего
	// локального игрока ставит на точку спавна его роли. Позицию задаёт только
	// authority — MultiplayerSynchronizer разошлёт её остальным.
	private void ApplyRoles()
	{
		// Модели ролей игроки применяют сами (PlayerController.RefreshRoleModel).
		// Здесь только ставим локального игрока на точку спавна его роли и
		// показываем подсказку.
		PlayerController local = PlayerController.LocalPlayer;
		if (local == null
			|| !LobbyManager.Instance.Players.TryGetValue(local.PlayerId, out var localInfo))
		{
			return;
		}

		local.GlobalPosition = ComputeSpawn(local.PlayerId, localInfo.Role);
		_ui.SetTip(localInfo.Role == PlayerRole.Warden ? "Ты — Надзиратель" : "Ты — Заключённый");
	}

	// Надзиратель встаёт на свою точку; каждый заключённый — в отдельную камеру.
	// Индекс считается по отсортированному списку id, поэтому одинаков на всех
	// пирах. Если заключённых больше, чем камер, лишние подселяются со сдвигом.
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

		int cellCount = G.PrisonerCellSpawns.Length;
		Vector3 spawn = G.PrisonerCellSpawns[index % cellCount];

		// Подселение при переполнении камер — небольшой сдвиг, чтобы не спавниться
		// точно друг в друге.
		int overflow = index / cellCount;
		return spawn + new Vector3(overflow * 1.5f, 0f, 0f);
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
		RoundManager.Instance.Reset();

		_pendingStatus = status;
		_inventoryBound = false;
		GameState.SetPhase(GamePhase.MainMenu);
		Input.MouseMode = Input.MouseModeEnum.Visible;
		_ui.ShowTip(false);
		_ui.SetTimer(string.Empty);
		_ui.SetHealth(string.Empty);

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
