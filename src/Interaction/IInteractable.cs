using Godot;

namespace EscapeGame.Interaction;

/// <summary>
/// Объект сцены, с которым игрок может взаимодействовать клавишей "interact"
/// (F). Реализуется узлами вроде WorldItem (подбор) и CellDoor (дверь).
/// </summary>
public interface IInteractable
{
    // Позиция объекта — нужна, чтобы выбрать ближайшую цель. У всех
    // реализаций это Node3D, поэтому свойство наследуется автоматически.
    Vector3 GlobalPosition { get; }

    // Короткая подсказка для HUD (клиент). Пустая строка — ничего не показывать.
    string GetPrompt(PlayerController player);

    // Серверная проверка: может ли игрок сейчас взаимодействовать.
    bool CanInteract(PlayerController player);

    // Серверное действие. Вызывается ТОЛЬКО на сервере через InteractionRelay.
    void Interact(PlayerController player);
}
