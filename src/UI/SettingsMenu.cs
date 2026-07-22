using Godot;
using EscapeGame.Core;

namespace EscapeGame.UI;

/// <summary>
/// Модальный экран настроек: чувствительность мыши и переназначение клавиш
/// (взаимодействие/ускорение/присед). Показывается поверх меню или паузы;
/// фазу игры не меняет. Пока открыт — GameFlow игнорирует ESC/E (см.
/// <see cref="IsOpen"/>), а сам экран по ESC закрывается.
/// </summary>
public partial class SettingsMenu : Control
{
    // Один шаг ползунка ≈ этому приросту чувствительности (дефолт 0.002 = шаг 5).
    private const float SensPerStep = 0.0004f;

    // Открыт ли экран настроек — GameFlow проверяет, чтобы не реагировать на ESC/E.
    public static bool IsOpen { get; private set; }

    private HSlider _sensSlider;
    private Label _sensValue;
    private Button _resetButton;
    private Button _backButton;

    // Кнопка, ожидающая нажатия клавиши для переназначения (null — не слушаем).
    private string _listeningAction;

    public override void _Ready()
    {
        Visible = false;

        _sensSlider = GetNode<HSlider>("Panel/Margin/VBox/Grid/SensBox/SensSlider");
        _sensValue = GetNode<Label>("Panel/Margin/VBox/Grid/SensBox/SensValue");
        _resetButton = GetNode<Button>("Panel/Margin/VBox/ResetButton");
        _backButton = GetNode<Button>("Panel/Margin/VBox/BackButton");

        _sensSlider.MinValue = 1;
        _sensSlider.MaxValue = 25;
        _sensSlider.Step = 1;
        _sensSlider.ValueChanged += OnSensitivityChanged;

        _resetButton.Pressed += OnReset;
        _backButton.Pressed += Close;

        foreach (string action in Settings.RebindableActions)
        {
            Button button = GetNode<Button>($"Panel/Margin/VBox/Grid/{action}Button");
            button.Pressed += () => StartListening(action);
        }
    }

    public void Open()
    {
        RefreshAll();
        Visible = true;
        IsOpen = true;
        // Настройки нужны с мышью — на всякий случай показываем курсор.
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public void Close()
    {
        _listeningAction = null;
        Visible = false;
        IsOpen = false;
        // Если закрыли во время игры (из паузы) — вернём захват мыши, только когда
        // фаза снова геймплейная. Здесь просто оставляем как есть: паузу/меню
        // курсор устраивает, а выход в геймплей вернёт захват сам (снятие паузы).
    }

    private void RefreshAll()
    {
        _sensSlider.SetValueNoSignal(Mathf.Clamp(Mathf.Round(Settings.MouseSensitivity / SensPerStep), 1, 25));
        UpdateSensLabel();
        RefreshKeyLabels();
    }

    private void RefreshKeyLabels()
    {
        foreach (string action in Settings.RebindableActions)
        {
            Button button = GetNode<Button>($"Panel/Margin/VBox/Grid/{action}Button");
            button.Text = _listeningAction == action ? "..." : Settings.KeyName(action);
        }
    }

    private void UpdateSensLabel()
    {
        _sensValue.Text = ((int)_sensSlider.Value).ToString();
    }

    private void OnSensitivityChanged(double value)
    {
        Settings.MouseSensitivity = (float)value * SensPerStep;
        UpdateSensLabel();
        Settings.Save();
    }

    private void StartListening(string action)
    {
        _listeningAction = action;
        RefreshKeyLabels();
    }

    private void OnReset()
    {
        _listeningAction = null;
        Settings.ResetToDefaults();
        RefreshAll();
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible)
        {
            return;
        }

        if (@event is not InputEventKey key || !key.Pressed || key.Echo)
        {
            return;
        }

        // ESC: отменяет ожидание клавиши либо закрывает экран.
        if (key.PhysicalKeycode == Key.Escape || key.Keycode == Key.Escape)
        {
            GetViewport().SetInputAsHandled();
            if (_listeningAction != null)
            {
                _listeningAction = null;
                RefreshKeyLabels();
            }
            else
            {
                Close();
            }
            return;
        }

        if (_listeningAction == null)
        {
            return;
        }

        // Захватываем нажатую клавишу как новую привязку действия.
        Key physical = key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;
        Settings.Rebind(_listeningAction, physical);
        _listeningAction = null;
        RefreshKeyLabels();
        GetViewport().SetInputAsHandled();
    }
}
