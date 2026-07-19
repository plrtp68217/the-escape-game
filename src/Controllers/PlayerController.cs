using System.Collections.Generic;
using Godot;
using Inv = EscapeGame.Inventory;

namespace EscapeGame;

public partial class PlayerController : CharacterBody3D
{
	[Export]
	public float Speed { get; set; } = G.Speed;

	[Export]
	public float MoveAcceleration { get; set; } = G.MoveAcceleration;

	[Export]
	public float MoveDeceleration { get; set; } = G.MoveDeceleration;

	[Export]
	public float AirAcceleration { get; set; } = G.AirAcceleration;

	[Export]
	public float FallAcceleration { get; set; } = G.FallAcceleration;

	[Export]
	public float JumpImpulse { get; set; } = G.JumpImpulse;

	[Export]
	public float GravityScaleUp { get; set; } = G.GravityScaleUp;

	[Export]
	public float GravityScaleDown { get; set; } = G.GravityScaleDown;

	[Export]
	public float MouseSensitivity { get; set; } = G.MouseSensitivity;

	[Export]
	public int PlayerId { get; set; } = 1;

	public static PlayerController LocalPlayer { get; private set; }
	public static readonly Dictionary<long, PlayerController> AllPlayers = new();

	// Роль в текущем раунде. Назначается в GameFlow при старте игры.
	public PlayerRole Role { get; private set; } = PlayerRole.Prisoner;

	public Inv.PlayerInventory Inventory { get; private set; }
	public event System.Action InventoryChanged;

	public List<Interaction.IInteractable> NearbyInteractables { get; } = new();

	private Interaction.IInteractable _targetInteractable;
	private Vector3 _targetVelocity = Vector3.Zero;
	private Node3D _pivot;
	private Camera3D _camera;
	private Marker3D _hand;
	private CameraEffects _cameraEffects;

	public override void _Ready()
	{
		_pivot = GetNode<Node3D>("Pivot");
		_camera = GetNode<Camera3D>("Pivot/Camera3D");
		_hand = GetNode<Marker3D>("Pivot/Hand");
		_cameraEffects = new CameraEffects(_camera);

		Inventory = new Inv.PlayerInventory(4, 4);
		AllPlayers[PlayerId] = this;

		// Нельзя менять multiplayer authority прямо в _Ready() — движок в этот
		// момент ещё не закончил синхронизацию заспавненного узла (MultiplayerSynchronizer
		// ещё обрабатывает "pending spawn"), и SetMultiplayerAuthority здесь падает
		// с ошибкой "no network ID". Поэтому откладываем через CallDeferred —
		// вызов выполнится, когда спавн полностью завершится и PlayerId уже
		// будет корректно реплицирован на этот пир.
		CallDeferred(nameof(ApplyAuthority));
	}

	public override void _ExitTree()
	{
		AllPlayers.Remove(PlayerId);

		if (LocalPlayer == this)
		{
			LocalPlayer = null;
		}
	}

	private void ApplyAuthority()
	{
		SetMultiplayerAuthority(PlayerId);

		_camera.Current = IsMultiplayerAuthority();

		if (IsMultiplayerAuthority())
		{
			LocalPlayer = this;
		}

		if (Multiplayer.IsServer())
		{
			// Тестовое наполнение инвентаря для проверки UI.
			Inventory.AddItem(Inv.ItemDatabase.Get("axe"), 1);
			Inventory.AddItem(Inv.ItemDatabase.Get("health"), 3);
			Inventory.AddItem(Inv.ItemDatabase.Get("pill"), 2);

			Inv.InventoryRelay.Instance?.BroadcastInventory(this);
		}
	}

	// Применяет роль игрока: запоминает её и меняет видимую модель.
	// Вызывается на каждом пире для каждого игрока (роль берётся из
	// синхронизированного ростера LobbyManager), поэтому не привязано к authority.
	public void ApplyRole(PlayerRole role)
	{
		Role = role;
		SwapModel(role == PlayerRole.Warden ? R.Characters.Sanitar : R.Characters.Prisoner);
	}

