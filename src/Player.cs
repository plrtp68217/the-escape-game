using Godot;

public partial class Player : CharacterBody3D
{
	[Export] public int Speed { get; set; } = 14;
	[Export] public int FallAcceleration { get; set; } = 75;
	[Export] public int JumpImpulse { get; set; } = 20;
	[Export] public float MouseSensitivity { get; set; } = 0.002f;

	private Vector3 _targetVelocity = Vector3.Zero;
	private Node3D _pivot;

	public override void _Ready()
	{
		_pivot = GetNode<Node3D>("Pivot");

		if (_pivot == null)
		{
			GD.PrintErr("Player Pivot node for Camera not found!");
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseMotion mouseEvent &&
				Input.GetMouseMode() == Input.MouseModeEnum.Captured)
		{
			// --- Поворот ВЛЕВО-ВПРАВО (Yaw) ---
			// Вращаем ВЕСЬ узел Player вокруг оси Y (вертикаль)
			// mouseEvent.Relative.X - смещение мыши по горизонтали за этот кадр
			RotateY(-mouseEvent.Relative.X * MouseSensitivity);

			// --- Поворот ВВЕРХ-ВНИЗ (Pitch) ---
			// Вращаем ТОЛЬКО Pivot вокруг оси X (горизонталь)
			// mouseEvent.Relative.Y - смещение мыши по вертикали
			_pivot.RotateX(-mouseEvent.Relative.Y * MouseSensitivity);

			// --- Ограничиваем угол обзора (чтобы не перевернуться) ---
			// Зажимаем значение Rotation.X в диапазоне от -90° до +90°
			Vector3 pivotRotation = _pivot.Rotation;
			pivotRotation.X = Mathf.Clamp(pivotRotation.X, Mathf.DegToRad(-90f), Mathf.DegToRad(90f));
			_pivot.Rotation = pivotRotation;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Input.GetMouseMode() != Input.MouseModeEnum.Captured)
		{
			return;
		}

		var direction = Vector3.Zero;

		if (Input.IsActionPressed("move_right"))
		{
			direction.X += 1.0f;
		}
		if (Input.IsActionPressed("move_left"))
		{
			direction.X -= 1.0f;
		}
		if (Input.IsActionPressed("move_back"))
		{
			direction.Z += 1.0f;
		}
		if (Input.IsActionPressed("move_forward"))
		{
			direction.Z -= 1.0f;
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