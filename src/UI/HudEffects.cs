using Godot;

namespace EscapeGame.UI;

/// <summary>
/// Полноэкранный слой визуального фидбека поверх игры: красная виньетка при
/// получении урона, зелёная — при лечении, и хитмаркер по центру, когда
/// надзиратель попал по цели. Всё строится программно и затухает само,
/// поэтому узлу достаточно быть в дереве UI (см. ui_root.tscn).
/// </summary>
public partial class HudEffects : Control
{
    // Насколько долго держатся эффекты (в секундах).
    private const float VignetteDuration = 0.5f;
    private const float HitMarkerDuration = 0.25f;

    // Пиковая непрозрачность виньеток.
    private const float DamagePeak = 0.7f;
    private const float HealPeak = 0.45f;

    private ColorRect _damageVignette;
    private ColorRect _healVignette;
    private ColorRect _knockoutOverlay;
    private ProgressBar _reviveBar;

    private float _damageAlpha;
    private float _healAlpha;
    private float _hitMarkerTime;

    public override void _Ready()
    {
        // Слой перекрывает весь экран, но не должен перехватывать мышь.
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        _damageVignette = CreateVignette(new Color(0.8f, 0f, 0f));
        _healVignette = CreateVignette(new Color(0.2f, 0.9f, 0.3f));

        AddChild(_healVignette);
        AddChild(_damageVignette);

        _knockoutOverlay = CreateKnockoutOverlay();
        AddChild(_knockoutOverlay);

        _reviveBar = CreateReviveBar();
        AddChild(_reviveBar);
    }

    // Красная вспышка урона + тряска экрана (тряску даёт камера).
    public void FlashDamage()
    {
        _damageAlpha = DamagePeak;
    }

    // Зелёная вспышка лечения/подъёма.
    public void FlashHeal()
    {
        _healAlpha = HealPeak;
    }

    // Отметка попадания по центру экрана (для надзирателя).
    public void ShowHitMarker()
    {
        _hitMarkerTime = HitMarkerDuration;
    }

    // Затемнение экрана и надпись «ВАС НОКАУТИРОВАЛИ», пока игрок повержен.
    public void ShowKnockout(bool visible)
    {
        _knockoutOverlay.Visible = visible;
    }

    // Прогресс подъёма поверженного союзника (0..1). Значение <= 0 скрывает бар.
    public void SetReviveProgress(float value)
    {
        if (value <= 0f)
        {
            _reviveBar.Visible = false;
            return;
        }

        _reviveBar.Visible = true;
        _reviveBar.Value = Mathf.Clamp(value, 0f, 1f);
    }

    public override void _Process(double delta)
    {
        float d = (float)delta;

        _damageAlpha = Mathf.MoveToward(_damageAlpha, 0f, DamagePeak / VignetteDuration * d);
        _healAlpha = Mathf.MoveToward(_healAlpha, 0f, HealPeak / VignetteDuration * d);

        SetVignetteIntensity(_damageVignette, _damageAlpha);
        SetVignetteIntensity(_healVignette, _healAlpha);

        if (_hitMarkerTime > 0f)
        {
            _hitMarkerTime = Mathf.MoveToward(_hitMarkerTime, 0f, d);
            QueueRedraw();
        }
    }

    // Четыре коротких штриха «уголком» вокруг центра — классический хитмаркер.
    public override void _Draw()
    {
        if (_hitMarkerTime <= 0f)
        {
            return;
        }

        float alpha = _hitMarkerTime / HitMarkerDuration;
        Color color = new(1f, 1f, 1f, alpha);
        Vector2 center = Size * 0.5f;

        const float gap = 6f;
        const float len = 12f;
        const float width = 2f;

        DrawLine(center + new Vector2(gap, gap), center + new Vector2(gap + len, gap + len), color, width);
        DrawLine(center + new Vector2(-gap, gap), center + new Vector2(-gap - len, gap + len), color, width);
        DrawLine(center + new Vector2(gap, -gap), center + new Vector2(gap + len, -gap - len), color, width);
        DrawLine(center + new Vector2(-gap, -gap), center + new Vector2(-gap - len, -gap - len), color, width);
    }

    // Виньетка: прозрачная в центре, окрашенная по краям. Реализована шейдером,
    // чтобы не тянуть за собой текстуру-градиент.
    private ColorRect CreateVignette(Color color)
    {
        var rect = new ColorRect
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Color = new Color(1, 1, 1, 1),
        };
        rect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var shader = new Shader
        {
            Code = @"
shader_type canvas_item;
uniform float intensity = 0.0;
uniform vec4 tint : source_color = vec4(1.0, 0.0, 0.0, 1.0);
void fragment() {
    float d = distance(UV, vec2(0.5));
    float edge = smoothstep(0.2, 0.75, d);
    COLOR = vec4(tint.rgb, edge * intensity);
}",
        };

        var material = new ShaderMaterial { Shader = shader };
        material.SetShaderParameter("tint", new Color(color.R, color.G, color.B, 1f));
        material.SetShaderParameter("intensity", 0f);
        rect.Material = material;

        return rect;
    }

    private static void SetVignetteIntensity(ColorRect rect, float intensity)
    {
        rect.Visible = intensity > 0.001f;
        if (rect.Material is ShaderMaterial material)
        {
            material.SetShaderParameter("intensity", intensity);
        }
    }

    // Полупрозрачное затемнение на весь экран с крупной красной надписью.
    private static ColorRect CreateKnockoutOverlay()
    {
        var overlay = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.72f),
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false,
        };
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var label = new Label
        {
            Text = "ВАС НОКАУТИРОВАЛИ",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        label.AddThemeFontSizeOverride("font_size", 52);
        label.AddThemeColorOverride("font_color", new Color(1f, 0.25f, 0.25f));
        label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f));
        label.AddThemeConstantOverride("outline_size", 8);
        overlay.AddChild(label);

        return overlay;
    }

    // Полоса прогресса подъёма — по центру, чуть ниже прицела.
    private static ProgressBar CreateReviveBar()
    {
        var bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Step = 0.01,
            Value = 0,
            ShowPercentage = false,
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false,
        };

        bar.AnchorLeft = 0.5f;
        bar.AnchorRight = 0.5f;
        bar.AnchorTop = 0.5f;
        bar.AnchorBottom = 0.5f;
        bar.OffsetLeft = -140f;
        bar.OffsetRight = 140f;
        bar.OffsetTop = 60f;
        bar.OffsetBottom = 84f;

        return bar;
    }
}
