namespace EscapeGame.Interaction;

/// <summary>
/// Вид взаимодействия с объектом мира. Расширяется без изменения сервисного
/// интерфейса — достаточно добавить значение и обработчик в InteractionRelay.
/// </summary>
public enum InteractionKind
{
	Use,
	AxeHit,
}
