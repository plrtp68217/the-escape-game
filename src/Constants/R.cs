using Godot;

namespace EscapeGame;

/// <summary>
/// Глобальные пути к ресурсам и узлам сцены.
/// </summary>
public static class R
{
    // Scenes
    public const string PlayerScene = "res://scenes/player.tscn";

    // Runtime scene node paths
    public static readonly NodePath PlayersContainer = "/root/Main/Players";

    // Character assets
    public static class Characters
    {
        public const string Prisoner = "res://assets/characters/prisoner.glb";
        public const string Sanitar = "res://assets/characters/sanitar.glb";
    }
}
