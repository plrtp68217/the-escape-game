using Godot;
using EscapeGame.Logging;
using EscapeGame.Network;
using EscapeGame.Services;
using Inv = EscapeGame.Inventory;

namespace EscapeGame.Player;

/// <summary>
/// Фасад игрока. Содержит общее состояние, идентификатор, authority и ссылки
/// на компоненты. Сама не реализует игровую логику — делегирует её дочерним
/// нодам: движение, камера, способности, взаимодействие, бой, инвентарь, визуал.
/// </summary>
public partial class PlayerController : CharacterBody3D
{
	[Export]
	public int PlayerId { get; set; } = 1;

	// Общее состояние, которым делятся компоненты.
	public PlayerRole Role { get; set; } = PlayerRole.Prisoner;
	public int Health { get; set; } = G.Combat.MaxHealth;
	public PlayerVitalState VitalState { get; set; } = PlayerVitalState.Alive;
	public bool Crouching { get; set; }

	public Inventory.PlayerInventory Inventory { get; private set; }

	// Сетевые цели для интерполяции удалённых реплик.
	[Export]
	public Vector3 NetPosition { get; set; }

	[Export]
	public float NetRotationY { get; set; }

	// Совместимость: старый код обращается к LocalPlayer напрямую.
	public static PlayerController LocalPlayer => ServiceLocator.Players?.Local;

	public event System.Action InventoryChanged;
	public event System.Action LocalDamaged;
	public event System.Action LocalHealed;
	public event System.Action LocalHitConfirmed;

	public PlayerMovement Movement { get; private set; }
	public PlayerCamera PlayerCamera { get; private set; }
	public PlayerAbilities Abilities { get; private set; }
	public PlayerInteraction Interaction { get; private set; }
	public PlayerCombat Combat { get; private set; }
	public PlayerInventoryController InventoryController { get; private set; }
	public PlayerVisuals Visuals { get; private set; }

	public override void _EnterTree()
	{
		// Идентичность игрока берём из имени узла: его задаёт NetworkManager
		// (Name = id пира) и детерминированно реплицирует спавнер. Authority у
		// MultiplayerSynchronizer движок разрешает менять только в _EnterTree.
		if (int.TryParse(Name.ToString(), out int id))
		{
			PlayerId = id;
			SetMultiplayerAuthority(id);
		}
	}

	public override void _Ready()
	{
		Inventory = new Inventory.PlayerInventory(4, 4);

		Movement = GetNodeOrNull<PlayerMovement>("Movement");
		PlayerCamera = GetNodeOrNull<PlayerCamera>("Camera");
		Abilities = GetNodeOrNull<PlayerAbilities>("Abilities");
		Interaction = GetNodeOrNull<PlayerInteraction>("Interaction");
		Combat = GetNodeOrNull<PlayerCombat>("Combat");
		InventoryController = GetNodeOrNull<PlayerInventoryController>("Inventory");
		Visuals = GetNodeOrNull<PlayerVisuals>("Visuals");

		ServiceLocator.Players?.Register(this);

		NetPosition = Position;
		NetRotationY = Rotation.Y;

		Visuals?.RefreshRoleModel();
		if (ServiceLocator.Lobby != null)
		{
			ServiceLocator.Lobby.LobbyUpdated += OnLobbyUpdated;
		}

		if (ServiceLocator.Network?.IsServer ?? false)
		{
			ServiceLocator.Inventory?.BroadcastInventory(this);
		}
		else if (IsMultiplayerAuthority())
		{
			// Клиент: реплика игрока появляется позже серверной рассылки,
			// поэтому дозапрашиваем актуальное состояние инвентаря.
			ServiceLocator.Inventory?.RequestInventorySync((long)PlayerId);
		}
	}

