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

	// Роль в текущем раунде. Выводится из ростера LobbyManager (см. RefreshRoleModel).
	public PlayerRole Role { get; private set; } = PlayerRole.Prisoner;
	private bool _roleModelApplied;

	// Здоровье и жизненное состояние — авторитет сервера, приходят через CombatRelay.
	public int Health { get; private set; } = G.Combat.MaxHealth;
	public PlayerVitalState VitalState { get; private set; } = PlayerVitalState.Alive;

	// Способности (Веха 8). Crouching синхронизируется (см. player.tscn) — надзиратель
	// по нему решает, показывать ли заключённого при скане.
	[Export]
	public bool Crouching { get; set; }

	public float Stamina { get; private set; } = G.Abilities.StaminaMax;
	private bool _sprinting;
	private float _speedMultiplier = 1f;
	private float _staminaRegenDelay;
	private float _pivotBaseY;

	// Скан надзирателя: кулдаун (0 = готов) и остаток подсветки.
	private float _scanCooldownLeft;
	private float _scanRevealLeft;
	private readonly Dictionary<long, Label3D> _scanMarkers = new();

	public float StaminaRatio => Stamina / G.Abilities.StaminaMax;
	public bool ScanReady => _scanCooldownLeft <= 0f;
	public bool ScanActive => _scanRevealLeft > 0f;
	public int ScanCooldownSeconds => Mathf.CeilToInt(_scanCooldownLeft);

	public Inv.PlayerInventory Inventory { get; private set; }
	public event System.Action InventoryChanged;

	// События визуального фидбека локального игрока (HUD-оверлеи).
	// Тряску/отдачу камеры контроллер применяет сам, а вспышки на экране
	// показывает через GameFlow, который подписан на эти события.
	public event System.Action LocalDamaged;
	public event System.Action LocalHealed;
	public event System.Action LocalHitConfirmed;

	private Node3D _model;
	private Label3D _nameTag;
	private string _baseName = string.Empty;

	// Подъём поверженного союзника: цель рядом и накопленный прогресс удержания F.
	private PlayerController _reviveTarget;
	private float _reviveProgress;
	// Кому уже отправили запрос на подъём — чтобы не потратить второй медикамент,
	// пока по сети не пришло обновление состояния цели.
	private PlayerController _reviveSentTo;

	// Материал-подсветки поверженного игрока (общий на всех, ленивая инициализация).
	private static StandardMaterial3D _downedHighlight;

	public bool HasReviveTarget =>
		_reviveTarget != null && GodotObject.IsInstanceValid(_reviveTarget);
	public float ReviveProgress => _reviveProgress;

	// Самолечение расходником удержанием ЛКМ: прогресс канала и защёлка «уже
	// отправлено», чтобы за одно удержание применить ровно один предмет.
	private float _healProgress;
	private bool _healSent;
	public bool IsSelfHealing => _healProgress > 0f;
	public float SelfHealProgress => _healProgress;

	public List<Interaction.IInteractable> NearbyInteractables { get; } = new();

	private Interaction.IInteractable _targetInteractable;
	private bool _attackQueued;
	private Vector3 _targetVelocity = Vector3.Zero;
	private Node3D _pivot;
	private Camera3D _camera;
	private Marker3D _hand;
	private Vector3 _handBaseRotation;
	private Tween _swingTween;
	private CameraEffects _cameraEffects;

	public override void _EnterTree()
	{
		// Идентичность игрока берём из ИМЕНИ узла: его задаёт NetworkManager
		// (Name = id пира) и детерминированно реплицирует спавнер, поэтому имя
		// одинаково на всех пирах уже здесь. Синхронизируемый [Export] PlayerId
		// для этого не годится — он приходит позже и несогласованно.
		//
		// Authority у MultiplayerSynchronizer движок разрешает менять ТОЛЬКО в
		// _EnterTree. Если делать это позже (в _Ready или отложенно), спавн
		// отклоняется ("no network ID") и синхронизация позиции игнорируется —
		// именно из-за этого игроки расходились по позициям и моделям на разных
		// пирах.
		if (int.TryParse(Name.ToString(), out int id))
		{
			PlayerId = id;
			SetMultiplayerAuthority(id);
		}
	}

	public override void _Ready()
	{
		_pivot = GetNode<Node3D>("Pivot");
		_pivotBaseY = _pivot.Position.Y;
		_camera = GetNode<Camera3D>("Pivot/Camera3D");
		_hand = GetNode<Marker3D>("Pivot/Hand");
		_handBaseRotation = _hand.Rotation;
		_model = GetNode<Node3D>("Model");
		_nameTag = GetNodeOrNull<Label3D>("NameTag");
		_cameraEffects = new CameraEffects(_camera);

		Inventory = new Inv.PlayerInventory(4, 4);

		// PlayerId и authority уже выставлены в _EnterTree, поэтому здесь всё
		// корректно: ключ AllPlayers, роль по ростеру и IsMultiplayerAuthority().
		AllPlayers[PlayerId] = this;

		// Модель роли игрок применяет сам — при спавне и при каждом обновлении
		// ростера, поэтому она верна независимо от порядка спавна и синка ролей.
		LobbyManager.Instance.LobbyUpdated += RefreshRoleModel;
		RefreshRoleModel();

		_camera.Current = IsMultiplayerAuthority();

		if (IsMultiplayerAuthority())
		{
			LocalPlayer = this;
		}

		if (Multiplayer.IsServer())
		{
			SeedStartingInventory();
			Inv.InventoryRelay.Instance?.BroadcastInventory(this);
		}
		else if (IsMultiplayerAuthority())
		{
			// Клиент: реплика игрока появляется позже серверной рассылки инвентаря,
			// поэтому первая SyncInventory теряется — дозапрашиваем актуальное
			// состояние у сервера, иначе инвентарь при первом открытии пуст.
			Inv.InventoryRelay.Instance?.RpcId(1, nameof(Inv.InventoryRelay.RequestInventorySync), (long)PlayerId);
		}
	}

	// Стартовый набор игрока. Только сервер (изменяет авторитетный инвентарь).
	private void SeedStartingInventory()
	{
		// Тестовое наполнение инвентаря для проверки UI.
		Inventory.AddItem(Inv.ItemDatabase.Get("axe"), 1);
		Inventory.AddItem(Inv.ItemDatabase.Get("health"), 3);
		Inventory.AddItem(Inv.ItemDatabase.Get("pill"), 2);
	}

	// Сброс инвентаря к стартовому набору при перезапуске раунда. Только сервер;
	// новое состояние рассылается всем через InventoryRelay.
	public void ResetForRound()
	{
		if (!Multiplayer.IsServer())
		{
			return;
		}

		Inventory.Clear();
		SeedStartingInventory();
		Inv.InventoryRelay.Instance?.BroadcastInventory(this);
	}

	public override void _ExitTree()
	{
		ClearScanMarkers();
		AllPlayers.Remove(PlayerId);

		if (LobbyManager.Instance != null)
		{
			LobbyManager.Instance.LobbyUpdated -= RefreshRoleModel;
		}

		if (LocalPlayer == this)
		{
			LocalPlayer = null;
		}
	}

	// Определяет свою роль из синхронизированного ростера LobbyManager и
	// применяет модель. Идемпотентно и не привязано к authority: вызывается
	// при спавне и на каждом LobbyUpdated, поэтому модель корректна на всех
	// пирах независимо от порядка спавна игроков и синхронизации ролей.
	public void RefreshRoleModel()
	{
		if (LobbyManager.Instance == null
			|| !LobbyManager.Instance.Players.TryGetValue(PlayerId, out LobbyPlayerInfo info))
		{
			return;
		}

		if (_nameTag != null)
		{
			_baseName = info.Name;
			UpdateNameTag();
		}

		if (_roleModelApplied && info.Role == Role)
		{
			return;
		}

		Role = info.Role;
		_roleModelApplied = true;
		SwapModel(info.Role == PlayerRole.Warden ? R.Characters.Sanitar : R.Characters.Prisoner);
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

	// Применяет здоровье и состояние (приходит от CombatRelay на всех пирах).
	public void ApplyVitals(int health, PlayerVitalState state)
	{
		int previousHealth = Health;
		Health = health;
		VitalState = state;

		// Визуальный фидбек — только для своего игрока и только на реальное
		// изменение здоровья (сброс в начале раунда 100→100 не считается).
		if (IsMultiplayerAuthority())
		{
			if (health < previousHealth)
			{
				_cameraEffects?.AddTrauma(G.Feedback.DamageTrauma);
				LocalDamaged?.Invoke();
			}
			else if (health > previousHealth)
			{
				LocalHealed?.Invoke();
			}
		}

		// Поверженного наклоняем — грубый визуальный маркер (без анимации).
		if (_model != null)
		{
			_model.Rotation = state == PlayerVitalState.Downed
				? new Vector3(Mathf.DegToRad(80f), 0f, 0f)
				: Vector3.Zero;
		}

		// Нокаут виден остальным: красная подсветка модели и подпись над головой.
		bool downed = state == PlayerVitalState.Downed;
		SetHighlight(downed);
		UpdateNameTag();

		// Сброс собственного накопленного подъёма, если сам ушёл в нокаут.
		if (downed)
		{
			_reviveTarget = null;
			_reviveProgress = 0f;
			_reviveSentTo = null;
		}
	}

	// Подпись над головой: имя, а для поверженного — красная пометка «НОКАУТИРОВАН».
	private void UpdateNameTag()
	{
		if (_nameTag == null)
		{
			return;
		}

		if (VitalState == PlayerVitalState.Downed)
		{
			_nameTag.Text = $"{_baseName}\n[НОКАУТИРОВАН]";
			_nameTag.Modulate = new Color(1f, 0.3f, 0.3f);
		}
		else
		{
			_nameTag.Text = _baseName;
			_nameTag.Modulate = new Color(1f, 1f, 1f);
		}
	}

	// Подсветка модели поверженного игрока — материал поверх меша (material_overlay),
	// который не заменяет собственные материалы модели и легко снимается.
	private void SetHighlight(bool on)
	{
		if (_model == null)
		{
			return;
		}

		ApplyOverlay(_model, on ? GetDownedHighlight() : null);
	}

	private static StandardMaterial3D GetDownedHighlight()
	{
		if (_downedHighlight == null)
		{
			_downedHighlight = new StandardMaterial3D
			{
				AlbedoColor = new Color(1f, 0.15f, 0.15f, 0.4f),
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				EmissionEnabled = true,
				Emission = new Color(1f, 0.1f, 0.1f),
				EmissionEnergyMultiplier = 0.6f,
			};
		}

		return _downedHighlight;
	}

	private static void ApplyOverlay(Node node, Material overlay)
	{
		foreach (Node child in node.GetChildren())
		{
			if (child is GeometryInstance3D geometry)
			{
				geometry.MaterialOverlay = overlay;
			}

			ApplyOverlay(child, overlay);
		}
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
		if (!IsMultiplayerAuthority())
		{
			return;
		}

		if (_targetInteractable is Node node && GodotObject.IsInstanceValid(node))
		{
			Interaction.InteractionRelay.Instance?.RpcId(1, nameof(Interaction.InteractionRelay.RequestInteract),
				(long)PlayerId, node.GetPath().ToString());
		}

		// Подъём поверженного союзника — не мгновенный: удерживание F
		// накапливается в UpdateRevive (см. _PhysicsProcess).
	}

	// Удар топором по ЛКМ. Надзиратель бьёт заключённого, заключённый — выбивает
	// дверь. Луч из камеры, цель отправляется на сервер. По надзирателю бить
	// нельзя: заключённый попадает только по дверям.
	private void TryAttack()
	{
		if (!IsMultiplayerAuthority()
			|| VitalState != PlayerVitalState.Alive
			|| Inventory.EquippedSlot?.Item?.Id != G.Door.AxeItemId)
		{
			return;
		}

		// Замах виден всегда, независимо от попадания — вес удара чувствуется.
		PlaySwing();
		_cameraEffects?.Punch(
			new Vector3(0f, -0.03f, 0f),
			new Vector3(Mathf.DegToRad(3f), 0f, 0f));

		Vector3 from = _camera.GlobalPosition;
		Vector3 to = from + (-_camera.GlobalTransform.Basis.Z) * G.Combat.AttackRange;

		var query = PhysicsRayQueryParameters3D.Create(from, to);
		query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

		Godot.Collections.Dictionary hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
		if (hit.Count == 0)
		{
			return;
		}

		Node collider = hit["collider"].As<Node>();

		if (Role == PlayerRole.Warden)
		{
			if (collider is PlayerController target && target.Role == PlayerRole.Prisoner)
			{
				// Отметка попадания — оптимистично по лучу; урон подтвердит сервер.
				LocalHitConfirmed?.Invoke();
				Combat.CombatRelay.Instance?.RpcId(1, nameof(Combat.CombatRelay.RequestAttack),
					(long)PlayerId, (long)target.PlayerId);
			}
			return;
		}

		// Заключённый: по игрокам урона нет, но по двери — выбивание.
		if (collider is Interaction.CellDoor door)
		{
			Interaction.InteractionRelay.Instance?.RpcId(1, nameof(Interaction.InteractionRelay.RequestAxeHit),
				(long)PlayerId, door.GetPath().ToString());
		}
	}

	// Быстрый замах экипированной модели в руке: рубящее движение и возврат.
	private void PlaySwing()
	{
		if (_hand == null)
		{
			return;
		}

		_swingTween?.Kill();
		_hand.Rotation = _handBaseRotation;

		Vector3 swung = _handBaseRotation + new Vector3(Mathf.DegToRad(-75f), 0f, Mathf.DegToRad(20f));

		_swingTween = CreateTween();
		_swingTween.TweenProperty(_hand, "rotation", swung, 0.08)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		_swingTween.TweenProperty(_hand, "rotation", _handBaseRotation, 0.22)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
	}

	// Лечение расходником в руке удержанием ЛКМ (~2 c). За удержание — одно
	// применение; чтобы вылечиться ещё раз, ЛКМ нужно отпустить и зажать снова.
	private void UpdateSelfHeal(float delta)
	{
		Inv.InventorySlot slot = Inventory.EquippedSlot;
		bool canHeal = VitalState == PlayerVitalState.Alive
			&& slot != null && !slot.IsEmpty && IsConsumable(slot.Item.Id)
			&& Health < G.Combat.MaxHealth
			&& Input.IsActionPressed("attack");

		if (!canHeal)
		{
			_healProgress = 0f;
			_healSent = false;
			return;
		}

		// Запрос уже отправлен — держим бар полным, пока по сети не придёт здоровье.
		if (_healSent)
		{
			_healProgress = 1f;
			return;
		}

		_healProgress += delta / G.Combat.HealChannelTime;
		if (_healProgress >= 1f)
		{
			_healProgress = 1f;
			_healSent = true;
			Combat.CombatRelay.Instance?.RpcId(1, nameof(Combat.CombatRelay.RequestUseItem),
				(long)PlayerId, Inventory.EquippedSlotIndex);
		}
	}

	private static bool IsConsumable(string itemId)
	{
		return itemId == "health" || itemId == "pill" || itemId == "syringe";
	}

	// Выбрасывает экипированный предмет (клавиша drop / колесо не задействует).
	private void RequestDropEquipped()
	{
		if (!IsMultiplayerAuthority() || VitalState != PlayerVitalState.Alive)
		{
			return;
		}

		Inv.InventorySlot slot = Inventory.EquippedSlot;
		if (slot == null || slot.IsEmpty)
		{
			return;
		}

		RequestDropSlot(Inventory.EquippedSlotIndex);
	}

	// Общий путь выброса: клиент просит сервер выбросить содержимое слота.
	// Позицию (перед игроком, чуть над полом) считаем здесь — только клиент знает,
	// куда смотрит игрок; сервер её валидирует.
	public void RequestDropSlot(int slotIndex)
	{
		if (!IsMultiplayerAuthority())
		{
			return;
		}

		Vector3 forward = -GlobalTransform.Basis.Z;
		Vector3 position = GlobalPosition + forward * G.DropDistance + Vector3.Up * G.DropHeight;

		Inv.InventoryRelay.Instance?.RpcId(1, nameof(Inv.InventoryRelay.RequestDrop),
			(long)PlayerId, slotIndex, position);
	}

	// Колесо мыши: переключить экипировку на следующий/предыдущий занятый слот
	// хотбара (первые G.Hotbar.SlotCount ячеек).
	public void CycleHotbar(int direction)
	{
		if (!IsMultiplayerAuthority())
		{
			return;
		}

		int count = Mathf.Min(G.Hotbar.SlotCount, Inventory.Slots.Count);
		if (count <= 0)
		{
			return;
		}

		int current = Inventory.EquippedSlotIndex;
		int start = current >= 0 && current < count ? current : (direction > 0 ? -1 : 0);

		for (int step = 1; step <= count; step++)
		{
			int index = ((start + direction * step) % count + count) % count;
			if (!Inventory.Slots[index].IsEmpty)
			{
				Inv.InventoryRelay.Instance?.RpcId(1, nameof(Inv.InventoryRelay.RequestEquip),
					(long)PlayerId, index);
				return;
			}
		}
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

		// Сначала добавляем в руку (чтобы были валидны мировые трансформы), затем
		// нормализуем по реальному видимому размеру.
		_hand.AddChild(model);
		NormalizeHeldModel(model);
	}

	// Приводит модель в руке к целевому размеру и центрирует её в ладони. Размер
	// считаем по AABB в МИРОВЫХ координатах (с учётом вложенных трансформов GLB) —
	// иначе у моделей со вложенными узлами размер вычислялся неверно, и предмет
	// становился гигантским (камера внутри меша → себе не видно, другим — огромный)
	// либо исчезающе мелким.
	private void NormalizeHeldModel(Node3D model)
	{
		Inv.ModelBounds.FitVisibleSize(model, 0.25f);

		// После масштабирования совмещаем центр модели с точкой руки.
		if (Inv.ModelBounds.TryComputeWorldAabb(model, out Aabb scaled))
		{
			model.GlobalPosition += _hand.GlobalPosition - scaled.GetCenter();
		}
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
			float sensitivity = Settings.MouseSensitivity;
			RotateY(-mouseEvent.Relative.X * sensitivity);

			_pivot.RotateX(-mouseEvent.Relative.Y * sensitivity);

			Vector3 pivotRotation = _pivot.Rotation;
			pivotRotation.X = Mathf.Clamp(
				pivotRotation.X,
				Mathf.DegToRad(-90f),
				Mathf.DegToRad(90f)
			);
			_pivot.Rotation = pivotRotation;
		}

		// Действия недоступны поверженному игроку (осмотреться мышью — можно).
		if (VitalState != PlayerVitalState.Alive)
		{
			return;
		}

		if (@event.IsActionPressed("interact"))
		{
			RequestInteract();
		}

		// Удар — только при захваченной мыши, чтобы клик по инвентарю не бил.
		// Сам луч выполняется в _PhysicsProcess (безопасно для physics space).
		// С расходником в руке ЛКМ не бьёт, а лечит удержанием (см. UpdateSelfHeal).
		if (@event.IsActionPressed("attack")
			&& Input.GetMouseMode() == Input.MouseModeEnum.Captured)
		{
			_attackQueued = true;
		}

		// Выбросить экипированный предмет.
		if (@event.IsActionPressed("drop"))
		{
			RequestDropEquipped();
		}

		// Колесо мыши переключает экипировку по хотбару.
		if (@event is InputEventMouseButton { Pressed: true } wheel
			&& Input.GetMouseMode() == Input.MouseModeEnum.Captured)
		{
			if (wheel.ButtonIndex == MouseButton.WheelUp)
			{
				CycleHotbar(-1);
			}
			else if (wheel.ButtonIndex == MouseButton.WheelDown)
			{
				CycleHotbar(1);
			}
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

		// Поверженный игрок не двигается — ждёт подъёма. Но эффекты камеры
		// (затухание тряски от последнего удара) продолжаем обновлять.
		if (VitalState != PlayerVitalState.Alive)
		{
			_cameraEffects?.Update(Vector3.Zero, IsOnFloor(), Speed, (float)delta);
			return;
		}

		Vector3 input = ReadMovementInput();
		UpdateAbilities(input, (float)delta);
		ApplyHorizontalMovement(input, (float)delta);
		ApplyJump();
		ApplyGravity((float)delta);

		Velocity = _targetVelocity;
		MoveAndSlide();

		_cameraEffects?.Update(_targetVelocity, IsOnFloor(), Speed, (float)delta);

		// Луч атаки выполняем здесь (в физическом шаге), а не в _Input — так
		// запрос к physics space безопасен по времени.
		if (_attackQueued)
		{
			_attackQueued = false;
			TryAttack();
		}

		UpdateRevive((float)delta);
		UpdateSelfHeal((float)delta);
	}

	// Подъём поверженного союзника удержанием F. Прогресс копится, пока рядом
	// есть цель и зажата клавиша; по заполнении шлём запрос на сервер.
	private void UpdateRevive(float delta)
	{
		if (Role != PlayerRole.Prisoner)
		{
			_reviveTarget = null;
			_reviveProgress = 0f;
			_reviveSentTo = null;
			return;
		}

		_reviveTarget = FindReviveTarget();

		if (_reviveTarget == null || !Input.IsActionPressed("interact"))
		{
			_reviveProgress = 0f;
			_reviveSentTo = null;
			return;
		}

		// Уже отправили запрос на эту цель — ждём обновления её состояния по сети,
		// держим бар заполненным и не тратим второй медикамент.
		if (_reviveSentTo == _reviveTarget)
		{
			_reviveProgress = 1f;
			return;
		}

		_reviveProgress += delta / G.Combat.ReviveHoldTime;
		if (_reviveProgress >= 1f)
		{
			_reviveProgress = 1f;
			_reviveSentTo = _reviveTarget;
			Combat.CombatRelay.Instance?.RpcId(1, nameof(Combat.CombatRelay.RequestRevive),
				(long)PlayerId);
		}
	}

	// Ближайший поверженный союзник в радиусе подъёма. Предметов не требует —
	// поднять можно удержанием F рядом (см. CombatRelay.RequestRevive).
	private PlayerController FindReviveTarget()
	{
		PlayerController best = null;
		float bestDistance = G.Combat.ReviveRange * G.Combat.ReviveRange;

		foreach (PlayerController p in AllPlayers.Values)
		{
			if (p == this
				|| p.Role != PlayerRole.Prisoner
				|| p.VitalState != PlayerVitalState.Downed)
			{
				continue;
			}

			float distance = p.GlobalPosition.DistanceSquaredTo(GlobalPosition);
			if (distance <= bestDistance)
			{
				bestDistance = distance;
				best = p;
			}
		}

		return best;
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

	// Способности (Веха 8): присед, спринт с выносливостью, скан надзирателя.
	// Вызывается только для живого локального игрока (см. _PhysicsProcess).
	private void UpdateAbilities(Vector3 moveInput, float delta)
	{
		// Присед: медленнее и ниже; синхронизируется, чтобы скан видел стойку.
		bool crouchHeld = Input.IsActionPressed("crouch");
		if (crouchHeld != Crouching)
		{
			Crouching = crouchHeld;
			ApplyCrouchPose();
		}

		// Спринт: нельзя присев, нужно двигаться и иметь выносливость.
		_sprinting = Input.IsActionPressed("sprint")
			&& !Crouching
			&& moveInput.LengthSquared() > 0.01f
			&& Stamina > 0f
			&& (_sprinting || Stamina >= G.Abilities.SprintMinStamina);

		if (_sprinting)
		{
			Stamina = Mathf.Max(0f, Stamina - G.Abilities.StaminaDrainPerSec * delta);
			_staminaRegenDelay = G.Abilities.StaminaRegenDelay;
		}
		else if (_staminaRegenDelay > 0f)
		{
			_staminaRegenDelay -= delta;
		}
		else
		{
			Stamina = Mathf.Min(G.Abilities.StaminaMax, Stamina + G.Abilities.StaminaRegenPerSec * delta);
		}

		_speedMultiplier = Crouching
			? G.Abilities.CrouchMultiplier
			: _sprinting ? G.Abilities.SprintMultiplier : 1f;

		// Скан: кулдаун тикает всегда; надзиратель активирует по R, когда готов.
		if (_scanCooldownLeft > 0f)
		{
			_scanCooldownLeft -= delta;
		}

		if (Role == PlayerRole.Warden && ScanReady && Input.IsActionJustPressed("ability"))
		{
			_scanRevealLeft = G.Abilities.ScanRevealDuration;
			_scanCooldownLeft = G.Abilities.ScanCooldown;
		}

		UpdateScanMarkers(delta);
	}

	private void ApplyCrouchPose()
	{
		if (_pivot == null)
		{
			return;
		}

		Vector3 p = _pivot.Position;
		p.Y = _pivotBaseY - (Crouching ? G.Abilities.CrouchCameraDrop : 0f);
		_pivot.Position = p;
	}

	// Подсветка заключённых сквозь стены на время скана. Маркеры создаёт только
	// локальный надзиратель у себя — остальные их не видят. Присевшие и выбывшие
	// пропадают из подсветки (проверяется каждый кадр — присед мгновенно прячет).
	private void UpdateScanMarkers(float delta)
	{
		if (_scanRevealLeft <= 0f)
		{
			if (_scanMarkers.Count > 0)
			{
				ClearScanMarkers();
			}
			return;
		}

		_scanRevealLeft -= delta;

		foreach (var kv in _scanMarkers)
		{
			kv.Value.Visible = false;
		}

		foreach (PlayerController p in AllPlayers.Values)
		{
			if (p.Role != PlayerRole.Prisoner
				|| p.VitalState != PlayerVitalState.Alive
				|| p.Crouching)
			{
				continue;
			}

			Label3D marker = GetOrCreateMarker(p.PlayerId);
			marker.GlobalPosition = p.GlobalPosition + Vector3.Up * 2.8f;
			marker.Visible = true;
		}

		if (_scanRevealLeft <= 0f)
		{
			ClearScanMarkers();
		}
	}

	private Label3D GetOrCreateMarker(long id)
	{
		if (_scanMarkers.TryGetValue(id, out Label3D existing) && GodotObject.IsInstanceValid(existing))
		{
			return existing;
		}

		// Метка над головой в МИРОВЫХ координатах: билборд (лицом к камере) и без
		// depth-теста (видна сквозь стены). БЕЗ FixedSize — иначе метка держит
		// постоянный экранный размер и у ближнего игрока раздувается на весь экран.
		var marker = new Label3D
		{
			Text = "[!]",
			FontSize = 48,
			PixelSize = 0.012f,
			Modulate = new Color(1f, 0.25f, 0.25f),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			NoDepthTest = true,
			RenderPriority = 10,
		};
		GetTree().CurrentScene.AddChild(marker);
		_scanMarkers[id] = marker;
		return marker;
	}

	private void ClearScanMarkers()
	{
		foreach (Label3D m in _scanMarkers.Values)
		{
			if (GodotObject.IsInstanceValid(m))
			{
				m.QueueFree();
			}
		}
		_scanMarkers.Clear();
	}

	// Сброс способностей в начале раунда (вызывается для локального игрока).
	public void ResetAbilities()
	{
		Stamina = G.Abilities.StaminaMax;
		_sprinting = false;
		_staminaRegenDelay = 0f;
		Crouching = false;
		ApplyCrouchPose();
		_scanCooldownLeft = 0f;
		_scanRevealLeft = 0f;
		ClearScanMarkers();
	}

	private void ApplyHorizontalMovement(Vector3 direction, float delta)
	{
		float acceleration = IsOnFloor() ? MoveAcceleration : AirAcceleration;
		float deceleration = IsOnFloor() ? MoveDeceleration : AirAcceleration * 0.5f;

		Vector3 horizontalVelocity = new(_targetVelocity.X, 0f, _targetVelocity.Z);
		Vector3 targetHorizontal = direction * Speed * _speedMultiplier;

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
