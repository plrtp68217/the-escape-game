using Godot;

namespace EscapeGame;

/// <summary>
/// Корневая точка входа игровой сцены. Создаёт высокоуровневые
/// координаторы: UIManager и GameFlow. Сама не содержит игровой логики.
/// </summary>
public partial class Main : Node3D
{
    public override void _Ready()
    {
        var uiManager = new UI.UIManager { Name = "UIManager" };
        AddChild(uiManager);

        var gameFlow = new GameFlow { Name = "GameFlow" };
        gameFlow.Initialize(uiManager);
        AddChild(gameFlow);
    }
}