	public override void _Process(double delta)
	{
		// Локального игрока двигает физика; чужих реплики плавно догоняют
		// сетевые цели. Большой скачок переносится мгновенно.
		if (!(ServiceLocator.Network?.HasPeer ?? false) || IsMultiplayerAuthority())
		{
			return;
		}

		if (Position.DistanceSquaredTo(NetPosition) > G.Net.SnapDistance * G.Net.SnapDistance)
		{
			Position = NetPosition;
			Rotation = new Vector3(0f, NetRotationY, 0f);
			return;
		}

		float weight = 1f - Mathf.Exp(-G.Net.InterpolationRate * (float)delta);
		Position = Position.Lerp(NetPosition, weight);
		Rotation = new Vector3(0f, Mathf.LerpAngle(Rotation.Y, NetRotationY, weight), 0f);
	}

	public override void _ExitTree()
	{
		ServiceLocator.Players?.Unregister(PlayerId);

		if (ServiceLocator.Lobby != null)
		{
			ServiceLocator.Lobby.LobbyUpdated -= OnLobbyUpdated;
		}
	}

	internal void NotifyHitConfirmed()
	{
		LocalHitConfirmed?.Invoke();
	}

	// Публикует текущий трансформ как сетевую цель. Вызывается каждый физический
	// кадр при движении и после телепорта на спавн.
	public void PublishNetTransform()
	{
		NetPosition = Position;
		NetRotationY = Rotation.Y;
	}

	// Синхронизированный ростер обновился — переприменяем модель и имя.
	public void OnLobbyUpdated()
	{
		Visuals?.RefreshRoleModel();
	}

	// Обновляет инвентарь по данным от сервера.
	public void UpdateInventory(string[] itemIds, int[] counts, int equippedIndex)
	{
		for (int i = 0; i < Inventory.Slots.Count && i < itemIds.Length && i < counts.Length; i++)
		{
			Inventory.InventorySlot slot = Inventory.Slots[i];
			string id = itemIds[i];

			if (string.IsNullOrEmpty(id))
			{
				slot.Clear();
			}
			else
			{
				Inv.InventoryItem item = Inv.ItemDatabase.Get(id);
				slot.Set(item, counts[i]);
			}
		}

		Inventory.TryEquip(equippedIndex);
		InventoryController?.RefreshEquippedModel();
		InventoryChanged?.Invoke();
	}

	// Стартовый набор по роли. Вызывается сервером в начале каждого раунда.
	public void GiveRoleLoadout()
	{
		if (!(ServiceLocator.Network?.IsServer ?? false))
		{
			return;
		}

		Inventory.Clear();

		PlayerRole role = Role;
		if (ServiceLocator.Lobby?.Players != null
			&& ServiceLocator.Lobby.Players.TryGetValue(PlayerId, out LobbyPlayerInfo info))
		{
			role = info.Role;
		}

		// Единый стартовый набор: топор (дверь/барьер/защита), аптечка и таблетка.
		Inventory.AddItem(Inv.ItemDatabase.Get(G.Door.AxeItemId), 1);
		Inventory.AddItem(Inv.ItemDatabase.Get("health"), 1);
		Inventory.AddItem(Inv.ItemDatabase.Get("pill"), 1);
		Inventory.TryEquip(0);
		ServiceLocator.Inventory?.BroadcastInventory(this);
	}

	// Применяет здоровье и состояние, пришедшие от сервера.
	public void ApplyVitals(int health, PlayerVitalState state)
	{
		int previousHealth = Health;
		Health = health;
		VitalState = state;

		if (IsMultiplayerAuthority())
		{
			if (health < previousHealth)
			{
				PlayerCamera?.AddTrauma(G.Feedback.DamageTrauma);
				LocalDamaged?.Invoke();
			}
			else if (health > previousHealth)
			{
				LocalHealed?.Invoke();
			}
		}

		Visuals?.ApplyVitalsVisuals();
	}

	// Сброс способностей в начале раунда.
	public void ResetAbilities()
	{
		Abilities?.Reset();
	}

	// Внутренний helper: сообщить UI, что инвентарь изменился.
	internal void NotifyInventoryChanged()
	{
		InventoryChanged?.Invoke();
	}
}
