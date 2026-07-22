using Godot;
using EscapeGame.Services;

namespace EscapeGame.Player;

/// <summary>
/// Движение игрока: ввод WASD, ускорение, прыжок, гравитация. Работает только
/// для локального authority в фазах Gameplay или Inventory.
/// </summary>
public partial class PlayerMovement : Node
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

	private PlayerController _player;
	private Vector3 _targetVelocity;

	public float CurrentSpeedMultiplier { get; set; } = 1f;

	public override void _Ready()
	{
		_player = GetParent<PlayerController>();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!IsActive())
		{
			return;
		}

		if (Input.GetMouseMode() != Input.MouseModeEnum.Captured)
		{
			return;
		}

		if (_player.VitalState != PlayerVitalState.Alive)
		{
			_player.PlayerCamera?.CameraEffects?.Update(Vector3.Zero, _player.IsOnFloor(), Speed, (float)delta);
			return;
		}

		Vector3 input = ReadMovementInput();
		ApplyHorizontalMovement(input, (float)delta);
		ApplyJump();
		ApplyGravity((float)delta);

		_player.Velocity = _targetVelocity;
		_player.MoveAndSlide();

		_player.PublishNetTransform();
		_player.PlayerCamera?.CameraEffects?.Update(_targetVelocity, _player.IsOnFloor(), Speed, (float)delta);
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

	private Vector3 ReadMovementInput()
	{
		Vector3 forward = -_player.Transform.Basis.Z;
		Vector3 right = _player.Transform.Basis.X;

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
		float acceleration = _player.IsOnFloor() ? MoveAcceleration : AirAcceleration;
		float deceleration = _player.IsOnFloor() ? MoveDeceleration : AirAcceleration * 0.5f;

		Vector3 horizontalVelocity = new(_targetVelocity.X, 0f, _targetVelocity.Z);
		Vector3 targetHorizontal = direction * Speed * CurrentSpeedMultiplier;

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
		if (_player.IsOnFloor() && Input.IsActionJustPressed("jump"))
		{
			_targetVelocity.Y = JumpImpulse;
		}
	}

	private void ApplyGravity(float delta)
	{
		if (_player.IsOnFloor() && _targetVelocity.Y <= 0)
		{
			_targetVelocity.Y = 0;
			return;
		}

		float gravityScale = _targetVelocity.Y > 0 ? GravityScaleUp : GravityScaleDown;
		_targetVelocity.Y -= FallAcceleration * gravityScale * delta;
	}
}
