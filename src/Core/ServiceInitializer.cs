using Godot;
using EscapeGame.Combat;
using EscapeGame.GameFlow;
using EscapeGame.Interaction;
using EscapeGame.Inventory;
using EscapeGame.Network;
using EscapeGame.Services;

namespace EscapeGame.Core;

/// <summary>
/// Регистрирует реализации сервисов в глобальном <see cref="Services"/u003e.
/// Должен быть последним autoload (см. project.godot), чтобы все синглтоны
/// уже существовали к моменту регистрации.
/// </summary>
public partial class ServiceInitializer : Node
{
	public override void _Ready()
	{
		ServiceLocator.Network = new NetworkService(GetTree());
		ServiceLocator.Players = new PlayerRegistry();
		ServiceLocator.Lobby = new LobbyService(LobbyManager.Instance);
		ServiceLocator.Combat = new CombatService(CombatRelay.Instance);
		ServiceLocator.Inventory = new InventoryService(InventoryRelay.Instance);
		ServiceLocator.Interaction = new InteractionService(InteractionRelay.Instance);
		ServiceLocator.Round = new RoundService(RoundManager.Instance);

		Logging.Log.Info(Logging.Log.Cat.System, "ServiceInitializer: сервисы зарегистрированы");
	}
}
