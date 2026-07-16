using Godot;

namespace EscapeGame;

/// <summary>
/// Глобальные игровые константы: сеть, движение, ввод, UI-сообщения.
/// </summary>
public static class G
{
    // Network
    public const int Port = 7777;
    public const int MaxPlayers = 8;
    public const string DefaultAddress = "127.0.0.1";

    // Player movement defaults
    public const int Speed = 10;
    public const int FallAcceleration = 75;
    public const int JumpImpulse = 20;
    public const float MouseSensitivity = 0.002f;

    // Spawn
    public static readonly Vector3 SpawnPosition = new(0, 1, 0);

    // Input
    public const Key PauseKey = Key.Escape;

    // UI messages
    public static class Messages
    {
        public const string Joining = "Подключение...";
        public const string ServerStartError = "Ошибка старта сервера";
        public const string JoinError = "Ошибка подключения";
        public const string ServerDisconnected = "Сервер отключился";
        public const string ConnectionFailed = "Не удалось подключиться к серверу";
        public const string DefaultPlayerName = "Игрок";
    }

    // Lobby
    public static class Lobby
    {
        public const int MinPlayersToStart = 1;
    }
}
