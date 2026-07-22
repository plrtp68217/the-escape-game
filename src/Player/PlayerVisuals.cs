using Godot;
using EscapeGame.Logging;
using EscapeGame.Network;
using EscapeGame.Services;

namespace EscapeGame.Player;

/// <summary>
/// Визуал игрока: модель по роли, неймтег, подсветка поверженного,
/// общие материалы подсветки для взаимодействий.
/// </summary>
public partial class PlayerVisuals : Node
{
	private PlayerController _player;
	private Node3D _model;
	private Label3D _nameTag;
	private string _baseName = string.Empty;
	private bool _roleModelApplied;

	private static StandardMaterial3D _downedHighlight;
	private static StandardMaterial3D _targetHighlight;

	public override void _Ready()
	{
		_player = GetParent<PlayerController>();
		_model = _player.GetNodeOrNull<Node3D>("Model");
		_nameTag = _player.GetNodeOrNull<Label3D>("NameTag");
	}

	public override void _ExitTree()
	{
		// Подсветка цели снимается в PlayerInteraction; здесь чистим собственные
		// материалы поверженного.
		SetHighlight(false);
	}

	public void RefreshRoleModel()
	{
		if (ServiceLocator.Lobby?.Players == null
			|| !ServiceLocator.Lobby.Players.TryGetValue(_player.PlayerId, out LobbyPlayerInfo info))
		{
			return;
		}

		if (_nameTag != null)
		{
			_baseName = info.Name;
			UpdateNameTag();
		}

		if (_roleModelApplied && info.Role == _player.Role)
		{
			return;
		}

		_player.Role = info.Role;
		_roleModelApplied = true;
		SwapModel(info.Role == PlayerRole.Warden ? R.Characters.Sanitar : R.Characters.Prisoner);
	}

	private void SwapModel(string modelPath)
	{
		if (_model == null)
		{
			return;
		}

		foreach (Node child in _model.GetChildren())
		{
			child.QueueFree();
		}

		var scene = GD.Load<PackedScene>(modelPath);
		if (scene == null)
		{
			Log.Error(Log.Cat.Game, $"PlayerVisuals: не удалось загрузить модель {modelPath}");
			return;
		}

		var instance = scene.Instantiate<Node3D>();
		instance.Transform = new Transform3D(new Basis(Vector3.Up, Mathf.Pi), Vector3.Zero);
		_model.AddChild(instance);
	}

	public void ApplyVitalsVisuals()
	{
		if (_model != null)
		{
			_model.Rotation = _player.VitalState == PlayerVitalState.Downed
				? new Vector3(Mathf.DegToRad(80f), 0f, 0f)
				: Vector3.Zero;
		}

		bool downed = _player.VitalState == PlayerVitalState.Downed;
		SetHighlight(downed);
		UpdateNameTag();
	}

	private void UpdateNameTag()
	{
		if (_nameTag == null)
		{
			return;
		}

		if (_player.VitalState == PlayerVitalState.Downed)
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

	public static StandardMaterial3D GetTargetHighlight()
	{
		if (_targetHighlight == null)
		{
			_targetHighlight = new StandardMaterial3D
			{
				AlbedoColor = new Color(1f, 0.9f, 0.3f, 0.35f),
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				EmissionEnabled = true,
				Emission = new Color(1f, 0.8f, 0.2f),
				EmissionEnergyMultiplier = 0.8f,
			};
		}

		return _targetHighlight;
	}

	public static void ApplyOverlay(Node node, Material overlay)
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
}
