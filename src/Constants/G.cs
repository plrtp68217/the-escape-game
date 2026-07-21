using Godot;

namespace EscapeGame;

/// <summary>
/// Глобальные игровые константы: сеть, движение, ввод, UI-сообщения.
/// </summary>
public static class G
{
    // Network
    public const int Port = 4444;
    public const int MaxPlayers = 8;
    public const string DefaultAddress = "127.0.0.1";

    // Сетевое сглаживание (Веха 10). Позиция/поворот удалённых игроков приходят
    // сетевыми тиками (реже кадров) — без интерполяции реплики «дёргаются».
    public static class Net
    {
        // Скорость, с которой реплика догоняет сетевую позицию: больше — резче
        // (меньше «резины»), меньше — плавнее (но заметнее отставание).
        public const float InterpolationRate = 18f;

        // Порог «телепорта»: если сетевая позиция дальше этого (м), не
        // интерполируем через всю карту, а переносим мгновенно (спавн по
        // камерам в начале раунда, рематч).
        public const float SnapDistance = 3f;
    }

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

    // Надзиратель спавнится в центральном узле (пост охраны).
    public static readonly Vector3 WardenSpawn = new(0, 1, 2);

    // По одной точке на камеру: каждый заключённый попадает в свою запертую
    // клетку. Соответствуют расположению Cell0..Cell4 в main.tscn.
    public static readonly Vector3[] PrisonerCellSpawns =
    {
        new(-24, 1, -22), // Cell0 / К1 (север-запад)
        new(24, 1, -22),  // Cell1 / К2 (север-восток)
        new(0, 1, 14),    // Cell2 / К3 (центр)
        new(-24, 1, 10),  // Cell3 / К4 (центр-запад)
        new(24, 1, 10),   // Cell4 / К5 (центр-восток)
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

        // Краткое название роли (после того как обучающая подсказка свернулась).
        public const string WardenRole = "Ты — Надзиратель";
        public const string PrisonerRole = "Ты — Заключённый";

        // Обучающие подсказки с управлением в начале раунда.
        public const string WardenHint =
            "Ты — Надзиратель. ЛКМ — топор, [F] — дверь, [R] — скан заключённых, [Shift] — бег. Не дай сбежать!";
        public const string PrisonerHint =
            "Ты — Заключённый. [F] — действие, ЛКМ — дверь/барьер, [E] — инвентарь, [Shift] — бег, [C] — присесть (прячет от скана). К выходу!";
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

        // Сколько секунд в начале раунда показывать обучающую подсказку с
        // управлением, прежде чем свернуть её до краткого названия роли.
        public const float HintDuration = 8f;
    }

    // Здоровье и бой
    public static class Combat
    {
        public const int MaxHealth = 100;

        // Урон от удара топором. 3 удара — заключённый повержен.
        public const int AxeDamage = 34;
        public const float AttackRange = 3f;

        // Лечение расходниками (по типу) и здоровье после подъёма.
        public const int HealAmount = 40;     // аптечка (health)
        public const int PillHealAmount = 25; // таблетка (pill)
        public const int SyringeHealAmount = 60; // шприц (syringe)
        public const int ReviveHealth = 50;

        // Сколько секунд удерживать ЛКМ с расходником в руке, чтобы вылечиться.
        public const float HealChannelTime = 2f;

        // Дистанция, с которой можно поднять поверженного союзника.
        public const float ReviveRange = 2.5f;

        // Сколько секунд нужно удерживать F, чтобы поднять поверженного союзника.
        public const float ReviveHoldTime = 1.5f;
    }

    // Взаимодействие/подбор по прицелу (луч из центра камеры).
    public static class Interact
    {
        // Дальность луча взаимодействия и подбора предметов (м).
        public const float Range = 3.5f;

        // Угловой допуск «довинчивания» прицела на предмет: если прямой луч не
        // попал точно, но предмет в пределах этого угла от центра экрана и в прямой
        // видимости — он считается наведённым (мелкие предметы иначе не поймать).
        public const float AimAssistDegrees = 7f;
    }

    // Хотбар (быстрый доступ внизу экрана)
    public static class Hotbar
    {
        // Сколько первых слотов инвентаря показывать в хотбаре и перебирать колесом.
        public const int SlotCount = 4;
    }

    // Куда спавнится выброшенный предмет: перед игроком и над полом, чтобы не
    // проваливался под текстуры (у предметов нет физики — они не падают).
    public const float DropDistance = 0.9f;
    public const float DropHeight = 0.8f;
    // Видимый размер выброшенного предмета зажимается в диапазон [min, max]:
    // крохотные модели (пилюля) увеличиваем, чтобы их было видно на полу, а
    // гигантские (нативный масштаб GLB) уменьшаем, чтобы не заполняли экран.
    public const float DropMinVisibleSize = 0.12f;
    public const float DropMaxVisibleSize = 0.5f;

    // Двери камер
    public static class Door
    {
        // Сколько ударов топором нужно, чтобы выбить запертую дверь.
        public const int Health = 3;

        public const string KeyItemId = "key";
        public const string AxeItemId = "axe";
    }

    // Способности сторон (Веха 8): спринт/выносливость, присед, скан надзирателя.
    public static class Abilities
    {
        // Спринт (доступен всем): множитель скорости и трата выносливости.
        public const float SprintMultiplier = 1.6f;
        public const float StaminaMax = 100f;
        public const float StaminaDrainPerSec = 35f;  // при спринте
        public const float StaminaRegenPerSec = 22f;  // при восстановлении
        public const float StaminaRegenDelay = 1.0f;  // пауза перед реген после спринта
        public const float SprintMinStamina = 8f;      // минимум, чтобы начать спринт

        // Присед (заключённые): множитель скорости, опускание камеры, скрытность.
        public const float CrouchMultiplier = 0.45f;
        public const float CrouchCameraDrop = 0.5f;

        // Скан надзирателя: кулдаун и длительность подсветки заключённых.
        public const float ScanCooldown = 18f;
        public const float ScanRevealDuration = 5f;
    }

    // Инструменты для барьеров (Веха 7). Id совпадают с ItemDatabase.
    public static class Tools
    {
        public const string Screwdriver = "screwdriver";
        public const string Crowbar = "crowbar";
        public const string Cutters = "cutters";
        public const string Keycard = "keycard";
        public const string Cipher = "cipher";
        public const string Shovel = "shovel";
        public const string Explosive = "explosive";
    }
}
