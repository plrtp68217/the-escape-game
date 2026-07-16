using Godot;
using System.Linq;

namespace EscapeGame.UI;

/// <summary>
/// Табло со списком игроков на сервере. Показывается по удержанию TAB
/// во время игры. Данные берутся из синхронизированного списка LobbyManager.
/// </summary>
public partial class Scoreboard : Control
{
	private Label _playerListLabel;

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

	// Показать/скрыть табло (вызывается из Main по нажатию TAB).
	public void SetShown(bool shown)
	{
		if (shown)
		{
			Refresh();
		}

		Visible = shown;
	}

	private void Refresh()
	{
		var lines = LobbyManager.Instance.Players.Values
			.Select(p => p.Name)
			.ToArray();

		_playerListLabel.Text = lines.Length > 0
			? string.Join("\n", lines)
			: G.Messages.NoPlayers;
	}
}
