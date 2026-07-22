namespace EscapeGame.Interaction;

/// <summary>
/// Запрос на взаимодействие с объектом мира. Универсальная структура,
/// которую легко сериализовать в RPC и расширять новыми видами.
/// </summary>
public readonly record struct InteractionRequest(
	InteractionKind Kind,
	long PlayerId,
	string TargetPath,
	string Payload = "");
