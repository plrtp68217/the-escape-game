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

    // UI node paths inside Main.tscn
    public static class UI
    {
        public static readonly NodePath PauseMenu = "UserInterface/PauseMenu";
        public static readonly NodePath TipLabel = "UserInterface/TipLabel";
        public static readonly NodePath NetworkMenu = "UserInterface/NetworkMenu";
        public static readonly NodePath IpInput = "UserInterface/NetworkMenu/VBoxContainer/IpInput";
        public static readonly NodePath StatusLabel = "UserInterface/NetworkMenu/VBoxContainer/StatusLabel";
        public static readonly NodePath HostButton = "UserInterface/NetworkMenu/VBoxContainer/HostButton";
        public static readonly NodePath JoinButton = "UserInterface/NetworkMenu/VBoxContainer/JoinButton";
    }

    // Character assets
    public static class Characters
    {
        public const string Prisoner = "res://assets/characters/prisoner.glb";
        public const string Sanitar = "res://assets/characters/sanitar.glb";
    }
}
