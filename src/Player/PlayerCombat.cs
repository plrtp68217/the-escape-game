using Godot;
using EscapeGame.Core;
using EscapeGame.Services;

namespace EscapeGame.Player;

/// <summary>
/// Бой, лечение и подъём: удар топором, самолечение расходником, подъём
/// поверженного союзника. Обрабатывает ЛКМ для локального authority.
/// </summary>
public partial class PlayerCombat : Node
{
	private PlayerController _player;
	private PlayerCamera _camera;
	private Marker3D _hand;
	private Vector3 _handBaseRotation;
	private Tween _swingTween;

	private bool _attackQueued;
	private float _healProgress;
	private bool _healSent;

	private PlayerController _reviveTarget;
	private float _reviveProgress;
	private PlayerController _reviveSentTo;

	public bool HasReviveTarget => _reviveTarget != null && GodotObject.IsInstanceValid(_reviveTarget);
	public float ReviveProgress => _reviveProgress;
	public bool IsSelfHealing => _healProgress > 0f;
	public float SelfHealProgress => _healProgress;

	private ICombatService _combat;

	public override void _Ready()
	{
		_player = GetParent<PlayerController>();
		_combat = ServiceLocator.Combat;
		_camera = _player.GetNodeOrNull<PlayerCamera>("Camera");
		_hand = _player.GetNodeOrNull<Marker3D>("Pivot/Hand");
		if (_hand != null)
		{
			_handBaseRotation = _hand.Rotation;
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (!IsActive())
		{
			return;
		}

		// Удар — только при захваченной мыши, чтобы клик по инвентарю не бил.
		// Сам луч выполняется в _PhysicsProcess.
		if (@event.IsActionPressed("attack")
			&& Input.GetMouseMode() == Input.MouseModeEnum.Captured
			&& _player.VitalState == PlayerVitalState.Alive)
		{
			_attackQueued = true;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!IsActive())
		{
			return;
		}

		if (_attackQueued)
		{
			_attackQueued = false;
			TryAttack();
		}

		UpdateRevive((float)delta);
		UpdateSelfHeal((float)delta);
	}

	private void TryAttack()
	{
		if (_player.VitalState != PlayerVitalState.Alive
			|| _player.Inventory.EquippedSlot?.Item?.Id != G.Door.AxeItemId)
		{
			return;
		}

		PlaySwing();
		_camera?.Punch(
			new Vector3(0f, -0.03f, 0f),
			new Vector3(Mathf.DegToRad(3f), 0f, 0f));

		if (_camera == null)
		{
			return;
		}

		Vector3 from = _camera.GlobalPosition;
		Vector3 to = from + _camera.Forward * G.Combat.AttackRange;

		var query = PhysicsRayQueryParameters3D.Create(from, to);
		query.Exclude = new Godot.Collections.Array<Rid> { _player.GetRid() };

		Godot.Collections.Dictionary hit = _player.GetWorld3D().DirectSpaceState.IntersectRay(query);
		if (hit.Count == 0)
		{
			return;
		}

		Node collider = hit["collider"].As<Node>();

		if (_player.Role == PlayerRole.Warden)
		{
			if (collider is PlayerController target && target.Role == PlayerRole.Prisoner)
			{
				_player.NotifyHitConfirmed();
				_combat?.RequestAttack((long)_player.PlayerId, (long)target.PlayerId);
			}
			return;
		}

		if (collider is Interaction.CellDoor door)
		{
			var request = new Interaction.InteractionRequest(
				Interaction.InteractionKind.AxeHit,
				(long)_player.PlayerId,
				door.GetPath().ToString());
			ServiceLocator.Interaction?.Execute(request);
		}
	}

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

	private void UpdateSelfHeal(float delta)
	{
		Inventory.InventorySlot slot = _player.Inventory.EquippedSlot;
		bool canHeal = _player.VitalState == PlayerVitalState.Alive
			&& slot != null && !slot.IsEmpty && IsConsumable(slot.Item.Id)
			&& _player.Health < G.Combat.MaxHealth
			&& Input.IsActionPressed("attack");

		if (!canHeal)
		{
			_healProgress = 0f;
			_healSent = false;
			return;
		}

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
			_combat?.RequestSelfHeal((long)_player.PlayerId);
		}
	}

	private static bool IsConsumable(string itemId)
	{
		return itemId == Inventory.ItemIds.Health
			|| itemId == Inventory.ItemIds.Pill
			|| itemId == Inventory.ItemIds.Syringe;
	}

	private void UpdateRevive(float delta)
	{
		if (_player.Role != PlayerRole.Prisoner)
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
			_combat?.RequestRevive((long)_player.PlayerId);
		}
	}

	private PlayerController FindReviveTarget()
	{
		PlayerController best = null;
		float bestDistance = G.Combat.ReviveRange * G.Combat.ReviveRange;

		foreach (PlayerController p in ServiceLocator.Players.All)
		{
			if (p == _player
				|| p.Role != PlayerRole.Prisoner
				|| p.VitalState != PlayerVitalState.Downed)
			{
				continue;
			}

			float distance = p.GlobalPosition.DistanceSquaredTo(_player.GlobalPosition);
			if (distance <= bestDistance)
			{
				bestDistance = distance;
				best = p;
			}
		}

		return best;
	}

	private bool IsActive()
	{
		if (GameFlow.GameState.CurrentPhase != GameFlow.GamePhase.Gameplay
			&& GameFlow.GameState.CurrentPhase != GameFlow.GamePhase.Inventory)
		{
			return false;
		}

		return ServiceLocator.Network?.HasPeer ?? false
			&& _player.IsMultiplayerAuthority();
	}
}
