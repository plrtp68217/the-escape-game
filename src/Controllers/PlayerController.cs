using Godot;

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

	private Vector3 _targetVelocity = Vector3.Zero;
	private Node3D _pivot;
	private Camera3D _camera;
	private CameraEffects _cameraEffects;

	public override void _Ready()
	{
		_pivot = GetNode<Node3D>("Pivot");
		_camera = GetNode<Camera3D>("Pivot/Camera3D");
		_cameraEffects = new CameraEffects(_camera);

		// Нельзя менять multiplayer authority прямо в _Ready() — движок в этот
		// момент ещё не закончил синхронизацию заспавненного узла (MultiplayerSynchronizer
		// ещё обрабатывает "pending spawn"), и SetMultiplayerAuthority здесь падает
		// с ошибкой "no network ID". Поэтому откладываем через CallDeferred —
		// вызов выполнится, когда спавн полностью завершится и PlayerId уже
		// будет корректно реплицирован на этот пир.
		CallDeferred(nameof(ApplyAuthority));
	}

	private void ApplyAuthority()
	{
		SetMultiplayerAuthority(PlayerId);

		_camera.Current = IsMultiplayerAuthority();
	}

	public override void _Input(InputEvent @event)
	{
		if (GameState.CurrentPhase != GamePhase.Gameplay)
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
	}

	public override void _PhysicsProcess(double delta)
	{
		if (GameState.CurrentPhase != GamePhase.Gameplay)
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
