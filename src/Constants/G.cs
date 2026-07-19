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
    public const float Speed = 10f;
    public const float MoveAcceleration = 80f;
    public const float MoveDeceleration = 60f;
    public const float AirAcceleration = 8f;
    public const float FallAcceleration = 75f;
    public const float JumpImpulse = 20f;
    public const float MouseSensitivity = 0.002f;
    public const float GravityScaleDown = 1.6f;
    public const float GravityScaleUp = 1.0f;

    // Camera effects
    public static class Camera
    {
        // Частота покачивания камеры при ходьбе: чем больше значение,
        // тем быстрее шаги.
        public const float BobFrequency = 1f;

        // Амплитуда покачивания: высота вертикального смещения камеры
        // вверх-вниз при каждом шаге.
        public const float BobAmplitude = 0.1f;

        // Максимальный наклон камеры вбок при стрейфе. Положительное
        // значение — наклон в сторону движения, отрицательное — против.
        public const float StrafeTilt = 0.01f;

        // Сила толчка камеры вниз при приземлении. Умножается на скорость
        // падения, поэтому чем выше падение, тем заметнее эффект.
        public const float LandingImpact = 0.05f;

        // Скорость, с которой камера возвращается в исходное положение
        // после приземления. Больше значение — быстрее отскок.
        public const float LandingRecovery = 10f;
    }

    // Spawn
    public static readonly Vector3 SpawnPosition = new(0, 1, 0);

    // Точки спавна по ролям (применяются при старте раунда). Пока карта —
    // плоскость, поэтому позиции временные, лишь бы стороны были разнесены.
    public static readonly Vector3 WardenSpawn = new(0, 1, 8);
    public static readonly Vector3 PrisonerSpawn = new(0, 1, -6);

    // Расстояние между заключёнными, чтобы они не спавнились друг в друге.
    public const float PrisonerSpawnSpacing = 2f;

    // Input
    public const Key PauseKey = Key.Escape;
    public const Key ScoreboardKey = Key.Tab;

    // UI messages
    public static class Messages
    {
        public const string Joining = "Подключение...";
        public const string ServerStartError = "Ошибка старта сервера";
        public const string JoinError = "Ошибка подключения";
        public const string ServerDisconnected = "Сервер отключился";
        public const string ConnectionFailed = "Не удалось подключиться к серверу";
        public const string DefaultPlayerName = "Игрок";
        public const string GameInProgress = "Игра уже идёт";
        public const string NoPlayers = "Нет игроков";
    }

    // Lobby
    public static class Lobby
    {
        public const int MinPlayersToStart = 1;
    }

    // Раунд
    public static class Round
    {
        // Сколько секунд у заключённых на побег, прежде чем побеждает надзиратель.
        public const int Duration = 180;
    }

    // Двери камер
    public static class Door
    {
        // Сколько ударов топором нужно, чтобы выбить запертую дверь.
        public const int Health = 3;

        public const string KeyItemId = "key";
        public const string AxeItemId = "axe";
    }
}
