using System.Collections.Generic;
using Godot;
using EscapeGame.Interaction;
using EscapeGame.Services;

namespace EscapeGame.Player;

/// <summary>
/// Взаимодействие с миром: луч из камеры, выбор цели, подсветка, подсказка.
/// Обрабатывает клавишу [F] для локального authority.
/// </summary>
public partial class PlayerInteraction : Node
{
	private PlayerController _player;
	private PlayerCamera _camera;

	public List<Interaction.IInteractable> NearbyInteractables { get; } = new();

	private Interaction.IInteractable _targetInteractable;
	private Node3D _highlightedNode;

	public bool HasTarget => _targetInteractable != null;
	public Interaction.IInteractable Target => _targetInteractable;

	private IInteractionService _interaction;

	public override void _Ready()
	{
		_player = GetParent<PlayerController>();
		_camera = _player.GetNodeOrNull<PlayerCamera>("Camera");
		_interaction = ServiceLocator.Interaction;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!IsActive())
		{
			ClearAimTarget();
			return;
		}

		UpdateAimTarget();
	}

	public override void _Input(InputEvent @event)
	{
		if (!IsActive())
		{
			return;
		}

		if (@event.IsActionPressed("interact") && _player.VitalState == PlayerVitalState.Alive)
		{
			RequestInteract();
		}
	}

	public void RegisterInteractable(Interaction.IInteractable interactable)
	{
		if (!IsActiveForInput())
		{
			return;
		}

		if (!NearbyInteractables.Contains(interactable))
		{
			NearbyInteractables.Add(interactable);
		}
	}

	public void UnregisterInteractable(Interaction.IInteractable interactable)
	{
		if (!IsActiveForInput())
		{
			return;
		}

		NearbyInteractables.Remove(interactable);
	}

	public string GetInteractPrompt()
	{
		if (_targetInteractable is Node node && !GodotObject.IsInstanceValid(node))
		{
			_targetInteractable = null;
		}

		return _targetInteractable?.GetPrompt(_player) ?? string.Empty;
	}

	private void RequestInteract()
	{
		if (_targetInteractable is Node node && GodotObject.IsInstanceValid(node))
		{
			var request = new InteractionRequest(
				InteractionKind.Use,
				(long)_player.PlayerId,
				node.GetPath().ToString());
			_interaction?.Execute(request);
		}
	}

	private void UpdateAimTarget()
	{
		if (_camera == null)
		{
			return;
		}

		Vector3 from = _camera.GlobalPosition;
		Vector3 dir = _camera.Forward;
		Godot.Collections.Array<Rid> exclude = _camera.BuildHeldItemExcludes();

		Interaction.IInteractable target = RaycastInteractable(from, dir, exclude)
			?? FindAimedItem(from, dir, exclude);

		if (target is Inventory.WorldItem { IsAvailable: false })
		{
			target = null;
		}

		if (target != null && !target.CanInteract(_player))
		{
			target = null;
		}

		SetAimTarget(target);
	}

	private Interaction.IInteractable RaycastInteractable(Vector3 from, Vector3 dir,
		Godot.Collections.Array<Rid> exclude)
	{
		var query = PhysicsRayQueryParameters3D.Create(from, from + dir * G.Interact.Range);
		query.Exclude = exclude;
		query.CollideWithAreas = true;
		query.CollideWithBodies = true;

		Godot.Collections.Dictionary hit = _player.GetWorld3D().DirectSpaceState.IntersectRay(query);
		if (hit.Count == 0)
		{
			return null;
		}

		return FindInteractable(hit["collider"].As<Node>());
	}

	private static Interaction.IInteractable FindInteractable(Node node)
	{
		while (node != null)
		{
			if (node is Interaction.IInteractable interactable)
			{
				return interactable;
			}
			node = node.GetParent();
		}
		return null;
	}

	private Interaction.IInteractable FindAimedItem(Vector3 from, Vector3 dir,
		Godot.Collections.Array<Rid> exclude)
	{
		float bestDot = Mathf.Cos(Mathf.DegToRad(G.Interact.AimAssistDegrees));
		float rangeSq = G.Interact.Range * G.Interact.Range;
		Inventory.WorldItem best = null;

		foreach (Inventory.WorldItem item in Inventory.WorldItem.All)
		{
			if (!item.IsAvailable)
			{
				continue;
			}

			Vector3 toItem = item.GlobalPosition + Vector3.Up * 0.3f - from;
			float distSq = toItem.LengthSquared();
			if (distSq > rangeSq || distSq < 0.0001f)
			{
				continue;
			}

			float dist = Mathf.Sqrt(distSq);
			Vector3 toDir = toItem / dist;
			if (dir.Dot(toDir) < bestDot)
			{
				continue;
			}

			var los = PhysicsRayQueryParameters3D.Create(from, from + toDir * dist);
			los.Exclude = exclude;
			if (_player.GetWorld3D().DirectSpaceState.IntersectRay(los).Count > 0)
			{
				continue;
			}

			bestDot = dir.Dot(toDir);
			best = item;
		}

		return best;
	}

	private void SetAimTarget(Interaction.IInteractable target)
	{
		if (target is Node node && !GodotObject.IsInstanceValid(node))
		{
			target = null;
		}

		_targetInteractable = target;

		Node3D targetNode = target as Node3D;
		if (ReferenceEquals(targetNode, _highlightedNode))
		{
			return;
		}

		if (_highlightedNode != null && GodotObject.IsInstanceValid(_highlightedNode))
		{
			PlayerVisuals.ApplyOverlay(_highlightedNode, null);
		}

		_highlightedNode = targetNode;

		if (_highlightedNode != null)
		{
			PlayerVisuals.ApplyOverlay(_highlightedNode, PlayerVisuals.GetTargetHighlight());
		}
	}

	private void ClearAimTarget()
	{
		_targetInteractable = null;

		if (_highlightedNode != null && GodotObject.IsInstanceValid(_highlightedNode))
		{
			PlayerVisuals.ApplyOverlay(_highlightedNode, null);
		}
		_highlightedNode = null;
	}

	private bool IsActive()
	{
		if (GameFlow.GameState.CurrentPhase != GameFlow.GamePhase.Gameplay
			&& GameFlow.GameState.CurrentPhase != GameFlow.GamePhase.Inventory)
		{
			return false;
		}

		return ServiceLocator.Network?.HasPeer ?? false
			&& _player.IsMultiplayerAuthority()
			&& Input.GetMouseMode() == Input.MouseModeEnum.Captured;
	}

	private bool IsActiveForInput()
	{
		return ServiceLocator.Network?.HasPeer ?? false
			&& _player.IsMultiplayerAuthority();
	}

	public override void _ExitTree()
	{
		ClearAimTarget();
	}
}
