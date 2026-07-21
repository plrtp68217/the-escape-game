using Godot;

namespace EscapeGame.Logging;

/// <summary>
/// Уровень важности записи лога. Порядок = возрастание важности, поэтому
/// <see cref="Log.MinLevel"/> отсекает всё, что ниже.
/// </summary>
public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error,
}

/// <summary>
/// Единая система логирования проекта. Пишет в файл сессии
/// (<c>user://logs/game_ГГГГ-ММ-ДД_ЧЧ-ММ-СС.log</c>) и одновременно зеркалит
/// вывод в консоль Godot, чтобы записи были видны и в редакторе.
///
/// Статический и ленивый: первый вызов любого метода сам открывает файл, поэтому
/// логировать можно из чего угодно (включая autoload-синглтоны, которые
/// стартуют раньше главной сцены). Каждая запись сразу флашится на диск — данные
/// не теряются даже при аварийном закрытии.
/// </summary>
public static class Log
{
    // Категории для группировки и последующей фильтрации записей.
    public static class Cat
    {
        public const string System = "SYS";
        public const string Net = "NET";
        public const string Lobby = "LOBBY";
        public const string Game = "GAME";
        public const string Round = "ROUND";
        public const string Inventory = "INV";
        public const string Combat = "COMBAT";
        public const string UI = "UI";
    }

    // Записи ниже этого уровня игнорируются. По умолчанию пишем всё.
    public static LogLevel MinLevel = LogLevel.Debug;

    private static readonly object Sync = new();
    private static FileAccess _file;
    private static bool _initialized;
    private static string _logPath = string.Empty;

    /// <summary>Путь к текущему файлу лога (res-путь <c>user://...</c>).</summary>
    public static string LogPath => _logPath;

    /// <summary>
    /// Открывает файл лога и пишет заголовок сессии. Вызывается автоматически при
    /// первом логировании; можно вызвать явно (например, из <c>Main._Ready</c>),
    /// чтобы зафиксировать старт как можно раньше.
    /// </summary>
    public static void Init()
    {
        lock (Sync)
        {
            if (_initialized)
            {
                return;
            }

            // Ставим флаг ДО первых записей: методы логирования вызывают EnsureInit,
            // а Write ниже — это предотвращает повторный вход в Init.
            _initialized = true;

            try
            {
                const string dir = "user://logs";
                DirAccess.MakeDirRecursiveAbsolute(dir);

                // В имени файла двоеточия времени недопустимы в путях Windows.
                string stamp = Time.GetDatetimeStringFromSystem(false, true)
                    .Replace(':', '-')
                    .Replace(' ', '_');
                _logPath = $"{dir}/game_{stamp}.log";

                _file = FileAccess.Open(_logPath, FileAccess.ModeFlags.Write);
                if (_file == null)
                {
                    GD.PushError($"[SYS] Не удалось открыть файл лога {_logPath}: {FileAccess.GetOpenError()}");
                }
            }
            catch (System.Exception ex)
            {
                GD.PushError($"[SYS] Ошибка инициализации лога: {ex}");
            }
        }

        // Заголовок вне lock не нужен (lock реентрантен), но держим его после
        // открытия файла, чтобы первые строки уже попали в него.
        Write(LogLevel.Info, Cat.System, $"=== Старт сессии. Файл лога: {_logPath} ===");
        Write(LogLevel.Info, Cat.System,
            $"Godot {Engine.GetVersionInfo()["string"].AsString()} | ОС {OS.GetName()} | отладка={OS.IsDebugBuild()}");
    }

    public static void Debug(string category, string message) => Write(LogLevel.Debug, category, message);

    public static void Info(string category, string message) => Write(LogLevel.Info, category, message);

    public static void Warning(string category, string message) => Write(LogLevel.Warning, category, message);

    public static void Error(string category, string message) => Write(LogLevel.Error, category, message);

    /// <summary>Логирует ошибку вместе с полным текстом исключения.</summary>
    public static void Exception(string category, string message, System.Exception ex)
        => Write(LogLevel.Error, category, $"{message} | Исключение: {ex}");

    private static void Write(LogLevel level, string category, string message)
    {
        if (level < MinLevel)
        {
            return;
        }

        EnsureInit();

        string line = $"[{Time.GetTimeStringFromSystem()}] [{Tag(level)}] [{category}] {message}";

        lock (Sync)
        {
            if (_file != null)
            {
                _file.StoreLine(line);
                _file.Flush();
            }
        }

        // Зеркало в консоль Godot: предупреждения/ошибки идут через Push*, чтобы
        // попадать в панель отладчика редактора; остальное — обычным Print.
        switch (level)
        {
            case LogLevel.Warning:
                GD.PushWarning(line);
                break;
            case LogLevel.Error:
                GD.PushError(line);
                break;
            default:
                GD.Print(line);
                break;
        }
    }

    private static void EnsureInit()
    {
        if (!_initialized)
        {
            Init();
        }
    }

    private static string Tag(LogLevel level) => level switch
    {
        LogLevel.Debug => "DEBUG",
        LogLevel.Info => "INFO ",
        LogLevel.Warning => "WARN ",
        LogLevel.Error => "ERROR",
        _ => "?????",
    };

    /// <summary>
    /// Закрывает файл лога. Записи и так флашатся по строке, поэтому вызов не
    /// обязателен — это лишь аккуратное завершение при выходе.
    /// </summary>
    public static void Close()
    {
        lock (Sync)
        {
            if (_file == null)
            {
                return;
            }

            Write(LogLevel.Info, Cat.System, "=== Завершение сессии ===");
            _file.Close();
            _file = null;
            _initialized = false;
        }
    }
}
