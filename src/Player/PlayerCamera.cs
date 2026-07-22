using Godot;
using EscapeGame.Core;
using EscapeGame.Effects;

namespace EscapeGame.Player;

/// <summary>
/// Камера и мышь: поворот от первого лица, эффекты камеры. Только для
/// локального authority.
/// </summary>
public partial class PlayerCamera : Node
{
	[Export]
	public float MouseSensitivity { get; set; } = G.MouseSensitivity;

	private PlayerController _player;
	private Node3D _pivot;
	private Camera3D _camera;
	private CameraEffects _cameraEffects;

	public CameraEffects CameraEffects => _cameraEffects;

	public override void _Ready()
	{
		_player = GetParent<PlayerController>();
		_pivot = _player.GetNode<Node3D>("Pivot");
		_camera = _player.GetNode<Camera3D>("Pivot/Camera3D");
		_cameraEffects = new CameraEffects(_camera);

		_camera.Current = _player.IsMultiplayerAuthority();
	}

	public override void _Input(InputEvent @event)
	{
		if (!IsActive())
		{
			return;
		}

		if (@event is not InputEventMouseMotion mouseEvent
			|| Input.GetMouseMode() != Input.MouseModeEnum.Captured)
		{
			return;
		}

		float sensitivity = Settings.MouseSensitivity;
		_player.RotateY(-mouseEvent.Relative.X * sensitivity);

		_pivot.RotateX(-mouseEvent.Relative.Y * sensitivity);
		Vector3 pivotRotation = _pivot.Rotation;
		pivotRotation.X = Mathf.Clamp(
			pivotRotation.X,
			Mathf.DegToRad(-90f),
			Mathf.DegToRad(90f)
		);
		_pivot.Rotation = pivotRotation;
	}

	public void AddTrauma(float amount)
	{
		_cameraEffects?.AddTrauma(amount);
	}

	public void Punch(Vector3 positionOffset, Vector3 rotationOffset)
	{
		_cameraEffects?.Punch(positionOffset, rotationOffset);
	}

	public Vector3 Forward => -_camera.GlobalTransform.Basis.Z;
	public Vector3 GlobalPosition => _camera.GlobalPosition;
	public Godot.Collections.Array<Rid> BuildHeldItemExcludes()
	{
		var exclude = new Godot.Collections.Array<Rid> { _player.GetRid() };

		Marker3D hand = _player.GetNodeOrNull<Marker3D>("Pivot/Hand");
		if (hand == null)
		{
			return exclude;
		}

		foreach (Node child in hand.GetChildren())
		{
			CollectCollisionRids(child, exclude);
		}

		return exclude;
	}

	private static void CollectCollisionRids(Node node, Godot.Collections.Array<Rid> into)
	{
		if (node is CollisionObject3D collider)
		{
			into.Add(collider.GetRid());
		}

		foreach (Node child in node.GetChildren())
		{
			CollectCollisionRids(child, into);
		}
	}

	private bool IsActive()
	{
		if (GameFlow.GameState.CurrentPhase != GameFlow.GamePhase.Gameplay
			&& GameFlow.GameState.CurrentPhase != GameFlow.GamePhase.Inventory)
		{
			return false;
		}

		return Multiplayer.MultiplayerPeer != null
			&& _player.IsMultiplayerAuthority();
	}
}
