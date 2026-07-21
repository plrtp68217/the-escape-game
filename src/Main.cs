using Godot;
using EscapeGame.Logging;

namespace EscapeGame;

/// <summary>
/// Корневая точка входа игровой сцены. Создаёт высокоуровневые
/// координаторы: UIManager и GameFlow. Сама не содержит игровой логики.
/// </summary>
public partial class Main : Node3D
{
	public override void _Ready()
	{
		// Инициализируем лог как можно раньше, чтобы весь старт сцены попал в файл.
		Log.Init();
		Log.Info(Log.Cat.System, "Main._Ready: создаём координаторы UI и игрового цикла");

		var uiManager = new UI.UIManager { Name = "UIManager" };
		AddChild(uiManager);

		var gameFlow = new GameFlow { Name = "GameFlow" };
		gameFlow.Initialize(uiManager);
		AddChild(gameFlow);
	}
}
