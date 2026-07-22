using System;
using System.Linq;
using Godot;
using EscapeGame.Network;
using EscapeGame.Player;

namespace EscapeGame.UI;

/// <summary>
/// Табло со списком игроков на сервере. Показывается по удержанию TAB
/// во время игры. Данные берутся из синхронизированного списка LobbyManager.
/// </summary>
public partial class Scoreboard : Control
{
    private Label _playerListLabel;
    private Func<bool> _visibilitySource;

    public override void _Ready()
    {
        _playerListLabel = GetNode<Label>("Panel/VBoxContainer/PlayerListLabel");
        Visible = false;

        LobbyManager.Instance.LobbyUpdated += Refresh;
        Refresh();
    }

    public override void _ExitTree()
    {
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.LobbyUpdated -= Refresh;
        }
    }

    public void SetVisibilitySource(Func<bool> source)
    {
        _visibilitySource = source;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey key || key.Keycode != G.ScoreboardKey || key.Echo)
        {
            return;
        }

        bool canShow = _visibilitySource?.Invoke() ?? false;

        if (key.Pressed && canShow)
        {
            Refresh();
            Visible = true;
        }
        else if (!key.Pressed)
        {
            Visible = false;
        }
    }

    private void Refresh()
    {
        var lines = LobbyManager.Instance.Players.Values.Select(FormatPlayer).ToArray();

        _playerListLabel.Text = lines.Length > 0 ? string.Join("\n", lines) : G.Messages.NoPlayers;
    }

    private static string FormatPlayer(LobbyPlayerInfo info)
    {
        string role = info.Role == PlayerRole.Warden ? "Надзиратель" : "Заключённый";

        string status = string.Empty;
        if (PlayerController.AllPlayers.TryGetValue(info.Id, out PlayerController player)
            && player.VitalState == PlayerVitalState.Downed)
        {
            status = " (повержен)";
        }

        return $"{info.Name} — {role}{status}";
    }
}
