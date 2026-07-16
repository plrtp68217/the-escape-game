using Godot;

namespace EscapeGame;

public partial class Player : CharacterBody3D
{
	[Export] public int Speed { get; set; } = G.Speed;
	[Export] public int FallAcceleration { get; set; } = G.FallAcceleration;
	[Export] public int JumpImpulse { get; set; } = G.JumpImpulse;
	[Export] public float MouseSensitivity { get; set; } = G.MouseSensitivity;
	[Export] public int PlayerId { get; set; } = 1;

	private Vector3 _targetVelocity = Vector3.Zero;
	private Node3D _pivot;
	private Camera3D _camera;

	public override void _Ready()
	{
		_pivot = GetNode<Node3D>("Pivot");
		_camera = GetNode<Camera3D>("Pivot/Camera3D");

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

		if (@event is InputEventMouseMotion mouseEvent &&
				Input.GetMouseMode() == Input.MouseModeEnum.Captured)
		{
			RotateY(-mouseEvent.Relative.X * MouseSensitivity);

			_pivot.RotateX(-mouseEvent.Relative.Y * MouseSensitivity);

			Vector3 pivotRotation = _pivot.Rotation;
			pivotRotation.X = Mathf.Clamp(pivotRotation.X, Mathf.DegToRad(-90f), Mathf.DegToRad(90f));
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

		_targetVelocity.X = direction.X * Speed;
		_targetVelocity.Z = direction.Z * Speed;

		if (IsOnFloor() && Input.IsActionJustPressed("jump"))
		{
			_targetVelocity.Y = JumpImpulse;
		}

		if (IsOnFloor() == false)
		{
			_targetVelocity.Y -= FallAcceleration * (float)delta;
		}

		Velocity = _targetVelocity;
		MoveAndSlide();
	}
}