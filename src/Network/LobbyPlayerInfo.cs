using EscapeGame.Player;

namespace EscapeGame.Network;

/// <summary>
/// Информация об игроке в лобби.
/// </summary>
public class LobbyPlayerInfo
{
	public long Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public bool IsReady { get; set; }

	// Назначается сервером при старте игры (см. LobbyManager.AssignRoles).
	public PlayerRole Role { get; set; } = PlayerRole.Prisoner;
}
