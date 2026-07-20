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

	// Поколение обучающей подсказки: отложенный сброс срабатывает, только если
	// с момента его планирования не начался новый раунд (иначе стал бы затирать
	// подсказку следующего раунда неверной ролью).
	private int _tipGeneration;

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
		_ui.RematchRequested += OnRematchRequested;
		_ui.InventorySlotSelected += OnInventorySlotSelected;
		_ui.InventorySlotDropRequested += OnInventorySlotDropRequested;

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
			_ui.Hotbar.Bind(local.Inventory);
			local.InventoryChanged += () =>
			{
				_ui.Inventory.Refresh();
				_ui.Hotbar.Refresh();
			};

			// Визуальный фидбек боя: вспышки урона/лечения и хитмаркер.
			local.LocalDamaged += _ui.FlashDamage;
			local.LocalHealed += _ui.FlashHeal;
			local.LocalHitConfirmed += _ui.ShowHitMarker;

			_inventoryBound = true;
		}

		// Подсказка, таймер, здоровье и хотбар показываются только в самой игре.
		bool inGameplay = GameState.CurrentPhase == GamePhase.Gameplay;
		_ui.ShowHotbar(inGameplay);

		// Рядом поверженный союзник — подсказка «удерживайте F» и прогресс-бар
		// подъёма; иначе — обычная подсказка взаимодействия.
		if (inGameplay && local.HasReviveTarget)
		{
			_ui.SetInteractPrompt("Удерживайте [F], чтобы поднять");
			_ui.SetReviveProgress(local.ReviveProgress);
		}
		else if (inGameplay && local.IsSelfHealing)
		{
			_ui.SetInteractPrompt("Лечение…");
			_ui.SetReviveProgress(local.SelfHealProgress);
		}
		else
		{
			_ui.SetInteractPrompt(inGameplay ? local.GetInteractPrompt() : string.Empty);
			_ui.SetReviveProgress(-1f);
		}

		// Собственный нокаут: затемнение экрана и надпись.
		_ui.ShowKnockout(inGameplay && local.VitalState == PlayerVitalState.Downed);

		_ui.SetTimer(
			inGameplay && RoundManager.Instance.RoundActive
				? FormatTime(RoundManager.Instance.RemainingSeconds)
				: string.Empty);
		_ui.SetHealth(inGameplay ? FormatHealth(local) : string.Empty);
		_ui.SetStamina(inGameplay ? FormatStamina(local) : string.Empty);
		_ui.SetAbility(inGameplay ? FormatAbility(local) : string.Empty);
	}

	// Полоска выносливости (10 сегментов, ASCII — гарантированно в любом шрифте).
	private static string FormatStamina(PlayerController player)
	{
		int filled = Mathf.Clamp(Mathf.RoundToInt(player.StaminaRatio * 10f), 0, 10);
		return "Бег [" + new string('=', filled) + new string('-', 10 - filled) + "]";
	}

	// Статус скана — только у надзирателя.
	private static string FormatAbility(PlayerController player)
	{
		if (player.Role != PlayerRole.Warden)
		{
			return string.Empty;
		}

		if (player.ScanActive)
		{
			return "Скан активен";
		}

		return player.ScanReady ? "Скан [R]: готов" : $"Скан: {player.ScanCooldownSeconds}с";
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
			_ui.RematchRequested -= OnRematchRequested;
			_ui.InventorySlotSelected -= OnInventorySlotSelected;
			_ui.InventorySlotDropRequested -= OnInventorySlotDropRequested;
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
		// Перезапуск раунда инициирует только хост; остальные ждут его решения.
		_ui.ConfigureRoundOver(LobbyManager.Instance.IsHost);
		_ui.ShowTip(false);
		_ui.SetInteractPrompt(string.Empty);
		_ui.SetTimer(string.Empty);
		_ui.SetHealth(string.Empty);
		Input.MouseMode = Input.MouseModeEnum.Visible;
	}

	// Хост запускает новый раунд с теми же игроками. Сначала возвращаем мир в
	// исходное состояние на сервере (двери заперты, инвентари восстановлены), а
	// затем LobbyManager переназначает роли и запускает раунд у всех тем же
	// путём, что и первый старт (OnGameStarted). Здоровье и таймер сбрасываются
	// внутри OnGameStarted (CombatRelay.ResetAll + RoundManager.StartRound).
	private void OnRematchRequested()
	{
		if (!LobbyManager.Instance.IsHost)
		{
			return;
		}

		foreach (Interaction.CellDoor door in Interaction.CellDoor.All)
		{
			door.ResetState();
		}

		foreach (Interaction.Barrier barrier in Interaction.Barrier.All)
		{
			barrier.ResetState();
		}

		foreach (PlayerController player in PlayerController.AllPlayers.Values)
		{
			player.ResetForRound();
		}

		LobbyManager.Instance.StartRematch();
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
		// Гасим остаточную скорость, чтобы после телепорта на спавн игрока не несло.
		local.Velocity = Vector3.Zero;
		local.ResetAbilities();

		bool isWarden = localInfo.Role == PlayerRole.Warden;
		_ui.SetTip(isWarden ? G.Messages.WardenHint : G.Messages.PrisonerHint);
		ScheduleTipReset(isWarden);
	}

	// Через несколько секунд после старта сворачивает обучающую подсказку до
	// краткого названия роли. Токен поколения отсекает срабатывание таймера от
	// предыдущего раунда (роль между раундами может смениться).
	private void ScheduleTipReset(bool isWarden)
	{
		int generation = ++_tipGeneration;
		SceneTreeTimer timer = GetTree().CreateTimer(G.Round.HintDuration);
		timer.Timeout += () =>
		{
			// Таймер живёт в SceneTree и мог пережить перезагрузку сцены (возврат
			// в меню) — тогда этот GameFlow уже освобождён, обращаться к нему нельзя.
			if (!IsInstanceValid(this))
			{
				return;
			}

			if (generation == _tipGeneration && GameState.CurrentPhase == GamePhase.Gameplay)
			{
				_ui.SetTip(isWarden ? G.Messages.WardenRole : G.Messages.PrisonerRole);
			}
		};
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

	private void OnInventorySlotDropRequested(int slotIndex)
	{
		PlayerController.LocalPlayer?.RequestDropSlot(slotIndex);
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
