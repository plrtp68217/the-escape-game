using Godot;

namespace EscapeGame.Inventory;

/// <summary>
/// Предмет, лежащий в мире. Знает свой ID и сообщает игроку, что он рядом.
/// </summary>
public partial class WorldItem : Area3D
{
    [Export]
    public string ItemId { get; set; } = string.Empty;

    [Export]
    public int Count { get; set; } = 1;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    private void OnBodyEntered(Node3D body)
    {
        if (body is PlayerController player && player.IsMultiplayerAuthority())
        {
            player.RegisterNearbyItem(this);
        }
    }

    private void OnBodyExited(Node3D body)
    {
        if (body is PlayerController player && player.IsMultiplayerAuthority())
        {
            player.UnregisterNearbyItem(this);
        }
    }
}
