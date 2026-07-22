using System.Collections.Generic;
using Godot;

namespace EscapeGame.Player;

/// <summary>
/// Способности игрока: присед, спринт с выносливостью, скан надзирателя.
/// </summary>
public partial class PlayerAbilities : Node
{
	private PlayerController _player;
	private PlayerMovement _movement;
	private PlayerCamera _camera;
	private Node3D _pivot;
	private float _pivotBaseY;

	public float Stamina { get; private set; } = G.Abilities.StaminaMax;
	private bool _sprinting;
	private float _staminaRegenDelay;

	private float _scanCooldownLeft;
	private float _scanRevealLeft;
	private readonly Dictionary<long, Label3D> _scanMarkers = new();

	public float StaminaRatio => Stamina / G.Abilities.StaminaMax;
	public bool ScanReady => _scanCooldownLeft <= 0f;
	public bool ScanActive => _scanRevealLeft > 0f;
	public int ScanCooldownSeconds => Mathf.CeilToInt(_scanCooldownLeft);

	public override void _Ready()
	{
		_player = GetParent<PlayerController>();
		_movement = _player.GetNodeOrNull<PlayerMovement>("Movement");
		_camera = _player.GetNodeOrNull<PlayerCamera>("Camera");
		_pivot = _player.GetNode<Node3D>("Pivot");
		_pivotBaseY = _pivot.Position.Y;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!IsActive())
		{
			return;
		}

		UpdateAbilities((float)delta);
	}

	public void Reset()
	{
		Stamina = G.Abilities.StaminaMax;
		_sprinting = false;
		_staminaRegenDelay = 0f;
		_player.Crouching = false;
		ApplyCrouchPose();
		_scanCooldownLeft = 0f;
		_scanRevealLeft = 0f;
		ClearScanMarkers();
	}

	private bool IsActive()
	{
		if (GameFlow.GameState.CurrentPhase != GameFlow.GamePhase.Gameplay
			&& GameFlow.GameState.CurrentPhase != GameFlow.GamePhase.Inventory)
		{
			return false;
		}

		return Multiplayer.MultiplayerPeer != null
			&& _player.IsMultiplayerAuthority()
			&& _player.VitalState == PlayerVitalState.Alive;
	}

	private void UpdateAbilities(float delta)
	{
		bool crouchHeld = Input.IsActionPressed("crouch");
		if (crouchHeld != _player.Crouching)
		{
			_player.Crouching = crouchHeld;
			ApplyCrouchPose();
		}

		Vector3 moveInput = ReadRawMovementInput();
		_sprinting = Input.IsActionPressed("sprint")
			&& !_player.Crouching
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

		if (_movement != null)
		{
			_movement.CurrentSpeedMultiplier = _player.Crouching
				? G.Abilities.CrouchMultiplier
				: _sprinting ? G.Abilities.SprintMultiplier : 1f;
		}

		if (_scanCooldownLeft > 0f)
		{
			_scanCooldownLeft -= delta;
		}

		if (_player.Role == PlayerRole.Warden && ScanReady && Input.IsActionJustPressed("ability"))
		{
			_scanRevealLeft = G.Abilities.ScanRevealDuration;
			_scanCooldownLeft = G.Abilities.ScanCooldown;
		}

		UpdateScanMarkers(delta);
	}

	private Vector3 ReadRawMovementInput()
	{
		Vector3 direction = Vector3.Zero;
		if (Input.IsActionPressed("move_right")) direction += Vector3.Right;
		if (Input.IsActionPressed("move_left")) direction += Vector3.Left;
		if (Input.IsActionPressed("move_forward")) direction += Vector3.Back;
		if (Input.IsActionPressed("move_back")) direction += Vector3.Forward;
		return direction.Length() > 0 ? direction.Normalized() : direction;
	}

	private void ApplyCrouchPose()
	{
		if (_pivot == null)
		{
			return;
		}

		Vector3 p = _pivot.Position;
		p.Y = _pivotBaseY - (_player.Crouching ? G.Abilities.CrouchCameraDrop : 0f);
		_pivot.Position = p;
	}

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

		foreach (Label3D marker in _scanMarkers.Values)
		{
			marker.Visible = false;
		}

		foreach (PlayerController p in PlayerController.AllPlayers.Values)
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
		foreach (Label3D marker in _scanMarkers.Values)
		{
			if (GodotObject.IsInstanceValid(marker))
			{
				marker.QueueFree();
			}
		}
		_scanMarkers.Clear();
	}

	public override void _ExitTree()
	{
		ClearScanMarkers();
	}
}