	private void SwapModel(string modelPath)
	{
		var modelRoot = GetNodeOrNull<Node3D>("Model");
		if (modelRoot == null)
		{
			return;
		}

		foreach (Node child in modelRoot.GetChildren())
		{
			child.QueueFree();
		}

		var scene = GD.Load<PackedScene>(modelPath);
		if (scene == null)
		{
			GD.PrintErr($"PlayerController: не удалось загрузить модель {modelPath}");
			return;
		}

		var instance = scene.Instantiate<Node3D>();
		// Разворот на 180° вокруг Y — модель смотрит вперёд (как AuxScene в player.tscn).
		instance.Transform = new Transform3D(new Basis(Vector3.Up, Mathf.Pi), Vector3.Zero);
		modelRoot.AddChild(instance);
	}

	public void UpdateInventory(string[] itemIds, int[] counts, int equippedIndex)
	{
		for (int i = 0; i < Inventory.Slots.Count && i < itemIds.Length && i < counts.Length; i++)
		{
			Inv.InventorySlot slot = Inventory.Slots[i];
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

		RefreshEquippedModel();
		InventoryChanged?.Invoke();
	}

	public void RegisterInteractable(Interaction.IInteractable interactable)
	{
		if (!IsMultiplayerAuthority())
		{
			return;
		}

		if (!NearbyInteractables.Contains(interactable))
		{
			NearbyInteractables.Add(interactable);
			UpdateTargetInteractable();
		}
	}

	public void UnregisterInteractable(Interaction.IInteractable interactable)
	{
		if (!IsMultiplayerAuthority())
		{
			return;
		}

		NearbyInteractables.Remove(interactable);
		UpdateTargetInteractable();
	}

	private void UpdateTargetInteractable()
	{
		_targetInteractable = null;
		float closest = float.MaxValue;

		for (int i = NearbyInteractables.Count - 1; i >= 0; i--)
		{
			Interaction.IInteractable interactable = NearbyInteractables[i];

			// Объект мог быть удалён из мира (например, подобранный предмет).
			if (interactable is Node node && !GodotObject.IsInstanceValid(node))
			{
				NearbyInteractables.RemoveAt(i);
				continue;
			}

			float distance = interactable.GlobalPosition.DistanceSquaredTo(GlobalPosition);
			if (distance < closest)
			{
				closest = distance;
				_targetInteractable = interactable;
			}
		}
	}

	// Текущая подсказка взаимодействия для HUD (клиент).
	public string GetInteractPrompt()
	{
		if (_targetInteractable is Node node && !GodotObject.IsInstanceValid(node))
		{
			_targetInteractable = null;
		}

		return _targetInteractable?.GetPrompt(this) ?? string.Empty;
	}

	public void RequestInteract()
	{
		if (_targetInteractable is not Node node
			|| !GodotObject.IsInstanceValid(node)
			|| !IsMultiplayerAuthority())
		{
			return;
		}

		Interaction.InteractionRelay.Instance?.RpcId(1, nameof(Interaction.InteractionRelay.RequestInteract),
			(long)PlayerId, node.GetPath().ToString());
	}

	public void RefreshEquippedModel()
	{
		Inv.InventorySlot slot = Inventory.EquippedSlot;
		EquipItem(slot?.Item);
	}

	public void EquipItem(Inv.InventoryItem item)
	{
		foreach (Node child in _hand.GetChildren())
		{
			child.QueueFree();
		}

		if (item?.WorldModel == null)
		{
			return;
		}

		Node3D model = item.WorldModel.Instantiate<Node3D>();

		// Модель предмета в руке часто повторяет сцену WorldItem (например,
		// axe.tscn). Отключаем её как подбираемый объект, иначе она бы
		// зарегистрировалась как предмет рядом с самим игроком.
		if (model is Inv.WorldItem heldItem)
		{
			heldItem.Monitoring = false;
			heldItem.Monitorable = false;
		}

		NormalizeModel(model);
		_hand.AddChild(model);
	}

	private static void NormalizeModel(Node3D model)
	{
		Aabb bounds = CalculateBounds(model);
		if (bounds.Size.LengthSquared() <= 0)
		{
			return;
		}

		float maxDimension = bounds.Size[(int)bounds.Size.MaxAxisIndex()];

		float targetSize = 0.25f;
		float scale = targetSize / maxDimension;
		model.Scale = new Vector3(scale, scale, scale);

		Vector3 centerOffset = -bounds.GetCenter() * scale;
		model.Position = centerOffset;
	}

	private static Aabb CalculateBounds(Node3D model)
	{
		Aabb total = new(Vector3.Zero, Vector3.Zero);
		bool first = true;

		foreach (Node child in model.GetChildren())
		{
			if (child is VisualInstance3D visual)
			{
				Aabb bounds = visual.GetAabb();
				if (first)
				{
					total = bounds;
					first = false;
				}
				else
				{
					total = total.Merge(bounds);
				}
			}
			else if (child is Node3D childNode)
			{
				Aabb childBounds = CalculateBounds(childNode);
				if (first)
				{
					total = childBounds;
					first = false;
				}
				else if (childBounds.Size.LengthSquared() > 0)
				{
					total = total.Merge(childBounds);
				}
			}
		}

		return total;
	}

	public override void _Input(InputEvent @event)
	{
		if (
			GameState.CurrentPhase != GamePhase.Gameplay
			&& GameState.CurrentPhase != GamePhase.Inventory
		)
		{
			return;
		}

		if (Multiplayer.MultiplayerPeer == null || IsMultiplayerAuthority() == false)
		{
			return;
		}

		if (
			@event is InputEventMouseMotion mouseEvent
			&& Input.GetMouseMode() == Input.MouseModeEnum.Captured
		)
		{
			RotateY(-mouseEvent.Relative.X * MouseSensitivity);

			_pivot.RotateX(-mouseEvent.Relative.Y * MouseSensitivity);

			Vector3 pivotRotation = _pivot.Rotation;
			pivotRotation.X = Mathf.Clamp(
				pivotRotation.X,
				Mathf.DegToRad(-90f),
				Mathf.DegToRad(90f)
			);
			_pivot.Rotation = pivotRotation;
		}

		if (@event.IsActionPressed("interact"))
		{
			RequestInteract();
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (
			GameState.CurrentPhase != GamePhase.Gameplay
			&& GameState.CurrentPhase != GamePhase.Inventory
		)
		{
			return;
		}

		if (Multiplayer.MultiplayerPeer == null || IsMultiplayerAuthority() == false)
		{
			return;
		}

		if (Input.GetMouseMode() != Input.MouseModeEnum.Captured)
		{
			return;
		}

		Vector3 input = ReadMovementInput();
		ApplyHorizontalMovement(input, (float)delta);
		ApplyJump();
		ApplyGravity((float)delta);

		Velocity = _targetVelocity;
		MoveAndSlide();

		_cameraEffects?.Update(_targetVelocity, IsOnFloor(), Speed, (float)delta);
	}

	private Vector3 ReadMovementInput()
	{
		Vector3 forward = -Transform.Basis.Z;
		Vector3 right = Transform.Basis.X;

		Vector3 direction = Vector3.Zero;

		if (Input.IsActionPressed("move_right"))
		{
			direction += right;
		}
		if (Input.IsActionPressed("move_left"))
		{
			direction -= right;
		}
		if (Input.IsActionPressed("move_back"))
		{
			direction -= forward;
		}
		if (Input.IsActionPressed("move_forward"))
		{
			direction += forward;
		}

		if (direction.Length() > 0)
		{
			direction = direction.Normalized();
		}

		return direction;
	}

	private void ApplyHorizontalMovement(Vector3 direction, float delta)
	{
		float acceleration = IsOnFloor() ? MoveAcceleration : AirAcceleration;
		float deceleration = IsOnFloor() ? MoveDeceleration : AirAcceleration * 0.5f;

		Vector3 horizontalVelocity = new(_targetVelocity.X, 0f, _targetVelocity.Z);
		Vector3 targetHorizontal = direction * Speed;

		if (targetHorizontal.LengthSquared() > 0)
		{
			horizontalVelocity = horizontalVelocity.MoveToward(
				targetHorizontal,
				acceleration * delta
			);
		}
		else
		{
			horizontalVelocity = horizontalVelocity.MoveToward(Vector3.Zero, deceleration * delta);
		}

		_targetVelocity.X = horizontalVelocity.X;
		_targetVelocity.Z = horizontalVelocity.Z;
	}

	private void ApplyJump()
	{
		if (IsOnFloor() && Input.IsActionJustPressed("jump"))
		{
			_targetVelocity.Y = JumpImpulse;
		}
	}

	private void ApplyGravity(float delta)
	{
		if (IsOnFloor() && _targetVelocity.Y <= 0)
		{
			_targetVelocity.Y = 0;
			return;
		}

		float gravityScale = _targetVelocity.Y > 0 ? GravityScaleUp : GravityScaleDown;
		_targetVelocity.Y -= FallAcceleration * gravityScale * delta;
	}
}
