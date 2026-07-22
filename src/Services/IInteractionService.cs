namespace EscapeGame.Services;

/// <summary>
/// Запросы к серверному обработчику взаимодействий с миром.
/// </summary>
public interface IInteractionService
{
	void RequestInteract(long playerId, string interactablePath);
	void RequestAxeHit(long playerId, string doorPath);
}
