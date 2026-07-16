using Godot;

public partial class Player : CharacterBody3D
{
	[Export] public int Speed { get; set; } = 10;
	[Export] public int FallAcceleration { get; set; } = 75;
	[Export] public int JumpImpulse { get; set; } = 20;
	[Export] public float MouseSensitivity { get; set; } = 0.002f;

	[Export] public int PlayerId = 1;

	private int _playerId = 1;

	private Vector3 _targetVelocity = Vector3.Zero;
	private Node3D _pivot;
	private Camera3D _camera;

	public override void _Ready()
	{
		if (PlayerId > 0)
		{
			SetMultiplayerAuthority(PlayerId);
		}

		_pivot = GetNode<Node3D>("Pivot");
		_camera = GetNode<Camera3D>("Pivot/Camera3D");

		if (_pivot == null)
		{
			GD.PrintErr("Player Pivot node for Camera not found!");
		}

		// Камера должна быть активна только у "своего" игрока на этом клиенте,
		// иначе у всех клиентов будет дёргаться картинка между камерами.
		_camera.Current = IsMultiplayerAuthority();
	}

	public override void _Input(InputEvent @event)
	{
		if (!IsMultiplayerAuthority())
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
		if (!IsMultiplayerAuthority())
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