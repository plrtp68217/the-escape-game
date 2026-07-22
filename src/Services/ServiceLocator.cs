namespace EscapeGame.Services;

/// <summary>
/// Глобальный локатор сервисов. Autoload-ноды регистрируют здесь свои
/// реализации, а прикладной код получает зависимости через интерфейсы.
/// Это не полноценный DI-контейнер, но позволяет заменять реализации
/// при тестировании и не жёстко завязываться на конкретные Godot-ноды.
/// </summary>
public static class ServiceLocator
{
	public static INetworkService Network { get; set; }
	public static IPlayerRegistry Players { get; set; }
	public static ILobbyService Lobby { get; set; }
	public static ICombatService Combat { get; set; }
	public static IInventoryService Inventory { get; set; }
	public static IInteractionService Interaction { get; set; }
	public static IRoundService Round { get; set; }
}
