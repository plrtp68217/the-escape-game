namespace EscapeGame;

/// <summary>
/// Информация об игроке в лобби.
/// </summary>
public class LobbyPlayerInfo
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsReady { get; set; }
}
