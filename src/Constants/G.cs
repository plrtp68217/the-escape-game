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

        // --- Тряска камеры (trauma-based) ---
        // Скорость затухания тряски: сколько единиц «травмы» уходит в секунду.
        public const float ShakeDecay = 2.0f;

        // Максимальное смещение камеры (в метрах) при полной травме.
        public const float ShakeMaxOffset = 0.14f;

        // Максимальный поворот камеры (в радианах) при полной травме.
        public const float ShakeMaxRotation = 0.1f;

        // --- Толчок камеры (punch) при действии, например ударе топором ---
        // Скорость возврата смещения и поворота после толчка (больше — быстрее).
        public const float PunchPositionRecovery = 0.6f;
        public const float PunchRotationRecovery = 1.6f;
    }

    // Триггеры визуального фидбека (тряска/оверлеи при событиях).
    public static class Feedback
    {
        // Сколько «травмы» добавляет получение урона (0..1).
        public const float DamageTrauma = 0.7f;
    }

    // Spawn
    public static readonly Vector3 SpawnPosition = new(0, 1, 0);

    // Надзиратель спавнится в общем зале, у выхода.
    public static readonly Vector3 WardenSpawn = new(8, 1, 0);

    // По одной точке на камеру: каждый заключённый попадает в свою запертую
    // клетку. Соответствуют расположению Cell0..Cell3 в main.tscn.
    public static readonly Vector3[] PrisonerCellSpawns =
    {
        new(-8, 1, -10.5f),
        new(-8, 1, -3.5f),
        new(-8, 1, 3.5f),
        new(-8, 1, 10.5f),
    };

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

    // Здоровье и бой
    public static class Combat
    {
        public const int MaxHealth = 100;

        // Урон от удара топором. 3 удара — заключённый повержен.
        public const int AxeDamage = 34;
        public const float AttackRange = 3f;

        // Лечение расходником (аптечка/шприц) и здоровье после подъёма.
        public const int HealAmount = 40;
        public const int ReviveHealth = 50;

        // Дистанция, с которой можно поднять поверженного союзника.
        public const float ReviveRange = 2.5f;

        // Сколько секунд нужно удерживать F, чтобы поднять поверженного союзника.
        public const float ReviveHoldTime = 1.5f;
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
