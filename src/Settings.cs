using Godot;

namespace EscapeGame;

/// <summary>
/// Пользовательские настройки: переназначение клавиш и чувствительность мыши.
/// Хранятся в user://settings.cfg и применяются к InputMap при загрузке.
/// Ребиндим только действия из <see cref="RebindableActions"/>.
/// </summary>
public static class Settings
{
    private const string ConfigPath = "user://settings.cfg";
    private const string SectionInput = "input";
    private const string SectionMouse = "mouse";

    // Действия, доступные для переназначения в меню настроек.
    public static readonly string[] RebindableActions = { "interact", "sprint", "crouch" };

    public static float MouseSensitivity { get; set; } = G.MouseSensitivity;

    // Клавиши по умолчанию (для кнопки «сбросить»). Совпадают с project.godot.
    public static Key DefaultKey(string action) => action switch
    {
        "interact" => Key.F,
        "sprint" => Key.Shift,
        "crouch" => Key.C,
        _ => Key.None,
    };

    // Человекочитаемое имя действия для UI.
    public static string ActionName(string action) => action switch
    {
        "interact" => "Взаимодействие",
        "sprint" => "Ускорение",
        "crouch" => "Приседание",
        _ => action,
    };

    public static void Load()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(ConfigPath) != Error.Ok)
        {
            return; // Файла нет — остаются значения из project.godot и дефолт мыши.
        }

        MouseSensitivity = cfg.GetValue(SectionMouse, "sensitivity", MouseSensitivity).AsSingle();

        foreach (string action in RebindableActions)
        {
            int code = cfg.GetValue(SectionInput, action, 0).AsInt32();
            if (code != 0)
            {
                ApplyBinding(action, (Key)code);
            }
        }
    }

    public static void Save()
    {
        var cfg = new ConfigFile();
        cfg.SetValue(SectionMouse, "sensitivity", MouseSensitivity);
        foreach (string action in RebindableActions)
        {
            cfg.SetValue(SectionInput, action, (int)GetKey(action));
        }
        cfg.Save(ConfigPath);
    }

    // Текущая физическая клавиша действия (первое клавиатурное событие).
    public static Key GetKey(string action)
    {
        if (!InputMap.HasAction(action))
        {
            return Key.None;
        }

        foreach (InputEvent ev in InputMap.ActionGetEvents(action))
        {
            if (ev is InputEventKey key)
            {
                return key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;
            }
        }

        return Key.None;
    }

    public static string KeyName(string action)
    {
        Key key = GetKey(action);
        return key == Key.None ? "—" : OS.GetKeycodeString(key);
    }

    public static void Rebind(string action, Key physicalKey)
    {
        ApplyBinding(action, physicalKey);
        Save();
    }

    public static void ResetToDefaults()
    {
        MouseSensitivity = G.MouseSensitivity;
        foreach (string action in RebindableActions)
        {
            ApplyBinding(action, DefaultKey(action));
        }
        Save();
    }

    private static void ApplyBinding(string action, Key physicalKey)
    {
        if (!InputMap.HasAction(action))
        {
            return;
        }

        InputMap.ActionEraseEvents(action);
        InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = physicalKey });
    }
}
