using EscapeGame.Interaction;

namespace EscapeGame.Services;

/// <summary>
/// Запросы к серверному обработчику взаимодействий с миром.
/// Один универсальный метод: новый вид взаимодействия добавляется через
/// <see cref="InteractionKind"/u003e и обработчик в InteractionRelay,
/// без изменения этого интерфейса.
/// </summary>
public interface IInteractionService
{
	void Execute(InteractionRequest request);
}
