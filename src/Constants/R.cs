using Godot;

namespace EscapeGame;

/// <summary>
/// Глобальные пути к ресурсам и узлам сцены.
/// </summary>
public static class R
{
    // Scenes
    public const string PlayerScene = "res://scenes/player.tscn";
    public const string UIRootScene = "res://scenes/ui/ui_root.tscn";

    // Runtime scene node paths
    public static readonly NodePath PlayersContainer = "/root/Main/Players";
    public static readonly NodePath UIRoot = "/root/Main/UIRoot";

    // UI node paths inside ui_root.tscn
    public static class UI
    {
        public static readonly NodePath TipLabel = "TipLabel";
        public static readonly NodePath MainMenu = "MainMenu";
        public static readonly NodePath LobbyMenu = "LobbyMenu";
        public static readonly NodePath PauseMenu = "PauseMenu";
        public static readonly NodePath Scoreboard = "Scoreboard";
    }

    // Character assets
    public static class Characters
    {
        public const string Prisoner = "res://assets/characters/prisoner.glb";
        public const string Sanitar = "res://assets/characters/sanitar.glb";
    }
}
